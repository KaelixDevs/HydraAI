using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hydra.Core.Security;

namespace Hydra.Core.Artifacts;

public record GeneratedFile(string Name, string Content, string Language);
public record Artifact(string Id, string Name, string Sha256, long Bytes, string? ChatId, string Backend, DateTimeOffset CreatedAt);

public sealed class ArtifactStore
{
    private readonly Workspace workspace;
    public ArtifactStore(string directory) { Directory.CreateDirectory(directory); workspace = new Workspace(directory); }
    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || name.Contains('/') || name.Contains('\\') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.EndsWith('.') || name.EndsWith(' ') || name.StartsWith('.') || Regex.IsMatch(name, @"\A(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)", RegexOptions.IgnoreCase))
            throw new ArgumentException("Use a simple filename without folders, reserved names or special path characters.");
    }
    public async Task<Artifact> SaveTextAsync(GeneratedFile file, string? chatId, string backend, CancellationToken ct = default)
    {
        ValidateName(file.Name);
        if (new[] { ".exe", ".dll", ".rbxl", ".blend", ".fbx", ".zip" }.Contains(Path.GetExtension(file.Name).ToLowerInvariant()))
            throw new InvalidDataException("This format requires a real build/export tool; plain text cannot be saved as this binary format.");
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(file.Content));
        return await SaveAsync(file.Name, input, chatId, backend, ct);
    }
    public async Task<Artifact> ImportAsync(string path, string? chatId, string backend, CancellationToken ct = default)
    {
        await using var input = File.OpenRead(path);
        return await SaveAsync(Path.GetFileName(path), input, chatId, backend, ct);
    }
    private async Task<Artifact> SaveAsync(string name, Stream input, string? chatId, string backend, CancellationToken ct)
    {
        ValidateName(name); var id = Guid.NewGuid().ToString("N");
        var directory = workspace.Resolve(id + "/files"); Directory.CreateDirectory(directory);
        var path = workspace.Resolve(id + "/files/" + name);
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true))
        { await input.CopyToAsync(output, ct); }
        await using var saved = File.OpenRead(path);
        var artifact = new Artifact(id, name, Convert.ToHexString(await SHA256.HashDataAsync(saved, ct)), saved.Length, chatId, backend, DateTimeOffset.UtcNow);
        await File.WriteAllTextAsync(workspace.Resolve(id + "/artifact.json"), JsonSerializer.Serialize(artifact), ct);
        return artifact;
    }
    public IReadOnlyList<Artifact> List(string? chatId = null)
    {
        var result = new List<Artifact>();
        foreach (var directory in Directory.EnumerateDirectories(workspace.Root))
        {
            var id = Path.GetFileName(directory); if (!Regex.IsMatch(id, "\\A[0-9a-f]{32}\\z")) continue;
            var manifest = workspace.Resolve(id + "/artifact.json"); if (!File.Exists(manifest)) continue;
            var artifact = JsonSerializer.Deserialize<Artifact>(File.ReadAllText(manifest));
            if (artifact is not null && artifact.Id == id && (chatId is null || artifact.ChatId == chatId))
            { ValidateName(artifact.Name); result.Add(artifact); }
        }
        return result.OrderByDescending(a => a.CreatedAt).ToList();
    }
    public string PathFor(Artifact artifact)
    {
        if (!Regex.IsMatch(artifact.Id, "\\A[0-9a-f]{32}\\z")) throw new ArgumentException("Invalid artifact ID.");
        ValidateName(artifact.Name); return workspace.Resolve(artifact.Id + "/files/" + artifact.Name);
    }
    public async Task CopyToAsync(Artifact artifact, string destination, CancellationToken ct = default)
    {
        var source = PathFor(artifact);
        await UpdateVerifier.VerifyArtifactAsync(source, artifact.Bytes, artifact.Sha256, ct);
        await using var input = File.OpenRead(source);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
        await input.CopyToAsync(output, ct);
    }
}

public static class GeneratedFiles
{
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    { ["html"] = "html", ["css"] = "css", ["javascript"] = "js", ["js"] = "js", ["typescript"] = "ts", ["ts"] = "ts", ["json"] = "json", ["python"] = "py", ["py"] = "py", ["c++"] = "cpp", ["cpp"] = "cpp", ["c"] = "c", ["csharp"] = "cs", ["c#"] = "cs", ["cs"] = "cs", ["lua"] = "lua", ["luau"] = "luau", ["xml"] = "xml", ["markdown"] = "md", ["md"] = "md", ["svg"] = "svg", ["sql"] = "sql", ["bash"] = "sh", ["powershell"] = "ps1", ["hydra-mesh"] = "mesh.json", ["obj"] = "obj", ["rbxlx"] = "rbxlx" };
    public static IReadOnlyList<GeneratedFile> Extract(string message)
    {
        var result = new List<GeneratedFile>();
        foreach (Match match in Regex.Matches(message, @"^```(?<info>[^\r\n]*)\r?\n(?<body>[\s\S]*?)^```[ \t]*$", RegexOptions.Multiline, TimeSpan.FromSeconds(1)))
        {
            var info = match.Groups["info"].Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var language = info.FirstOrDefault()?.ToLowerInvariant() ?? "text";
            var ext = Extensions.GetValueOrDefault(language, "txt");
            var name = $"generated-{result.Count + 1}.{ext}";
            var requested = info.FirstOrDefault(x => x.StartsWith("filename=", StringComparison.OrdinalIgnoreCase))?[9..].Trim('"');
            if (requested is not null && requested.EndsWith("." + ext, StringComparison.OrdinalIgnoreCase))
            { try { ArtifactStore.ValidateName(requested); name = requested; } catch (ArgumentException) { } }
            result.Add(new(name, match.Groups["body"].Value.TrimEnd('\r', '\n'), language));
            if (result.Count == 20) break;
        }
        return result;
    }
}
