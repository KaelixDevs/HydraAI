using System.Text.Json;

namespace Hydra.Core.SystemInfo;

public enum LogLevel { Error, Warn, Info, Debug, Trace }
public enum DiagnosticEvent { ApplicationStarted, ModelRegistered, EngineStarted, EngineStopped, GenerationCompleted, GenerationFailed }

/// <summary>Deliberately accepts no arbitrary strings, prompts, secrets, paths or exception text.</summary>
public sealed class DiagnosticLog(string directory)
{
    public void Write(LogLevel level, DiagnosticEvent eventId, int numericCode = 0)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "events.jsonl");
        if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) File.WriteAllText(path, "");
        File.AppendAllText(path, JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, level = level.ToString(), eventId = eventId.ToString(), numericCode }) + Environment.NewLine);
    }
    public static string Export(int installedModels) => JsonSerializer.Serialize(new
    {
        app = "Hydra AI", version = "0.1.1-development", hardware = Hardware.Detect(), installedModels,
        telemetry = false, cloudSync = false, note = "User-generated diagnostics. No paths, chats, credentials, or file contents included."
    }, new JsonSerializerOptions { WriteIndented = true });
}
