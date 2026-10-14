using System.Diagnostics;
using System.Text;

namespace Hydra.Core.Tools;

public static class LocalProcess
{
    public static async Task<string> RunAsync(string executable, IEnumerable<string> arguments, string directory,
        CancellationToken ct, IReadOnlyDictionary<string, string>? environment = null)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        // Prevent inherited Git/SDK overrides from redirecting operations outside the chosen workspace.
        foreach (var key in start.Environment.Keys.ToArray())
            if (key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase) || key.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase)) start.Environment.Remove(key);
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        if (environment is not null) foreach (var pair in environment) start.Environment[pair.Key] = pair.Value;
        using var process = Process.Start(start) ?? throw new IOException("The configured local tool did not start.");
        static async Task<string> Drain(StreamReader reader)
        {
            var kept = new StringBuilder(); var buffer = new char[4096]; int n;
            while ((n = await reader.ReadAsync(buffer)) > 0) if (kept.Length < 16000) kept.Append(buffer, 0, Math.Min(n, 16000 - kept.Length));
            return kept.ToString();
        }
        var stdout = Drain(process.StandardOutput); var stderr = Drain(process.StandardError);
        try { await process.WaitForExitAsync(ct); }
        catch { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); await Task.WhenAll(stdout, stderr); throw; }
        var output = await stdout; var error = await stderr;
        if (process.ExitCode != 0) throw new IOException($"{Path.GetFileName(executable)} failed (exit {process.ExitCode}).\n{error}\n{output}");
        return output.Trim();
    }
}
