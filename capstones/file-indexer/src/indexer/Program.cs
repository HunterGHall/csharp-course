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
