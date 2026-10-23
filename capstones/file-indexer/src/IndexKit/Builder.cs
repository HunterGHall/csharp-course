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
