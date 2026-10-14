using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Hydra.Core.Models;

namespace Hydra.Core.Inference;

/// <summary>Owns a local llama.cpp process. Never accepts remote intelligence endpoints.</summary>
public sealed class LlamaBackend : IInferenceBackend
{
    private Process? process;
    private HttpClient? client;
    public HydraModel? ActiveModel { get; private set; }
    public bool IsRunning => process is { HasExited: false } && ActiveModel is not null;
    public async Task StartAsync(string executable, HydraModel model, CancellationToken ct)
    {
        Stop();
        if (!File.Exists(executable)) throw new FileNotFoundException("Choose a trusted llama-server executable in Model Manager.");
        await ModelRegistry.VerifyAsync(model, ct);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var start = new ProcessStartInfo(Path.GetFullPath(executable))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable))!
        };
        foreach (var argument in new[] { "-m", model.ModelPath, "--host", "127.0.0.1", "--port", port.ToString(), "--ctx-size", model.ContextLength.ToString(), "--api-key", secret, "--n-gpu-layers", "0" }) start.ArgumentList.Add(argument);
        // Do not pass credentials from the parent environment into native inference.
        var keep = new[] { "SystemRoot", "WINDIR", "PATH", "TEMP", "TMP", "LOCALAPPDATA" };
        foreach (var key in start.Environment.Keys.ToArray())
            if (!keep.Contains(key, StringComparer.OrdinalIgnoreCase)) start.Environment.Remove(key);
        try
        {
            process = Process.Start(start) ?? throw new IOException("Could not start the local inference engine.");
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            client = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
            { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(ct);
            startup.CancelAfter(TimeSpan.FromMinutes(5));
            while (true)
            {
                startup.Token.ThrowIfCancellationRequested();
                if (process.HasExited) throw new IOException("Inference engine exited. Check runtime DLLs, model compatibility, and available RAM.");
                try
                {
                    using var probe = CancellationTokenSource.CreateLinkedTokenSource(startup.Token);
                    probe.CancelAfter(TimeSpan.FromSeconds(2));
                    using var response = await client.GetAsync("health", probe.Token);
                    if (response.IsSuccessStatusCode) break;
                }
                catch (HttpRequestException) { }
                catch (OperationCanceledException) when (!startup.IsCancellationRequested) { }
                await Task.Delay(250, startup.Token);
            }
            ActiveModel = model;
        }
        catch { Stop(); throw; }
    }
    public async IAsyncEnumerable<string> GenerateAsync(IReadOnlyList<InferenceMessage> messages, [EnumeratorCancellation] CancellationToken ct)
    {
        if (!IsRunning || client is null) throw new InvalidOperationException("Start a local model in Model Manager first.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = JsonContent.Create(new { messages = messages.Select(m => new { role = m.Role, content = m.Content }), stream = true, temperature = 0.7, max_tokens = 1024 })
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new IOException($"Local inference returned HTTP {(int)response.StatusCode}. Check context size and model compatibility.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        await foreach (var token in ParseStreamAsync(reader, ct)) yield return token;
    }
    public static async IAsyncEnumerable<string> ParseStreamAsync(TextReader reader, [EnumeratorCancellation] CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var payload = line[5..].Trim();
            if (payload == "[DONE]") yield break;
            if (payload.Length == 0) continue;
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("error", out _)) throw new IOException("The local engine reported a generation error.");
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                yield return content.GetString()!;
        }
        throw new IOException("The local inference stream ended before completion.");
    }
    public void Stop()
    {
        ActiveModel = null;
        client?.Dispose(); client = null;
        if (process is not null)
        {
            try { if (!process.HasExited) { process.Kill(true); process.WaitForExit(5000); } }
            catch (InvalidOperationException) { }
            finally { process.Dispose(); process = null; }
        }
    }
    public ValueTask DisposeAsync() { Stop(); return ValueTask.CompletedTask; }
}
