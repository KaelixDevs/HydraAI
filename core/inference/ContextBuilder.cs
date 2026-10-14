using System.Text;

namespace Hydra.Core.Inference;

public static class ContextBuilder
{
    // UTF-8 bytes are a deliberately conservative token estimate; exact tokenizer accounting is future work.
    public static IReadOnlyList<InferenceMessage> Build(string system, IReadOnlyList<InferenceMessage> history, int contextLength)
    {
        var budget = contextLength - 1280 - Encoding.UTF8.GetByteCount(system);
        var selected = new List<InferenceMessage>();
        for (var i = history.Count - 1; i >= 0; i--)
        {
            var size = Encoding.UTF8.GetByteCount(history[i].Content) + 32;
            if (size > budget)
            {
                if (selected.Count == 0) throw new InvalidOperationException("This message is too large for the configured context. Shorten it or import the model with a larger supported context.");
                break;
            }
            selected.Insert(0, history[i]); budget -= size;
        }
        while (selected.Count > 0 && selected[0].Role != "user") selected.RemoveAt(0);
        selected.Insert(0, new("system", system)); return selected;
    }
}
