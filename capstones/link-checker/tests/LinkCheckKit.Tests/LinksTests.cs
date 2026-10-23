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
