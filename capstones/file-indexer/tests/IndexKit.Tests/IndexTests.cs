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
