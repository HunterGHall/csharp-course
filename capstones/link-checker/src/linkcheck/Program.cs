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
