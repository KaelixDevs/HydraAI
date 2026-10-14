using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Hydra.Core.Security;

namespace Hydra.Core.Models;

public record DownloadArtifact(Uri Url, long Size, string Sha256);

/// <summary>Opt-in, resumable, bounded downloads. A trusted catalogue/UI integration is still required.</summary>
public sealed class ModelDownloader(HttpClient client, string cacheDirectory)
{
    public async Task<string> DownloadAsync(DownloadArtifact artifact, bool networkAllowed, IProgress<long>? progress, CancellationToken ct)
    {
        if (!networkAllowed) throw new UnauthorizedAccessException("Model download network access is disabled.");
        if (!artifact.Url.IsAbsoluteUri || artifact.Url.Scheme != "https" || artifact.Url.UserInfo.Length != 0 || artifact.Url.IsLoopback)
            throw new ArgumentException("Model downloads require an explicit HTTPS source.");
        if (artifact.Size is <= 0 or > 109_951_162_777 || !Regex.IsMatch(artifact.Sha256, "\\A[0-9a-fA-F]{64}\\z"))
            throw new ArgumentException("Expected artifact size and SHA-256 are required.");
        var directory = Path.GetFullPath(cacheDirectory); Directory.CreateDirectory(directory);
        var final = Path.Combine(directory, artifact.Sha256.ToLowerInvariant() + ".gguf");
        var partial = final + ".partial";
        if (File.Exists(final)) { await UpdateVerifier.VerifyArtifactAsync(final, artifact.Size, artifact.Sha256, ct); return final; }
        var offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        if (offset > artifact.Size) throw new InvalidDataException("Partial download exceeds expected size.");
        if (offset < artifact.Size)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, artifact.Url);
            if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            // Reject a redirected destination even when a caller supplied an auto-redirecting client.
            if (response.RequestMessage?.RequestUri is { } destination && destination != artifact.Url)
                throw new InvalidDataException("Redirects require a newly reviewed download source.");
            if (offset > 0 && response.StatusCode == HttpStatusCode.OK) offset = 0;
            else if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                var range = response.Content.Headers.ContentRange;
                if (range?.From != offset || range.Length != artifact.Size || range.Unit != "bytes")
                    throw new InvalidDataException("Server returned an inconsistent byte range.");
            }
            else if (response.StatusCode != HttpStatusCode.OK) throw new InvalidDataException("Unexpected model download response.");
            if (response.Content.Headers.ContentLength is { } length && length != artifact.Size - offset)
                throw new InvalidDataException("Server returned an unexpected artifact length.");
            await using (var output = new FileStream(partial, offset == 0 ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.None, 131072, true))
            await using (var input = await response.Content.ReadAsStreamAsync(ct))
            {
                var buffer = new byte[131072]; int read;
                while ((read = await input.ReadAsync(buffer, ct)) != 0)
                {
                    if (offset + read > artifact.Size) throw new InvalidDataException("Download exceeded declared size.");
                    await output.WriteAsync(buffer.AsMemory(0, read), ct); offset += read; progress?.Report(offset);
                }
            }
        }
        await UpdateVerifier.VerifyArtifactAsync(partial, artifact.Size, artifact.Sha256, ct);
        File.Move(partial, final, false);
        return final;
    }
    public static HttpClient CreateClient() => new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = TimeSpan.FromHours(6) };
}
