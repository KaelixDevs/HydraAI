using System.Security.Cryptography;
using System.Text;
using Hydra.Core.Inference;
using Hydra.Core.Models;
using Hydra.Core.Research;
using Hydra.Core.Retrieval;
using Hydra.Core.Security;
using Hydra.Core.Storage;
using Hydra.Core.SystemInfo;

var root = Path.Combine(Environment.GetEnvironmentVariable("HYDRA_TEST_ROOT") ?? Path.GetTempPath(), "hydra-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var tests = new List<(string Name, Func<Task> Test)>();
void Test(string name, Action action) => tests.Add((name, () => { action(); return Task.CompletedTask; }));
void AsyncTest(string name, Func<Task> action) => tests.Add((name, action));
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
async Task ThrowsAsync<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }

Test("SQLite migration, Unicode, parameter injection, and reopen", () =>
{
    var file = Path.Combine(root, "storage.db"); string id;
    using (var db = new Database(file))
    {
        var store = new ChatStore(db); id = store.Create("hello'); DROP TABLE Chats; -- 世界", "auto");
        store.Add(id, "user", "Привет 🐉"); Assert(store.Messages(id)[0].Content == "Привет 🐉");
        Assert(store.List().Count == 1); Assert(db.Query("PRAGMA user_version")[0]["user_version"] == "1");
    }
    using var reopened = new Database(file); Assert(new ChatStore(reopened).Messages(id).Count == 1);
});
Test("Transactions roll back partial writes", () =>
{
    using var db = new Database(Path.Combine(root, "rollback.db"));
    Throws<IOException>(() => db.Transaction(() => { db.Execute("INSERT INTO Settings VALUES('x','y')"); db.Execute("INSERT INTO NotATable VALUES('x')"); }));
    Assert(db.Query("SELECT * FROM Settings").Count == 0);
});
Test("Chat branch, pin, literal search, and delete cascade", () =>
{
    using var db = new Database(Path.Combine(root, "chats.db")); var store = new ChatStore(db);
    var id = store.Create("100% test", "auto"); store.Add(id, "user", "text");
    db.Execute("INSERT INTO Files VALUES('f',?,'file.txt','body','hash')", id);
    var branch = store.Branch(id); Assert(store.Messages(branch).Single().Content == "text");
    Assert(db.Query("SELECT * FROM Files WHERE chat_id=?", branch).Count == 1);
    store.Pin(id, true); Assert(store.List()[0].Id == id); Assert(store.List("_").Count == 0);
    store.Delete(id); Assert(store.Messages(id).Count == 0); Assert(store.Messages(branch).Count == 1);
});
Test("Workspace confinement, secret exclusions, and required write approval", () =>
{
    var directory = Path.Combine(root, "project"); Directory.CreateDirectory(directory); var w = new Workspace(directory);
    foreach (var path in new[] { "../escape.txt", "C:\\outside.txt", ".env", ".git/config", "key.pem", "a.txt:stream", ".ssh/id_rsa" }) Throws<UnauthorizedAccessException>(() => w.Resolve(path));
    Throws<UnauthorizedAccessException>(() => w.Write("a.txt", "body", false));
    var edit = w.Write("src/a.txt", "one", true); Assert(w.Read("src/a.txt") == "one");
    w.Undo(edit, true); Assert(!File.Exists(Path.Combine(directory, "src/a.txt")));
});
Test("Undo protects intervening edits", () =>
{
    var w = new Workspace(Path.Combine(root, "project")); w.Write("edit.txt", "before", true);
    var edit = w.Write("edit.txt", "after", true); w.Write("edit.txt", "newer", true);
    Throws<IOException>(() => w.Undo(edit, true)); Assert(w.Read("edit.txt") == "newer");
});
AsyncTest("Model registration records upstream identity and detects tampering", async () =>
{
    using var db = new Database(Path.Combine(root, "models.db")); var registry = new ModelRegistry(db);
    // Header-only unit fixture; not valid inference weights and never shipped as a model.
    var path = Path.Combine(root, "fixture.gguf"); await File.WriteAllBytesAsync(path, "GGUFfixture"u8.ToArray());
    var model = await registry.ImportAsync(path, "Unit fixture", "CC0-1.0", "test fixture");
    Assert(model.DisplayName == "Unit fixture" && model.IsDevelopmentBackend); await ModelRegistry.VerifyAsync(model, default);
    await File.AppendAllTextAsync(path, "changed"); await ThrowsAsync<InvalidDataException>(() => ModelRegistry.VerifyAsync(model, default));
    await ThrowsAsync<ArgumentException>(() => registry.ImportAsync(path, "Hydra Kairo", "MIT", "test"));
    registry.Remove(model.ModelId); Assert(registry.List().Count == 0 && File.Exists(path));
});
AsyncTest("SSE parses content and Unicode without emitting metadata", async () =>
{
    var input = ": keepalive\n\ndata: {\"choices\":[{\"delta\":{\"role\":\"assistant\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"hello 🐉\"}}]}\n\ndata: [DONE]\n";
    var text = new StringBuilder(); await foreach (var token in LlamaBackend.ParseStreamAsync(new StringReader(input), default)) text.Append(token);
    Assert(text.ToString() == "hello 🐉");
});
AsyncTest("SSE truncated streams and engine errors do not claim success", async () =>
{
    async Task Read(string input) { await foreach (var _ in LlamaBackend.ParseStreamAsync(new StringReader(input), default)) { } }
    await ThrowsAsync<IOException>(() => Read("data: {\"choices\":[]}\n"));
    await ThrowsAsync<IOException>(() => Read("data: {\"error\":\"test failure\"}\n"));
});
AsyncTest("Inference fails closed with no runtime", async () =>
{
    await using var engine = new LlamaBackend();
    await ThrowsAsync<InvalidOperationException>(async () => { await foreach (var _ in engine.GenerateAsync([new("user", "hello")], default)) { } });
    Assert(!engine.IsRunning);
});
Test("Context bounds preserve newest user message and reject oversized input", () =>
{
    var context = ContextBuilder.Build("system", [new("user", new string('a', 3000)), new("assistant", "answer"), new("user", "latest")], 2048);
    Assert(context.First().Role == "system" && context.Last().Content == "latest" && context.Count == 2);
    Throws<InvalidOperationException>(() => ContextBuilder.Build("system", [new("user", new string('x', 3000))], 2048));
});
Test("Auto routing is deterministic and token-aware", () =>
{
    Assert(Profiles.Route("debug my Python").Name == "Kairo"); Assert(Profiles.Route("latest news").Name == "Veyra");
    Assert(Profiles.Route("quiz me").Name == "Lumen"); Assert(Profiles.Route("generate a 3d mesh").Name == "Nyra");
    Assert(Profiles.Route("brainstorm a story").Name == "Zerin"); Assert(Profiles.Route("what does encode mean?").Name == "Elara");
});
Test("Retrieval ranks supplied chunks and rejects invalid overlap", () =>
{
    var chunks = TextRetrieval.Chunk("a", "apples are red", 8, 2).Concat(TextRetrieval.Chunk("b", "python code and debugging"));
    Assert(TextRetrieval.Search(chunks, "python debugging").First().Source == "b");
    Assert(TextRetrieval.Search(chunks, "volcano").Count == 0);
    Throws<ArgumentOutOfRangeException>(() => TextRetrieval.Chunk("a", "b", 10, 10).ToList());
});
Test("Citation references require matching retrieved content", () =>
{
    var manager = new CitationManager(); var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("source body")));
    manager.Register(new("one", new Uri("https://example.org/source"), "Title", "source body", DateTimeOffset.UtcNow, hash));
    Assert(manager.Resolve("one").Title == "Title"); Throws<KeyNotFoundException>(() => manager.Resolve("invented"));
    Throws<InvalidDataException>(() => manager.Register(new("bad", new Uri("https://example.org"), "Title", "changed", DateTimeOffset.UtcNow, hash)));
});
Test("Password hashes use independent salt and verify constant-time derived keys", () =>
{
    var one = Passwords.Hash("correct horse battery"); var two = Passwords.Hash("correct horse battery");
    Assert(one != two && Passwords.Verify("correct horse battery", one)); Assert(!Passwords.Verify("incorrect", one));
    Assert(!Passwords.Verify("x", "pbkdf2-sha256$999999999$invalid$invalid"));
});
Test("Signed manifests reject modified contents", () =>
{
    using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var bytes = "manifest"u8.ToArray();
    var signature = key.SignData(bytes, HashAlgorithmName.SHA256); var pem = key.ExportSubjectPublicKeyInfoPem();
    Assert(UpdateVerifier.VerifyManifest(bytes, signature, pem)); Assert(!UpdateVerifier.VerifyManifest("changed"u8.ToArray(), signature, pem));
});
Test("Hardware capacity detection returns real RAM", () => Assert(Hardware.Detect().RamBytes > 0));

AsyncTest("Downloads require permission and only activate matching complete artifacts", async () =>
{
    var payload = "GGUF-unit-download-fixture"u8.ToArray(); var hash = Convert.ToHexString(SHA256.HashData(payload));
    using var client = new HttpClient(new TestHttpHandler(request => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(payload), RequestMessage = request }));
    var downloader = new ModelDownloader(client, Path.Combine(root, "downloads"));
    var artifact = new DownloadArtifact(new Uri("https://example.org/model.gguf"), payload.Length, hash);
    await ThrowsAsync<UnauthorizedAccessException>(() => downloader.DownloadAsync(artifact, false, null, default));
    var file = await downloader.DownloadAsync(artifact, true, null, default); Assert(File.ReadAllBytes(file).SequenceEqual(payload));
    var corrupt = artifact with { Sha256 = new string('a', 64) };
    await ThrowsAsync<InvalidDataException>(() => downloader.DownloadAsync(corrupt, true, null, default));
    Assert(!File.Exists(Path.Combine(root, "downloads", new string('a', 64) + ".gguf")));
});
AsyncTest("Interrupted downloads resume from an exact verified byte range", async () =>
{
    var payload = "GGUF-resume-fixture"u8.ToArray(); var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    var directory = Path.Combine(root, "resume"); Directory.CreateDirectory(directory);
    await File.WriteAllBytesAsync(Path.Combine(directory, hash + ".gguf.partial"), payload[..4]);
    using var client = new HttpClient(new TestHttpHandler(request =>
    {
        Assert(request.Headers.Range?.Ranges.Single().From == 4);
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.PartialContent) { Content = new ByteArrayContent(payload[4..]), RequestMessage = request };
        response.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(4, payload.Length - 1, payload.Length);
        return response;
    }));
    var file = await new ModelDownloader(client, directory).DownloadAsync(new(new Uri("https://example.org/model.gguf"), payload.Length, hash), true, null, default);
    Assert(File.ReadAllBytes(file).SequenceEqual(payload));
});

if (Environment.GetEnvironmentVariable("HYDRA_LLAMA_RUNTIME") is { } runtime && Environment.GetEnvironmentVariable("HYDRA_TEST_MODEL") is { } weights)
{
    AsyncTest("REAL inference: GGUF import, process startup, streaming and unload", async () =>
    {
        using var db = new Database(Path.Combine(root, "real-inference.db"));
        var model = await new ModelRegistry(db).ImportAsync(weights, "SmolLM2-135M-Instruct (bartowski Q4_K_M)", "Apache-2.0", "https://huggingface.co/bartowski/SmolLM2-135M-Instruct-GGUF");
        await using var engine = new LlamaBackend(); using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await engine.StartAsync(runtime, model, timeout.Token);
        Assert(engine.IsRunning && engine.ActiveModel!.DisplayName == model.DisplayName);
        var generated = new StringBuilder(); var chunks = 0;
        await foreach (var token in engine.GenerateAsync([new("system", "Answer briefly."), new("user", "What is two plus two? Answer in one sentence.")], timeout.Token))
        { generated.Append(token); chunks++; }
        Assert(generated.Length > 0 && chunks > 0, "The real backend emitted no text.");
        Console.WriteLine($"REAL MODEL OUTPUT ({chunks} stream chunks): {generated}");
        engine.Stop(); Assert(!engine.IsRunning && engine.ActiveModel is null);
    });
}

var failed = 0;
ArtifactChecks.Register(root, AsyncTest);
foreach (var (name, test) in tests)
{
    try { await test(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed; {failed} failed.");
// Only remove the exact unique directory created by this test process.
// Git marks loose objects read-only. Do not follow links while clearing fixture attributes.
foreach (var path in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
    File.SetAttributes(path, FileAttributes.Normal);
Directory.Delete(root, true);
return failed == 0 ? 0 : 1;

sealed class TestHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request));
}
