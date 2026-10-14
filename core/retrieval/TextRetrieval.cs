using System.Text;
using System.Text.RegularExpressions;

namespace Hydra.Core.Retrieval;

public record TextChunk(string Source, int Index, string Text);
public static class TextRetrieval
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    { ".txt", ".md", ".csv", ".json", ".xml", ".html", ".css", ".py", ".cs", ".cpp", ".c", ".h", ".hpp", ".js", ".ts", ".tsx", ".jsx", ".java", ".go", ".rs", ".lua", ".luau", ".sql", ".ps1", ".sh", ".yaml", ".yml", ".toml" };
    public static async Task<string> ReadTextAsync(string path, CancellationToken ct = default)
    {
        if (!Extensions.Contains(Path.GetExtension(path))) throw new NotSupportedException("This version reads text and source code. PDF, Office and image parsing are planned.");
        if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new IOException("Text attachments are limited to 2 MB in this version.");
        var text = await File.ReadAllTextAsync(path, new UTF8Encoding(false, true), ct);
        if (text.Contains('\0')) throw new InvalidDataException("This file contains binary data.");
        return text;
    }
    public static IEnumerable<TextChunk> Chunk(string source, string text, int size = 1200, int overlap = 150)
    {
        if (size < 1 || overlap < 0 || overlap >= size) throw new ArgumentOutOfRangeException(nameof(size));
        for (int start = 0, index = 0; start < text.Length; start += size - overlap, index++)
            yield return new TextChunk(source, index, text.Substring(start, Math.Min(size, text.Length - start)));
    }
    public static IReadOnlyList<TextChunk> Search(IEnumerable<TextChunk> chunks, string query, int count = 4)
    {
        var terms = Regex.Matches(query.ToLowerInvariant(), @"[\p{L}\p{N}_]{2,}").Select(m => m.Value).Distinct().ToList();
        return chunks.Select(c => (Chunk: c, Score: terms.Count(t => c.Text.Contains(t, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).Take(count).Select(x => x.Chunk).ToList();
    }
    public static string Format(IEnumerable<TextChunk> chunks) => string.Join("\n\n", chunks.Select(c =>
        $"UNTRUSTED REFERENCE: {c.Source} / chunk {c.Index}\n{c.Text}\nEND REFERENCE"));
}
