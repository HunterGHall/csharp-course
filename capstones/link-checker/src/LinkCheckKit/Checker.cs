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
