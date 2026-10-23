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
