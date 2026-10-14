using System.Text;
using Hydra.Core.Security;
using Hydra.Core.Tools;

namespace Hydra.Core.Artifacts;

public record BackupSettings(string VaultPath, string GitPath, string GitExecutable, bool CommitLocally);

public sealed class BackupService(string scratchDirectory)
{
    public async Task<IReadOnlyList<string>> BackUpAsync(ArtifactStore store, Artifact artifact, BackupSettings settings, CancellationToken ct)
    {
        var results = new List<string>();
        // Attempt both destinations independently; report failures without undoing a successful backup.
        foreach (var (root, isGit) in new[] { (settings.VaultPath, false), (settings.GitPath, true) })
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            try
            {
                var workspace = new Workspace(root);
                if (isGit && !Directory.Exists(Path.Combine(root, ".git"))) throw new IOException("Choose the root of a regular local Git repository (worktrees are not yet supported).");
                if (isGit && (File.GetAttributes(Path.Combine(root, ".git")) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked Git metadata directories are not supported.");
                var relative = "HydraAI-Backups/" + DateTimeOffset.UtcNow.ToString("yyyy-MM-dd") + "/" + Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(workspace.Resolve(relative + "/files"));
                var file = relative + "/files/" + artifact.Name;
                await store.CopyToAsync(artifact, workspace.Resolve(file), ct);
                var note = relative + "/README.md";
                // Obsidian attachment links use a URI-escaped filename; arbitrary model text is not copied into the note.
                await File.WriteAllTextAsync(workspace.Resolve(note), $"# Hydra export\n\n[Download file](files/{Uri.EscapeDataString(artifact.Name)})\n\nCreated: {artifact.CreatedAt:O}\n\nSHA-256: `{artifact.Sha256}`\n\n{artifact.Bytes} bytes. This file is untrusted generated content; it was not executed by Hydra.\n", Encoding.UTF8, ct);
                if (isGit && settings.CommitLocally)
                {
                    try { await CommitAsync(settings.GitExecutable, root, [file, note], ct); }
                    catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
                    { results.Add("Git files copied, but commit failed: " + ex.Message); continue; }
                }
                results.Add(isGit ? settings.CommitLocally ? "Git backup copied and committed locally." : "Git folder backup copied (not committed)." : "Obsidian vault backup copied.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { results.Add((isGit ? "Git" : "Obsidian") + " backup failed: " + ex.Message); }
        }
        return results;
    }
    private async Task CommitAsync(string git, string root, string[] paths, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(git)) throw new IOException("Select git.exe in Settings to create local commits.");
        var job = Path.Combine(scratchDirectory, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(job);
        var hooks = Path.Combine(job, "no-hooks"); Directory.CreateDirectory(hooks);
        var common = new[] { "--no-optional-locks", "-c", "core.hooksPath=" + hooks, "-c", "core.fsmonitor=false", "-c", "commit.gpgsign=false",
            "-c", "user.name=Hydra AI Backup", "-c", "user.email=backup@hydra.invalid" };
        var env = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = Path.Combine(job, "index"), ["GIT_TERMINAL_PROMPT"] = "0" };
        Task<string> Git(string[] args, bool separateIndex = true) => LocalProcess.RunAsync(git, common.Concat(args), root, ct, separateIndex ? env : new Dictionary<string, string> { ["GIT_TERMINAL_PROMPT"] = "0" });
        var branch = await Git(["symbolic-ref", "HEAD"]);
        if (!branch.StartsWith("refs/heads/", StringComparison.Ordinal)) throw new IOException("A local branch must be checked out.");
        string? head;
        try { head = await Git(["rev-parse", "--verify", "HEAD"]); } catch (IOException) { head = null; }
        await Git(head is null ? ["read-tree", "--empty"] : ["read-tree", head]);
        var blobs = new List<(string Path, string Hash)>();
        foreach (var path in paths)
        {
            var blob = await Git(["hash-object", "-w", "--no-filters", "--", path]); blobs.Add((path, blob));
            await Git(["update-index", "--add", "--cacheinfo", "100644", blob, path]);
        }
        var tree = await Git(["write-tree"]);
        var args = new List<string> { "commit-tree", tree, "-m", "Back up Hydra generated files" };
        if (head is not null) { args.Add("-p"); args.Add(head); }
        var commit = await Git(args.ToArray());
        // Compare-and-swap protects a branch changed by another Git process during the backup.
        await Git(["update-ref", branch, commit, head ?? new string('0', commit.Length)]);
        foreach (var (path, hash) in blobs) await Git(["update-index", "--add", "--cacheinfo", "100644", hash, path], false);
        // Other staged entries are never included in the backup commit or reset.
    }
}
