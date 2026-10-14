using System.Security.Cryptography;
using System.Text;

namespace Hydra.Core.Research;

public record SearchHit(Uri Url, string Title, string Snippet);
public record RetrievedSource(string Id, Uri Url, string Title, string Content, DateTimeOffset RetrievedAt, string Sha256);
public interface SearchProvider { Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken ct); }
public interface PageFetcher { Task<RetrievedSource> FetchAsync(Uri url, CancellationToken ct); }
public interface DocumentParser { string Parse(ReadOnlyMemory<byte> bytes, string mediaType); }
public interface ResearchPlanner { IReadOnlyList<string> Plan(string question); }
public interface SourceRanker { double Rank(RetrievedSource source); }

/// <summary>Only accepts retrieved source records; does not invent URLs or establish truth.</summary>
public sealed class CitationManager
{
    private readonly Dictionary<string, RetrievedSource> sources = [];
    public void Register(RetrievedSource source)
    {
        if (source.Url.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(source.Content))
            throw new ArgumentException("Citations require retrieved HTTP(S) content.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.Content)));
        if (!hash.Equals(source.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Source content/hash mismatch.");
        if (sources.TryGetValue(source.Id, out var old) && old != source) throw new InvalidDataException("A citation identifier cannot be reassigned.");
        sources[source.Id] = source;
    }
    public RetrievedSource Resolve(string id) => sources.TryGetValue(id, out var source) ? source : throw new KeyNotFoundException("Citation was not retrieved.");
    public IReadOnlyList<RetrievedSource> All => sources.Values.ToList();
}
public sealed class ResearchSession(SearchProvider search, PageFetcher fetch, CitationManager citations)
{
    public async Task<IReadOnlyList<RetrievedSource>> RetrieveAsync(string query, bool webPermission, CancellationToken ct)
    {
        if (!webPermission) throw new UnauthorizedAccessException("Research network access is disabled.");
        foreach (var hit in (await search.SearchAsync(query, ct)).Take(5))
            citations.Register(await fetch.FetchAsync(hit.Url, ct));
        return citations.All;
    }
}
