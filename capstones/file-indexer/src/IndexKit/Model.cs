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
