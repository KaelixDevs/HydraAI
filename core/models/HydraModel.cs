using System.Security.Cryptography;
using System.Text.Json;
using Hydra.Core.Storage;

namespace Hydra.Core.Models;

public record HydraModel(string ModelId, string DisplayName, string Version, string Architecture,
    long? ParameterCount, string Quantization, int ContextLength, string[] Capabilities,
    string[] Modalities, string HardwareRequirements, string Tokenizer, string ModelPath,
    string Backend, string SystemProfile, string[] ToolPermissions, string License,
    string Source, string Sha256, long SizeBytes, bool IsDevelopmentBackend = true);

public sealed class ModelRegistry(Database db)
{
    public IReadOnlyList<HydraModel> List() => db.Query("SELECT manifest FROM Models ORDER BY id")
        .Select(r => JsonSerializer.Deserialize<HydraModel>(r["manifest"])!).ToList();
    public async Task<HydraModel> ImportAsync(string path, string originalName, string license, string source, int context = 4096, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(originalName) || string.IsNullOrWhiteSpace(license))
            throw new ArgumentException("Original model name and license are required.");
        if (originalName.StartsWith("Hydra", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Development backends must retain their upstream model name.");
        if (context is < 512 or > 131072) throw new ArgumentOutOfRangeException(nameof(context));
        path = Path.GetFullPath(path);
        await using var stream = File.OpenRead(path);
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, ct);
        if (!header.SequenceEqual("GGUF"u8.ToArray())) throw new InvalidDataException("The selected file is not a GGUF model.");
        stream.Position = 0;
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
        var model = new HydraModel(hash[..16], originalName.Trim(), "imported", "GGUF / upstream",
            null, "From GGUF", context, ["text"], ["text"], "Depends on weights, context and runtime",
            "Embedded in GGUF", path, "llama.cpp", "selected at generation", [], license.Trim(), source.Trim(), hash, stream.Length);
        db.Transaction(() =>
        {
            db.Execute("INSERT INTO Models VALUES(?,?) ON CONFLICT(id) DO UPDATE SET manifest=excluded.manifest", model.ModelId, JsonSerializer.Serialize(model));
            db.Execute("INSERT INTO InstalledModels VALUES(?,?,?,?) ON CONFLICT(id) DO UPDATE SET path=excluded.path,sha256=excluded.sha256,installed=excluded.installed", model.ModelId, path, hash, DateTimeOffset.UtcNow.ToString("O"));
        });
        return model;
    }
    public void Remove(string id) => db.Execute("DELETE FROM Models WHERE id=?", id);
    public static async Task VerifyAsync(HydraModel model, CancellationToken ct)
    {
        await using var stream = File.OpenRead(model.ModelPath);
        if (stream.Length != model.SizeBytes || !Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).Equals(model.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Model file changed after import. Import and review it again.");
    }
}
