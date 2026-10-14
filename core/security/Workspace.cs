using System.Security.Cryptography;

namespace Hydra.Core.Security;

public sealed record WorkspaceChange(string RelativePath, string? Before, string After, string AfterHash);

public sealed class Workspace
{
    public string Root { get; }
    private static readonly string[] DeniedParts = [".git", ".ssh", ".aws", ".azure", ".gnupg", "node_modules", "bin", "obj"];
    public Workspace(string root)
    {
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException("Choose an existing project folder.");
        RejectLinks(Root);
    }
    public string Resolve(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':'))
            throw new UnauthorizedAccessException("A project-relative path is required.");
        var path = Path.GetFullPath(Path.Combine(Root, relative));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new UnauthorizedAccessException("The path leaves the selected project.");
        var parts = Path.GetRelativePath(Root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(p => DeniedParts.Contains(p, StringComparer.OrdinalIgnoreCase) || p.StartsWith(".env", StringComparison.OrdinalIgnoreCase) ||
            p.EndsWith(".pem", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".key", StringComparison.OrdinalIgnoreCase) || p.StartsWith("id_rsa", StringComparison.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("This path is excluded from workspace access.");
        RejectLinks(path);
        return path;
    }
    private static void RejectLinks(string path)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Linked or redirected workspace paths are not allowed.");
    }
    public IReadOnlyList<string> ListFiles(int max = 1000)
    {
        var found = new List<string>();
        var pending = new Stack<string>(); pending.Push(Root);
        while (pending.Count > 0 && found.Count < max)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                var relative = Path.GetRelativePath(Root, path);
                try { Resolve(relative); }
                catch (UnauthorizedAccessException) { continue; }
                if (Directory.Exists(path)) pending.Push(path);
                else found.Add(relative);
                if (found.Count >= max) break;
            }
        }
        return found;
    }
    public string Read(string relative)
    {
        var path = Resolve(relative);
        if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new IOException("Workspace text preview is limited to 2 MB.");
        var text = File.ReadAllText(path);
        if (text.Contains('\0')) throw new InvalidDataException("Binary files cannot be edited here.");
        return text;
    }
    public WorkspaceChange Write(string relative, string content, bool approved)
    {
        if (!approved) throw new UnauthorizedAccessException("Explicit write approval is required.");
        if (content.Length > 2 * 1024 * 1024) throw new IOException("Workspace edits are limited to 2 MB.");
        var path = Resolve(relative);
        var before = File.Exists(path) ? Read(relative) : null;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".hydra-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temporary, content); Resolve(relative); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return new WorkspaceChange(relative, before, content, Hash(content));
    }
    public void Undo(WorkspaceChange change, bool approved)
    {
        if (!approved) throw new UnauthorizedAccessException("Undo requires approval.");
        if (Hash(Read(change.RelativePath)) != change.AfterHash) throw new IOException("The file changed after this edit. Undo would overwrite newer work.");
        if (change.Before is null) File.Delete(Resolve(change.RelativePath));
        else Write(change.RelativePath, change.Before, true);
    }
    private static string Hash(string content) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
}
