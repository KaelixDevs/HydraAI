using Hydra.Core.Models;

namespace Hydra.Core.Inference;

public record InferenceMessage(string Role, string Content);
public interface IInferenceBackend : IAsyncDisposable
{
    bool IsRunning { get; }
    HydraModel? ActiveModel { get; }
    Task StartAsync(string executable, HydraModel model, CancellationToken ct);
    IAsyncEnumerable<string> GenerateAsync(IReadOnlyList<InferenceMessage> messages, CancellationToken ct);
    void Stop();
}
