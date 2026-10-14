namespace Hydra.Core.Models;

public record MeshRequest(string Prompt, string? ImagePath, string ExportFormat);
public record MeshArtifact(string Path, string BackendIdentity, string License, IReadOnlyDictionary<string, double> Measurements);
public interface INyraBackend
{
    string Identity { get; }
    Task<MeshArtifact> GenerateAsync(MeshRequest request, CancellationToken ct);
}
