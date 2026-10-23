# 12. Capstone: Markdown link checker

## Goal

Build a real tool with a proper project structure, a library/executable split, and tests. It scans Markdown files, extracts every link, checks that local files and `#anchors` exist, and prints a report with a CI-friendly exit code.

This exercises: records and class hierarchies, pattern matching, `Regex` source generation, file-system APIs, LINQ, the `Try`/result mindset, multi-project layout, and xUnit.

## What it does

```
$ linkcheck docs/

docs/index.md
  OK    3:./setup.md
  FAIL  4:./missng.md  (file not found)
  FAIL  7:./setup.md#instal  (no heading #instal in ./setup.md)
  SKIP  9:https://dot.net  (external URL)

docs/setup.md
  OK    12:#install

Checked 5 links in 2 files: 2 OK, 2 broken, 1 skipped
$ echo $?
1
```

## Requirements

1. Recursively find `.md` files under one or more given paths (default: `.`).
2. Extract links: inline `[text](target)`, images `![alt](target)`, reference definitions `[ref]: target`, and autolinks `<https://...>`. Ignore anything inside fenced code blocks and inline code.
3. Classify each link: **URL** (has a scheme like `https:`), **anchor** (`#section`), or **local file**.
4. **Local files**: resolve relative to the containing file; check existence. If the target is a `.md` file with a `#fragment`, check the heading exists.
5. **Anchors**: check against the file's own headings, using GitHub's slug rules (`## Hello, World!` becomes `#hello-world`).
6. **URLs**: report as skipped (a checker that hits the network is a stretch goal).
7. `-q` / `--quiet` prints only broken links.
8. Exit code: `0` all good, `1` broken links found, `2` usage or I/O error.

## Project layout

```
link-checker/
├── src/
│   ├── LinkCheckKit/
│   │   ├── LinkCheckKit.csproj
│   │   ├── Links.cs           # pure: ExtractLinks, Classify, Slugify, HeadingSlugs
│   │   └── Checker.cs         # filesystem: FindMarkdownFiles, CheckFile
│   └── linkcheck/
│       ├── linkcheck.csproj
│       └── Program.cs         # argument parsing + report (thin)
└── tests/
    └── LinkCheckKit.Tests/
        ├── LinkCheckKit.Tests.csproj
        ├── LinksTests.cs      # pure functions: no files
        └── CheckerTests.cs    # uses a temporary directory
```

The split matters: `Links.cs` is **pure** (text in, data out) and trivially testable. `Checker.cs` touches the filesystem. `Program.cs` only handles arguments and output. Put the logic in a library, keep the executable thin: the shape from the projects lesson.

## Create the projects

```bash
mkdir link-checker && cd link-checker
dotnet new classlib -o src/LinkCheckKit
dotnet new console  -o src/linkcheck
dotnet new xunit    -o tests/LinkCheckKit.Tests
dotnet add src/linkcheck reference src/LinkCheckKit
dotnet add tests/LinkCheckKit.Tests reference src/LinkCheckKit
rm src/LinkCheckKit/Class1.cs tests/LinkCheckKit.Tests/UnitTest1.cs
```

## The project files

The library (`src/LinkCheckKit/LinkCheckKit.csproj`):

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

The executable (`src/linkcheck/linkcheck.csproj`):

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
    <ProjectReference Include="..\LinkCheckKit\LinkCheckKit.csproj" />
  </ItemGroup>

</Project>
````

## Step 1: parsing (pure code)

Start with `src/LinkCheckKit/Links.cs`. Write the tests (Step 4) *first* if you like: each function takes a string and returns data, so there's nothing to mock.

````csharp
using System.Text;
using System.Text.RegularExpressions;

namespace LinkCheckKit;

/// <summary>A link found in a Markdown document.</summary>
/// <param name="Line">1-based line number.</param>
/// <param name="Target">The target exactly as written, minus any title.</param>
public readonly record struct Link(int Line, string Target);

/// <summary>What a link points at.</summary>
public abstract record LinkKind;

/// <summary>https://..., mailto:..., and so on.</summary>
public sealed record UrlLink : LinkKind;

/// <summary>#heading in the same file.</summary>
public sealed record AnchorLink(string Fragment) : LinkKind;

/// <summary>./other.md#heading</summary>
public sealed record FileLink(string Path, string? Fragment) : LinkKind;

public static partial class Links
{
    // ---- Classification ----

    public static LinkKind Classify(string target)
    {
        if (target.StartsWith('#'))
        {
            return new AnchorLink(target[1..]);
        }
        if (HasScheme(target))
        {
            return new UrlLink();
        }

        var path = target;
        string? fragment = null;

        var hash = path.IndexOf('#');
        if (hash >= 0)
        {
            fragment = path[(hash + 1)..];
            path = path[..hash];
        }
        var question = path.IndexOf('?');
        if (question >= 0)
        {
            path = path[..question];
        }
        return new FileLink(path, fragment);
    }

    /// <summary>"https:", "mailto:" - but not a Windows drive letter such as "C:\".</summary>
    static bool HasScheme(string text)
    {
        var colon = text.IndexOf(':');
        if (colon < 2 || !char.IsLetter(text[0]))
        {
            return false;
        }
        for (var i = 0; i < colon; i++)
        {
            var c = text[i];
            if (!(char.IsLetterOrDigit(c) || c is '+' or '-' or '.'))
            {
                return false;
            }
        }
        return true;
    }

    // ---- Headings and slugs ----

    /// <summary>GitHub-style anchor: "Hello, World!" becomes "hello-world".</summary>
    public static string Slugify(string heading)
    {
        var slug = new StringBuilder();
        foreach (var ch in heading.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
            {
                slug.Append(ch);
            }
            else if (ch == ' ')
            {
                slug.Append('-');
            }
        }
        return slug.ToString();
    }

    /// <summary>Every anchor a document defines. Repeated headings get -1, -2, ... suffixes.</summary>
    public static HashSet<string> HeadingSlugs(string markdown)
    {
        var slugs = new HashSet<string>();
        var seen = new Dictionary<string, int>();
        var fences = new FenceTracker();

        foreach (var line in SplitLines(markdown))
        {
            if (fences.Skips(line))
            {
                continue;
            }

            var hashes = 0;
            while (hashes < line.Length && line[hashes] == '#')
            {
                hashes++;
            }
            if (hashes is < 1 or > 6 || hashes >= line.Length || line[hashes] != ' ')
            {
                continue;
            }

            var text = line[hashes..].Trim().Trim('#').Trim();
            var baseSlug = Slugify(text);
            seen.TryGetValue(baseSlug, out var count);
            seen[baseSlug] = count + 1;
            slugs.Add(count == 0 ? baseSlug : $"{baseSlug}-{count}");
        }
        return slugs;
    }

    // ---- Extracting links ----

    public static List<Link> ExtractLinks(string markdown)
    {
        var links = new List<Link>();
        var fences = new FenceTracker();
        var lines = SplitLines(markdown);

        for (var i = 0; i < lines.Length; i++)
        {
            if (fences.Skips(lines[i]))
            {
                continue;
            }

            var text = InlineCode().Replace(lines[i], m => new string(' ', m.Length));
            var targets = new List<string>();

            foreach (Match m in InlineLink().Matches(text))
            {
                targets.Add(Unwrap(m.Groups[1].Value));
            }
            var reference = ReferenceDefinition().Match(text);
            if (reference.Success)
            {
                targets.Add(Unwrap(reference.Groups[1].Value));
            }
            foreach (Match m in Autolink().Matches(text))
            {
                var target = m.Groups[1].Value;
                if (!targets.Contains(target))
                {
                    targets.Add(target);
                }
            }

            foreach (var target in targets.Where(t => t.Length > 0))
            {
                links.Add(new Link(i + 1, target));
            }
        }
        return links;
    }

    static string Unwrap(string target) =>
        target.StartsWith('<') && target.EndsWith('>') ? target[1..^1] : target;

    // `code` spans are blanked out before any link is looked for.
    [GeneratedRegex("`[^`]*`")]
    private static partial Regex InlineCode();

    // [text](target "optional title") and ![alt](target); we anchor on "](" so nested images work.
    [GeneratedRegex("""\]\(\s*(<[^>]*>|[^)\s]*)(?:\s+"[^"]*")?\s*\)""")]
    private static partial Regex InlineLink();

    // [label]: target
    [GeneratedRegex(@"^ {0,3}\[[^\]]+\]:\s*(<[^>]*>|\S+)")]
    private static partial Regex ReferenceDefinition();

    // <https://example.com>
    [GeneratedRegex(@"<([A-Za-z][A-Za-z0-9+.\-]*:[^>\s]*)>")]
    private static partial Regex Autolink();

    internal static string[] SplitLines(string text) =>
        text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
}

/// <summary>Tracks whether we are inside a fenced code block (``` or ~~~).</summary>
sealed class FenceTracker
{
    char? open;

    /// <summary>True for fence lines and for lines inside a fence - that is, lines to ignore.</summary>
    public bool Skips(string line)
    {
        var trimmed = line.TrimStart(' ');
        char? marker = null;
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            marker = '`';
        }
        else if (trimmed.StartsWith("~~~", StringComparison.Ordinal))
        {
            marker = '~';
        }

        if (open is { } current)
        {
            if (marker == current)
            {
                open = null;
            }
            return true;
        }
        if (marker is { } started)
        {
            open = started;
            return true;
        }
        return false;
    }
}
````

Things worth noticing:

- **`LinkKind` is a small class hierarchy of records** (`UrlLink`, `AnchorLink`, `FileLink`), so the checker can `switch` on it with type patterns and get the data each case needs.
- **`[GeneratedRegex]`** makes the compiler write an optimized matcher at build time. The regex methods are `partial`; you write the signature and the pattern.
- **Blanking inline code** (`InlineCode().Replace(...)`) with spaces rather than deleting it keeps the column positions stable and stops `` `[x](y)` `` from being treated as a link.
- **`FenceTracker`** is a tiny state machine: it remembers whether we're inside a ```` ``` ```` or `~~~` block.
- **`HasScheme` rejects `C:\path`** by requiring a scheme of at least two characters, so a Windows drive letter isn't mistaken for a URL.

## Step 2: checking (filesystem)

````csharp
namespace LinkCheckKit;

public enum Verdict
{
    Ok,
    Broken,
    Skipped,
}

public sealed record CheckResult(Link Link, Verdict Verdict, string? Detail = null);

public sealed record FileReport(string Path, IReadOnlyList<CheckResult> Results);

public sealed class LinkChecker
{
    // Parsing another file's headings is the expensive part, so remember it.
    readonly Dictionary<string, HashSet<string>> slugCache = new();

    /// <summary>All .md files under the given files or directories, sorted, skipping hidden and vendor folders.</summary>
    public static List<string> FindMarkdownFiles(IEnumerable<string> roots)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            if (File.Exists(root))
            {
                found.Add(root);
            }
            else if (Directory.Exists(root))
            {
                foreach (var file in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(root, file);
                    var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (parts[..^1].Any(p => p.StartsWith('.') || p == "node_modules"))
                    {
                        continue;
                    }
                    found.Add(file);
                }
            }
            else
            {
                throw new FileNotFoundException($"no such file or directory: {root}");
            }
        }
        return found.ToList();
    }

    public FileReport CheckFile(string path)
    {
        var text = File.ReadAllText(path);
        var ownSlugs = Links.HeadingSlugs(text);
        slugCache[Path.GetFullPath(path)] = ownSlugs;

        var results = Links.ExtractLinks(text)
            .Select(link => CheckLink(link, path, ownSlugs))
            .ToList();
        return new FileReport(path, results);
    }

    CheckResult CheckLink(Link link, string containingFile, HashSet<string> ownSlugs)
    {
        switch (Links.Classify(link.Target))
        {
            case UrlLink:
                return new CheckResult(link, Verdict.Skipped, "external URL");

            case AnchorLink anchor:
                return ownSlugs.Contains(anchor.Fragment)
                    ? new CheckResult(link, Verdict.Ok)
                    : new CheckResult(link, Verdict.Broken, $"no heading #{anchor.Fragment}");

            case FileLink { Path: "" }:
                return new CheckResult(link, Verdict.Skipped, "empty path");

            case FileLink file:
            {
                var relative = Uri.UnescapeDataString(file.Path);
                if (relative.StartsWith('/'))
                {
                    return new CheckResult(link, Verdict.Skipped, "root-relative path");
                }

                var directory = Path.GetDirectoryName(Path.GetFullPath(containingFile))!;
                var full = Path.GetFullPath(Path.Combine(directory, relative));
                var isFile = File.Exists(full);
                if (!isFile && !Directory.Exists(full))
                {
                    return new CheckResult(link, Verdict.Broken, "file not found");
                }

                if (file.Fragment is { Length: > 0 } fragment && isFile &&
                    full.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
                    !SlugsOf(full).Contains(fragment))
                {
                    return new CheckResult(link, Verdict.Broken, $"no heading #{fragment} in {file.Path}");
                }
                return new CheckResult(link, Verdict.Ok);
            }

            default:
                throw new InvalidOperationException("unreachable: every LinkKind is handled above");
        }
    }

    HashSet<string> SlugsOf(string fullPath)
    {
        if (!slugCache.TryGetValue(fullPath, out var slugs))
        {
            slugs = Links.HeadingSlugs(File.ReadAllText(fullPath));
            slugCache[fullPath] = slugs;
        }
        return slugs;
    }
}
````

- `LinkChecker` is a class (not static) because it holds a **cache** of other files' headings: parsing each target file once, however many links point at it.
- `FindMarkdownFiles` returns a sorted list, so output order never depends on the file system's enumeration order.
- The `switch` over `Links.Classify(...)` uses type patterns plus a property pattern (`FileLink { Path: "" }`), and ends with a `default` that throws, as a safety net for future `LinkKind` cases.
- `Uri.UnescapeDataString` handles links like `my%20file.md`.

## Step 3: the command-line front end

````csharp
using LinkCheckKit;

const string Usage = "usage: linkcheck [-q|--quiet] [path...]";

var quiet = false;
var paths = new List<string>();
foreach (var arg in args)
{
    switch (arg)
    {
        case "-q" or "--quiet":
            quiet = true;
            break;
        case "-h" or "--help":
            Console.WriteLine(Usage);
            return 0;
        case var flag when flag.StartsWith('-'):
            Console.Error.WriteLine($"unknown option '{flag}'\n{Usage}");
            return 2;
        default:
            paths.Add(arg);
            break;
    }
}
if (paths.Count == 0)
{
    paths.Add(".");
}

try
{
    var checker = new LinkChecker();
    var files = LinkChecker.FindMarkdownFiles(paths);
    int ok = 0, broken = 0, skipped = 0;

    foreach (var file in files)
    {
        var report = checker.CheckFile(file);
        var shown = report.Results.Where(r => !quiet || r.Verdict == Verdict.Broken).ToList();

        if (shown.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(report.Path);
        }
        foreach (var result in report.Results)
        {
            switch (result.Verdict)
            {
                case Verdict.Ok: ok++; break;
                case Verdict.Broken: broken++; break;
                case Verdict.Skipped: skipped++; break;
            }
            if (!shown.Contains(result))
            {
                continue;
            }
            var label = result.Verdict switch
            {
                Verdict.Ok => "OK   ",
                Verdict.Broken => "FAIL ",
                _ => "SKIP ",
            };
            var detail = result.Detail is null ? "" : $"  ({result.Detail})";
            Console.WriteLine($"  {label} {result.Link.Line}:{result.Link.Target}{detail}");
        }
    }

    Console.WriteLine();
    Console.WriteLine(
        $"Checked {ok + broken + skipped} links in {files.Count} files: {ok} OK, {broken} broken, {skipped} skipped");
    return broken > 0 ? 1 : 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 2;
}
````

## Step 4: tests

The test project file:

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
    <ProjectReference Include="..\..\src\LinkCheckKit\LinkCheckKit.csproj" />
  </ItemGroup>

</Project>
````

Pure-function tests:

````csharp
using LinkCheckKit;
using Xunit;

namespace LinkCheckKit.Tests;

public class LinksTests
{
    [Theory]
    [InlineData("Hello, World!", "hello-world")]
    [InlineData("  Spaces  ", "--spaces--")]
    [InlineData("What's new in C# 14?", "whats-new-in-c-14")]
    [InlineData("snake_case & kebab-case", "snake_case--kebab-case")]
    public void Slugify_follows_github_rules(string heading, string expected)
    {
        Assert.Equal(expected, Links.Slugify(heading));
    }

    [Fact]
    public void Classify_recognises_urls_anchors_and_files()
    {
        Assert.IsType<UrlLink>(Links.Classify("https://example.com"));
        Assert.IsType<UrlLink>(Links.Classify("mailto:a@b.c"));
        Assert.Equal(new AnchorLink("intro"), Links.Classify("#intro"));
        Assert.Equal(new FileLink("./a.md", "top"), Links.Classify("./a.md#top"));
        Assert.Equal(new FileLink("img.png", null), Links.Classify("img.png?raw=1"));
    }

    [Fact]
    public void Classify_does_not_mistake_a_drive_letter_for_a_scheme()
    {
        Assert.IsType<FileLink>(Links.Classify(@"C:\docs\a.md"));
    }

    [Fact]
    public void HeadingSlugs_numbers_repeats_and_ignores_code_fences()
    {
        var markdown = string.Join('\n',
            "# Title",
            "## Setup",
            "## Setup",
            "```",
            "# not a heading",
            "```",
            "### Closing hashes ###");

        var slugs = Links.HeadingSlugs(markdown);

        Assert.Equal(new[] { "title", "setup", "setup-1", "closing-hashes" }.Order(), slugs.Order());
    }

    [Fact]
    public void ExtractLinks_finds_inline_image_reference_and_auto_links()
    {
        var markdown = string.Join('\n',
            "See [one](./a.md) and ![pic](img/p.png \"A title\").",
            "Also <https://example.com> here.",
            "[ref]: ./b.md#part");

        var links = Links.ExtractLinks(markdown);

        Assert.Equal(
            new[]
            {
                new Link(1, "./a.md"),
                new Link(1, "img/p.png"),
                new Link(2, "https://example.com"),
                new Link(3, "./b.md#part"),
            },
            links);
    }

    [Fact]
    public void ExtractLinks_ignores_inline_code_and_fenced_blocks()
    {
        var markdown = string.Join('\n',
            "Use `[x](not-a-link.md)` like that.",
            "```md",
            "[y](also-not.md)",
            "```",
            "[real](real.md)");

        var links = Links.ExtractLinks(markdown);

        Assert.Equal(new[] { new Link(5, "real.md") }, links);
    }

    [Fact]
    public void ExtractLinks_handles_nested_image_links()
    {
        var links = Links.ExtractLinks("[![badge](b.svg)](https://ci.example)");

        Assert.Equal(new[] { new Link(1, "b.svg"), new Link(1, "https://ci.example") }, links);
    }
}
````

Filesystem tests, each in its own temporary directory:

````csharp
using LinkCheckKit;
using Xunit;

namespace LinkCheckKit.Tests;

/// <summary>Each test instance gets its own temporary directory, deleted afterwards.</summary>
public sealed class CheckerTests : IDisposable
{
    readonly string root = Directory.CreateTempSubdirectory("linkcheck-").FullName;

    public void Dispose() => Directory.Delete(root, recursive: true);

    string Write(string name, string content)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    static Verdict[] Verdicts(FileReport report) => report.Results.Select(r => r.Verdict).ToArray();

    [Fact]
    public void Good_and_broken_file_links_are_told_apart()
    {
        Write("other.md", "# Other\n");
        var index = Write("index.md", "[ok](other.md)\n[bad](missing.md)\n");

        var report = new LinkChecker().CheckFile(index);

        Assert.Equal(new[] { Verdict.Ok, Verdict.Broken }, Verdicts(report));
        Assert.Equal("file not found", report.Results[1].Detail);
    }

    [Fact]
    public void Fragments_are_checked_against_the_target_files_headings()
    {
        Write("other.md", "# Other\n## Install steps\n");
        var index = Write("index.md", "[a](other.md#install-steps)\n[b](other.md#nope)\n");

        var report = new LinkChecker().CheckFile(index);

        Assert.Equal(new[] { Verdict.Ok, Verdict.Broken }, Verdicts(report));
    }

    [Fact]
    public void Same_file_anchors_are_checked()
    {
        var index = Write("index.md", "# Intro\n[here](#intro)\n[gone](#outro)\n");

        var report = new LinkChecker().CheckFile(index);

        Assert.Equal(new[] { Verdict.Ok, Verdict.Broken }, Verdicts(report));
    }

    [Fact]
    public void External_urls_are_skipped()
    {
        var index = Write("index.md", "[web](https://example.com)\n");

        var report = new LinkChecker().CheckFile(index);

        Assert.Equal(new[] { Verdict.Skipped }, Verdicts(report));
    }

    [Fact]
    public void FindMarkdownFiles_is_recursive_sorted_and_skips_hidden_folders()
    {
        Write("b.md", "");
        Write("a.md", "");
        Write("sub/c.md", "");
        Write(".hidden/d.md", "");
        Write("node_modules/e.md", "");
        Write("notes.txt", "");

        var files = LinkChecker.FindMarkdownFiles(new[] { root });

        var names = files.Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')).ToArray();
        Assert.Equal(new[] { "a.md", "b.md", "sub/c.md" }, names);
    }

    [Fact]
    public void FindMarkdownFiles_throws_for_a_missing_path()
    {
        Assert.Throws<FileNotFoundException>(
            () => LinkChecker.FindMarkdownFiles(new[] { Path.Combine(root, "nope") }));
    }
}
````

Run everything:

```bash
dotnet test tests/LinkCheckKit.Tests
```

## Try it

```bash
dotnet run --project src/linkcheck -- ../docs          # check this course's docs
dotnet run --project src/linkcheck -- -q .             # only broken links
dotnet run --project src/linkcheck -- missing-dir; echo "exit code: $?"
```

Point it at a real project's README and docs folder. You'll probably find a broken link.

## Self-check

- [ ] `dotnet build` is warning-free; `dotnet test` is green.
- [ ] Links inside code fences and inline code are never reported.
- [ ] A link to `./other.md#heading` is checked against that file's headings.
- [ ] Repeated headings get `-1`, `-2` suffixes, matching GitHub.
- [ ] Output order is the same every run.
- [ ] You can explain why `Links.cs` has no `File` calls in it.

## Stretch goals

- Check external URLs with `HttpClient` (HEAD request, timeout, and a `--check-urls` flag), in parallel but politely limited.
- Output a JSON report with `--json`.
- Support `.mdx`, and HTML `<a href>` links.
- Report links to images that don't exist separately from page links.
- Add a `--fix` mode that suggests the closest existing heading (Levenshtein distance).

## When you're done

You should be able to explain every line, add a feature, and write a test for it. Then go to [Advanced](../advanced/index.md).
