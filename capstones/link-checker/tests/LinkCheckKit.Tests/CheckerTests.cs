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
