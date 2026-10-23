# 12. Capstone: parallel file indexer

## Goal

Build a concurrent search tool: it walks a directory tree, reads and tokenizes thousands of files in parallel, builds an in-memory inverted index, and answers queries ("which files contain *all* of these words, best matches first?"). It must be **free of shared mutable state, tested, deterministic, and measurably faster in parallel**.

This exercises: `Parallel.ForEachAsync` with bounded parallelism, `Channel<T>`, immutable records handed between threads, designing around *no shared mutable state*, async tests with xUnit, deterministic output from nondeterministic execution, cancellation, and timing with `Stopwatch`.

## What it does

```
$ indexer --stats ~/projects/myapp actor isolation
   14  Sources/App/Sync/CacheActor.cs
    6  docs/concurrency.md
    2  Sources/App/Model/User.cs

indexed 1873 files (412905 words, 21877 distinct terms)
skipped 2, jobs 8
build 84.3 ms
$ echo $?
0
```

## Requirements

1. `indexer [options] <directory> <query words...>`; exit `0` if there are hits, `1` if none, `2` for usage errors.
2. Recursively list files, filtered by extension (`--ext cs,md`, default `cs,md,txt`), skipping hidden entries and build directories (`bin`, `obj`, `node_modules`, ...).
3. Index files concurrently with **at most `--jobs N` in flight** (default: CPU cores). Never start one task per file up front.
4. Files that are not valid UTF-8 or can't be read are **skipped and reported on standard error**, not fatal.
5. Search returns files containing **every** query word, ranked by total occurrences, ties broken by path, so the output is identical no matter how tasks were scheduled.
6. `--top N` limits results; `--stats` prints file/word/term counts, skip count, and build time.
7. The index is **never touched by two threads**: no locks, no concurrent collections.

## Design

The key design decision: **share nothing**.

```
 ListFiles ──► [paths] ──►  ┌── worker ──► FileIndex ──┐
                            ├── worker ──► FileIndex ──┤   Channel<Outcome>   one reading loop
   bounded: at most         ├── worker ──► FileIndex ──┼──────────────────►  (the only code that
   `jobs` in flight         └── worker ──► Skipped ────┘                      calls index.Add)
```

- Each worker reads **one file** and returns an independent, immutable value (`FileIndex`: path + word counts). Workers touch no shared state.
- Results flow through a `Channel<Outcome>`. Only the **single reading loop** mutates the combined `SearchIndex`, so there is nothing to lock.
- `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = jobs` gives bounded parallelism without hand-written worker loops.
- Completion order is nondeterministic, so anything user-visible is sorted (`Skipped`, search hits) to keep output stable.

A `ConcurrentDictionary` or a `lock` around `index.Add` would also work, but adds contention on every file and makes correctness depend on remembering to lock. Measuring both is a stretch goal.

## Project layout

```
file-indexer/
├── src/
│   ├── IndexKit/
│   │   ├── IndexKit.csproj
│   │   ├── Model.cs           # Tokenizer, FileIndex, SearchIndex, SearchHit
│   │   └── Builder.cs         # ListFiles, BuildIndexAsync (the concurrent part)
│   └── indexer/
│       ├── indexer.csproj
│       └── Program.cs         # arguments, timing, output
└── tests/
    └── IndexKit.Tests/
        ├── IndexKit.Tests.csproj
        └── IndexTests.cs
```

## The project files

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

</Project>
````

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\IndexKit\IndexKit.csproj" />
  </ItemGroup>

</Project>
````

Create them the usual way:

```bash
dotnet new classlib -o src/IndexKit
dotnet new console  -o src/indexer
dotnet new xunit    -o tests/IndexKit.Tests
dotnet add src/indexer reference src/IndexKit
dotnet add tests/IndexKit.Tests reference src/IndexKit
```

## Step 1: the model (pure and immutable)

````csharp
using System.Text;

namespace IndexKit;

/// <summary>Splits text into lowercase words: runs of letters, digits and underscores, at least 2 long.</summary>
public static class Tokenizer
{
    public static IEnumerable<string> Tokenize(string text)
    {
        var word = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                word.Append(char.ToLowerInvariant(ch));
            }
            else if (word.Length > 0)
            {
                if (word.Length >= 2)
                {
                    yield return word.ToString();
                }
                word.Clear();
            }
        }
        if (word.Length >= 2)
        {
            yield return word.ToString();
        }
    }
}

/// <summary>
/// Everything one file contributes to the index: its path and how often each word occurs.
/// Immutable, so it can be handed from a worker to the merging loop without any locking.
/// </summary>
public sealed record FileIndex(string Path, IReadOnlyDictionary<string, int> Counts)
{
    public int TotalWords => Counts.Values.Sum();

    public static FileIndex FromText(string path, string text)
    {
        var counts = new Dictionary<string, int>();
        foreach (var word in Tokenizer.Tokenize(text))
        {
            counts[word] = counts.GetValueOrDefault(word) + 1;
        }
        return new FileIndex(path, counts);
    }
}

public sealed record SearchHit(string Path, int Score);

/// <summary>
/// An inverted index: word to (file to occurrences). Not thread-safe on purpose -
/// exactly one caller (the merging loop) ever mutates it.
/// </summary>
public sealed class SearchIndex
{
    readonly Dictionary<string, Dictionary<string, int>> postings = new();

    public int FileCount { get; private set; }
    public long WordCount { get; private set; }
    public int TermCount => postings.Count;

    public void Add(FileIndex file)
    {
        FileCount++;
        WordCount += file.TotalWords;
        foreach (var (word, count) in file.Counts)
        {
            if (!postings.TryGetValue(word, out var files))
            {
                files = new Dictionary<string, int>();
                postings[word] = files;
            }
            files[file.Path] = count;
        }
    }

    /// <summary>
    /// Files containing every query word, best first. Ties are broken by path so the
    /// order never depends on the order files were added.
    /// </summary>
    public IReadOnlyList<SearchHit> Search(IEnumerable<string> queryWords, int? top = null)
    {
        var terms = queryWords.SelectMany(Tokenizer.Tokenize).Distinct().ToList();
        if (terms.Count == 0)
        {
            return Array.Empty<SearchHit>();
        }

        Dictionary<string, int>? scores = null;
        foreach (var term in terms)
        {
            if (!postings.TryGetValue(term, out var files))
            {
                return Array.Empty<SearchHit>();
            }
            if (scores is null)
            {
                scores = new Dictionary<string, int>(files);
                continue;
            }
            foreach (var path in scores.Keys.ToList())
            {
                if (files.TryGetValue(path, out var count))
                {
                    scores[path] += count;
                }
                else
                {
                    scores.Remove(path);
                }
            }
        }

        var hits = scores!
            .Select(pair => new SearchHit(pair.Key, pair.Value))
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Path, StringComparer.Ordinal);
        return (top is { } n ? hits.Take(n) : hits).ToList();
    }
}
````

- `Tokenizer.Tokenize` is a lazy iterator (`yield return`), so a file's words are counted without building an intermediate list.
- `FileIndex` is a record: immutable, so handing it from a worker thread to the reading loop is safe without synchronization (publication through the channel provides the needed memory barrier).
- `SearchIndex` is deliberately **not** thread-safe, and the XML comment says why. Making that contract explicit is part of the design.
- `Search` is a pure function of the index's contents: scores are sums, order is `OrderByDescending(Score).ThenBy(Path, Ordinal)`. No dependency on insertion order, so concurrency can't change the answer.

## Step 2: the concurrent builder

````csharp
using System.Text;
using System.Threading.Channels;

namespace IndexKit;

public sealed record SkippedFile(string Path, string Reason);

public sealed record BuildResult(SearchIndex Index, IReadOnlyList<SkippedFile> Skipped);

public static class Builder
{
    static readonly HashSet<string> IgnoredDirectories = new(StringComparer.Ordinal)
    {
        "bin", "obj", "node_modules", "target", "dist",
    };

    /// <summary>Recursively lists files with one of the extensions (no dots), skipping hidden and build folders.</summary>
    public static List<string> ListFiles(string root, ISet<string> extensions)
    {
        var files = new List<string>();
        Walk(new DirectoryInfo(root));
        files.Sort(StringComparer.Ordinal);
        return files;

        void Walk(DirectoryInfo directory)
        {
            foreach (var file in directory.EnumerateFiles())
            {
                var extension = file.Extension.TrimStart('.').ToLowerInvariant();
                if (!file.Name.StartsWith('.') && extensions.Contains(extension))
                {
                    files.Add(file.FullName);
                }
            }
            foreach (var child in directory.EnumerateDirectories())
            {
                if (!child.Name.StartsWith('.') && !IgnoredDirectories.Contains(child.Name))
                {
                    Walk(child);
                }
            }
        }
    }

    /// <summary>
    /// Indexes the files with at most <paramref name="jobs"/> running at once.
    /// Workers share nothing: each returns an immutable result through a channel,
    /// and this method's single reading loop is the only code that touches the index.
    /// </summary>
    public static async Task<BuildResult> BuildIndexAsync(
        IEnumerable<string> files, int jobs, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(jobs, 1);

        var channel = Channel.CreateUnbounded<Outcome>();

        var producer = Task.Run(async () =>
        {
            try
            {
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = jobs,
                    CancellationToken = cancellationToken,
                };
                await Parallel.ForEachAsync(files, options, async (path, token) =>
                {
                    var outcome = await IndexFileAsync(path, token);
                    await channel.Writer.WriteAsync(outcome, token);
                });
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                channel.Writer.Complete(ex); // surfaces in the reading loop below
            }
        }, CancellationToken.None);

        var index = new SearchIndex();
        var skipped = new List<SkippedFile>();

        await foreach (var outcome in channel.Reader.ReadAllAsync(cancellationToken))
        {
            switch (outcome)
            {
                case Indexed indexed:
                    index.Add(indexed.File);
                    break;
                case Skipped skip:
                    skipped.Add(skip.File);
                    break;
            }
        }
        await producer;

        // Completion order is nondeterministic, so sort anything the user will see.
        skipped.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return new BuildResult(index, skipped);
    }

    static async Task<Outcome> IndexFileAsync(string path, CancellationToken token)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, token);
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return new Indexed(FileIndex.FromText(path, strictUtf8.GetString(bytes)));
        }
        catch (DecoderFallbackException)
        {
            return new Skipped(new SkippedFile(path, "not valid UTF-8"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Skipped(new SkippedFile(path, ex.Message));
        }
    }

    abstract record Outcome;

    sealed record Indexed(FileIndex File) : Outcome;

    sealed record Skipped(SkippedFile File) : Outcome;
}
````

Walk through the shape:

1. **`channel`** carries `Outcome`s (a tiny private hierarchy: `Indexed` or `Skipped`).
2. **`producer`** runs `Parallel.ForEachAsync` over the file list. Each iteration reads one file (`File.ReadAllBytesAsync`), decodes it with a strict UTF-8 decoder, and writes an outcome. When done, it completes the writer. If anything goes wrong it completes the writer **with the exception**, so the reading loop fails instead of hanging.
3. **The `await foreach` loop** is the only place `index.Add` is called.
4. **Sorting `skipped`** at the end removes any dependence on completion order.
5. **`cancellationToken`** reaches `ForEachAsync`, `ReadAllBytesAsync`, `WriteAsync` and `ReadAllAsync`, so cancellation is prompt.

## Step 3: the command-line front end

````csharp
using System.Diagnostics;
using IndexKit;

const string Usage = """
    usage: indexer [options] <directory> <query words...>

    options:
      --ext a,b,c   file extensions to index (default: cs,md,txt)
      --jobs N      files processed at once (default: number of CPU cores)
      --top N       show at most N results
      --stats       print counts and timing
    """;

var extensions = new HashSet<string> { "cs", "md", "txt" };
var jobs = Environment.ProcessorCount;
int? top = null;
var stats = false;
var positional = new List<string>();

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--ext" when i + 1 < args.Length:
            extensions = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.TrimStart('.').ToLowerInvariant())
                .ToHashSet();
            break;
        case "--jobs" when i + 1 < args.Length && int.TryParse(args[i + 1], out var j) && j >= 1:
            jobs = j;
            i++;
            break;
        case "--top" when i + 1 < args.Length && int.TryParse(args[i + 1], out var t) && t >= 1:
            top = t;
            i++;
            break;
        case "--stats":
            stats = true;
            break;
        case var flag when flag.StartsWith("--"):
            Console.Error.WriteLine($"bad or incomplete option '{flag}'\n{Usage}");
            return 2;
        default:
            positional.Add(args[i]);
            break;
    }
}

if (positional.Count < 2)
{
    Console.Error.WriteLine(Usage);
    return 2;
}

var root = positional[0];
var query = positional.Skip(1).ToList();

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"not a directory: {root}");
    return 2;
}

var timer = Stopwatch.StartNew();
var files = Builder.ListFiles(root, extensions);
var result = await Builder.BuildIndexAsync(files, jobs);
timer.Stop();

foreach (var skipped in result.Skipped)
{
    Console.Error.WriteLine($"skipped {Path.GetRelativePath(root, skipped.Path)}: {skipped.Reason}");
}

var hits = result.Index.Search(query, top);
foreach (var hit in hits)
{
    var relative = Path.GetRelativePath(root, hit.Path).Replace('\\', '/');
    Console.WriteLine($"{hit.Score,5}  {relative}");
}

if (stats)
{
    var index = result.Index;
    Console.WriteLine();
    Console.WriteLine($"indexed {index.FileCount} files ({index.WordCount} words, {index.TermCount} distinct terms)");
    Console.WriteLine($"skipped {result.Skipped.Count}, jobs {jobs}");
    Console.WriteLine($"build {timer.Elapsed.TotalMilliseconds:F1} ms");
}

return hits.Count > 0 ? 0 : 1;
````

## Step 4: tests

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\IndexKit\IndexKit.csproj" />
  </ItemGroup>

</Project>
````

````csharp
using IndexKit;
using Xunit;

namespace IndexKit.Tests;

public class ModelTests
{
    [Fact]
    public void Tokenize_lowercases_splits_on_punctuation_and_drops_single_letters()
    {
        var words = Tokenizer.Tokenize("Hello, World! a x_1 C# 42").ToArray();

        Assert.Equal(new[] { "hello", "world", "x_1", "42" }, words);
    }

    [Fact]
    public void Search_requires_every_word_and_ranks_by_total_occurrences()
    {
        var index = new SearchIndex();
        index.Add(FileIndex.FromText("a.txt", "actor actor isolation"));
        index.Add(FileIndex.FromText("b.txt", "actor isolation isolation isolation"));
        index.Add(FileIndex.FromText("c.txt", "actor only"));

        var hits = index.Search(new[] { "Actor", "isolation" });

        Assert.Equal(new[] { new SearchHit("b.txt", 4), new SearchHit("a.txt", 3) }, hits);
    }

    [Fact]
    public void Search_breaks_ties_by_path_regardless_of_insertion_order()
    {
        var index = new SearchIndex();
        index.Add(FileIndex.FromText("z.txt", "swift"));
        index.Add(FileIndex.FromText("m.txt", "swift"));
        index.Add(FileIndex.FromText("a.txt", "swift"));

        var hits = index.Search(new[] { "swift" });

        Assert.Equal(new[] { "a.txt", "m.txt", "z.txt" }, hits.Select(h => h.Path));
    }

    [Fact]
    public void Search_honours_top_and_returns_nothing_for_unknown_or_empty_queries()
    {
        var index = new SearchIndex();
        index.Add(FileIndex.FromText("a.txt", "one two"));
        index.Add(FileIndex.FromText("b.txt", "one"));

        Assert.Single(index.Search(new[] { "one" }, top: 1));
        Assert.Empty(index.Search(new[] { "missing" }));
        Assert.Empty(index.Search(new[] { "!!" }));
    }

    [Fact]
    public void Counts_are_tracked()
    {
        var index = new SearchIndex();
        index.Add(FileIndex.FromText("a.txt", "red green red"));
        index.Add(FileIndex.FromText("b.txt", "green blue"));

        Assert.Equal(2, index.FileCount);
        Assert.Equal(5, index.WordCount);
        Assert.Equal(3, index.TermCount);
    }
}

public sealed class BuilderTests : IDisposable
{
    readonly string root = Directory.CreateTempSubdirectory("indexer-").FullName;

    public void Dispose() => Directory.Delete(root, recursive: true);

    string Write(string name, string content)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ListFiles_filters_by_extension_and_skips_hidden_and_build_folders()
    {
        Write("a.md", "");
        Write("sub/b.txt", "");
        Write("sub/c.png", "");
        Write(".git/d.md", "");
        Write("obj/e.md", "");

        var files = Builder.ListFiles(root, new HashSet<string> { "md", "txt" });

        var names = files.Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')).ToArray();
        Assert.Equal(new[] { "a.md", "sub/b.txt" }, names);
    }

    [Fact]
    public async Task BuildIndexAsync_skips_files_that_are_not_utf8_instead_of_failing()
    {
        var good = Write("good.txt", "hello world");
        var bad = Path.Combine(root, "bad.txt");
        File.WriteAllBytes(bad, new byte[] { 0xFF, 0xFE, 0xFD, 0x80 });

        var result = await Builder.BuildIndexAsync(new[] { good, bad }, jobs: 2);

        Assert.Equal(1, result.Index.FileCount);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal(bad, skipped.Path);
        Assert.Equal("not valid UTF-8", skipped.Reason);
    }

    [Fact]
    public async Task BuildIndexAsync_gives_identical_results_at_any_concurrency()
    {
        for (var i = 0; i < 40; i++)
        {
            Write($"f{i:D2}.txt", string.Concat(Enumerable.Repeat("needle ", i % 7 + 1)) + "haystack");
        }
        var files = Builder.ListFiles(root, new HashSet<string> { "txt" });

        var serial = await Builder.BuildIndexAsync(files, jobs: 1);
        var parallel = await Builder.BuildIndexAsync(files, jobs: 8);

        Assert.Equal(
            serial.Index.Search(new[] { "needle" }),
            parallel.Index.Search(new[] { "needle" }));
        Assert.Equal(serial.Index.WordCount, parallel.Index.WordCount);
        Assert.Equal(40, parallel.Index.FileCount);
    }

    [Fact]
    public async Task BuildIndexAsync_rejects_a_job_count_below_one()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Builder.BuildIndexAsync(Array.Empty<string>(), jobs: 0));
    }

    [Fact]
    public async Task BuildIndexAsync_observes_cancellation()
    {
        var path = Write("a.txt", "hello");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Builder.BuildIndexAsync(new[] { path }, jobs: 2, cts.Token));
    }
}
````

The most important test is `BuildIndexAsync_gives_identical_results_at_any_concurrency`: it builds the same index with `jobs: 1` and `jobs: 8` and checks that the results match exactly. That's the executable form of the "deterministic output" requirement.

```bash
dotnet test tests/IndexKit.Tests
```

## Try it

```bash
# Index this course's Markdown (or any project):
dotnet run -c Release --project src/indexer -- --stats --ext md ../docs async await

# Compare parallelism:
dotnet run -c Release --project src/indexer -- --stats --jobs 1 ~/projects/myapp todo
dotnet run -c Release --project src/indexer -- --stats --jobs 8 ~/projects/myapp todo
```

Run it on a large tree (a clone of the `dotnet/runtime` repo is great). Always use `-c Release` when timing.

## Self-check

- [ ] `dotnet build` is warning-free and `dotnet test` is green.
- [ ] Output is byte-for-byte identical for `--jobs 1`, `2` and `16`.
- [ ] Only one place in the code calls `index.Add`.
- [ ] A binary file in the tree is reported on standard error and the run still succeeds.
- [ ] `--jobs 1` vs `--jobs 8` shows a measured speedup on a big directory. If it doesn't, find out why (small files? disk-bound? the channel loop as the bottleneck?).
- [ ] Cancelling (Ctrl+C wired to a `CancellationTokenSource`) stops promptly.

## Stretch goals

- Wire Ctrl+C to a `CancellationTokenSource` (`Console.CancelKeyPress`) and pass the token to `BuildIndexAsync`.
- Replace the single-reader merge with sharded indexes (one per worker) merged at the end, and measure.
- Add phrase search ("exact words in order") by storing positions.
- Add a `--watch` mode using `FileSystemWatcher` that updates the index incrementally.
- Rank with TF-IDF instead of raw counts.
- Stream results with `IAsyncEnumerable<SearchHit>`.
- Replace `File.ReadAllBytesAsync` with pooled buffers and `Span<byte>`-based tokenizing; benchmark with BenchmarkDotNet.
- Benchmark the `lock` + `ConcurrentDictionary` alternative against the channel design.

## When you're done

Stop studying and go build something real. You now have the tools to read the runtime's own source, contribute to open-source .NET projects, and design systems that are correct first and fast second.
