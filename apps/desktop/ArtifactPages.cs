using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hydra.Core.Artifacts;

namespace Hydra.Desktop;

public partial class MainWindow
{
    private BackupSettings BackupConfiguration() => new(chats.GetSetting("backup_vault"), chats.GetSetting("backup_git"), chats.GetSetting("tool_git"), chats.GetSetting("backup_commit") == "true");
    private async Task BackupAsync(Artifact artifact, bool explicitRequest)
    {
        if (!explicitRequest && chats.GetSetting("backup_auto") != "true") return;
        var settings = BackupConfiguration();
        if (settings.VaultPath.Length == 0 && settings.GitPath.Length == 0) { if (explicitRequest) Status("Connect an Obsidian vault or Git folder in Settings first."); return; }
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var results = await new BackupService(Path.Combine(dataRoot, "backup-jobs")).BackUpAsync(artifacts, artifact, settings, timeout.Token);
        Status("File saved in Hydra. " + string.Join(" ", results));
    }
    private async Task ExportCopyAsync(Artifact artifact)
    {
        var ext = Path.GetExtension(artifact.Name);
        var picker = new Microsoft.Win32.SaveFileDialog { FileName = artifact.Name, DefaultExt = ext, Filter = $"{ext} file|*{ext}", AddExtension = true, OverwritePrompt = true };
        if (picker.ShowDialog(this) != true) return;
        if (!Path.GetExtension(picker.FileName).Equals(ext, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Keep the original extension so the file's actual format stays correct.");
        if (Path.GetFullPath(picker.FileName).Equals(artifacts.PathFor(artifact), StringComparison.OrdinalIgnoreCase)) { Status("This file is already stored in Hydra."); return; }
        var temporary = picker.FileName + "." + Guid.NewGuid().ToString("N") + ".partial";
        try { await artifacts.CopyToAsync(artifact, temporary); File.Move(temporary, picker.FileName, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Status("Saved " + picker.FileName);
    }
    private void AddFileActions(StackPanel panel, string content, string backend)
    {
        foreach (var file in GeneratedFiles.Extract(content))
        {
            var actions = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
            actions.Children.Add(Button("↓ Save " + file.Name, () => _ = Guard(async () =>
            {
                if (busy) return;
                var artifact = await artifacts.SaveTextAsync(file, currentChat, backend);
                await BackupAsync(artifact, false);
                await ExportCopyAsync(artifact);
                Status("File available in Files & exports. " + StatusText.Text);
            })));
            if (file.Language is "lua" or "luau")
            {
                actions.Children.Add(Button("Roblox place (.rbxlx)", () => _ = Guard(async () =>
                {
                    if (busy) return;
                    var place = await artifacts.SaveTextAsync(new("HydraPlace.rbxlx", FileBuilders.RobloxXml(file.Content), "rbxlx"), currentChat, backend);
                    await BackupAsync(place, false);
                    await ExportCopyAsync(place);
                })));
                actions.Children.Add(Button("Build .rbxl", () => _ = Guard(() => BuildFileAsync("Rojo will package this Luau script into a Roblox place. Hydra does not execute the script.",
                    ct => builders.BuildRobloxAsync(file.Content, chats.GetSetting("tool_rojo"), ct), backend))));
            }
            if (file.Language is "csharp" or "c#" or "cs")
                actions.Children.Add(Button("Build Windows EXE package", () => _ = Guard(() => BuildFileAsync("Compile the displayed C# source with your configured .NET 10 SDK? The ZIP will contain the EXE and required companion files. It requires .NET 10 on the destination PC. Hydra will not run the program.",
                    ct => builders.BuildExeAsync(file.Content, chats.GetSetting("tool_dotnet"), ct), backend))));
            if (file.Language == "hydra-mesh")
                foreach (var format in new[] { "obj", "blend", "fbx" })
                    actions.Children.Add(Button("Export ." + format, () => _ = Guard(() => BuildFileAsync("Export this mesh specification as ." + format + "? BLEND/FBX use your configured Blender. Only Hydra's fixed exporter script runs; model output is treated as mesh data.",
                        ct => builders.BuildMeshAsync(file.Content, format, chats.GetSetting("tool_blender"), ct), backend))));
            panel.Children.Add(actions);
        }
    }
    private async Task BuildFileAsync(string explanation, Func<CancellationToken, Task<string>> build, string backend)
    {
        if (busy || !Confirm(explanation)) return;
        busy = true; operation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        if (sendButton is not null) { sendButton.Content = "■"; sendButton.ToolTip = "Cancel build"; }
        Status("Building locally. Use Stop to cancel. The generated program will not be executed.");
        try
        {
            var path = await build(operation.Token);
            var artifact = await artifacts.ImportAsync(path, currentChat, backend, operation.Token);
            Status("Built " + artifact.Name + ". Stored in Files & exports.");
            await BackupAsync(artifact, false);
            await ExportCopyAsync(artifact);
        }
        finally
        {
            busy = false; operation.Dispose(); operation = null;
            if (sendButton is not null) { sendButton.Content = "↑"; sendButton.ToolTip = "Send message"; }
        }
    }
    private void AddOutputLibrary(StackPanel page)
    {
        page.Children.Add(Text("Generated files", 22));
        page.Children.Add(Text("Saved outputs stay here across restarts. Files are never executed automatically. Binary formats come from actual compilers/exporters.", 13, "#A1A4B7"));
        page.Children.Add(Row(Button("Add an existing output file…", () => _ = Guard(async () =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Title = "Add a file to Hydra outputs and optional backups", Filter = "All files|*.*" };
            if (picker.ShowDialog(this) != true) return;
            var artifact = await artifacts.ImportAsync(picker.FileName, currentChat, "User-supplied file");
            await BackupAsync(artifact, false); ShowFiles();
        }))));
        foreach (var artifact in artifacts.List())
        {
            var card = new StackPanel(); card.Children.Add(Text(artifact.Name, 17));
            card.Children.Add(Text($"{artifact.Bytes:N0} bytes · {artifact.CreatedAt.LocalDateTime:g} · {artifact.Backend}", 11, "#A99BCD"));
            card.Children.Add(Row(Button("Save a copy…", () => _ = Guard(() => ExportCopyAsync(artifact))),
                Button("Back up now", () => _ = Guard(() => BackupAsync(artifact, true)))));
            page.Children.Add(Card(card));
        }
        if (artifacts.List().Count == 0) page.Children.Add(Text("No saved outputs yet. Use the Save buttons beneath a response's code blocks.", 13, "#989DB1"));
    }
    private void AddBackupSettings(StackPanel page)
    {
        var card = new StackPanel(); card.Children.Add(Text("Backups · Obsidian & Git", 22));
        card.Children.Add(Text("Choose local destinations for the files you save or create in Hydra. Each snapshot gets a separate folder. Your vault/repository's own sync software may upload these files.", 13, "#A0A5B8"));
        foreach (var item in new[] { ("backup_vault", "Obsidian vault"), ("backup_git", "Local Git repository") })
        {
            card.Children.Add(Text(item.Item2 + ": " + chats.GetSetting(item.Item1, "Not connected"), 12, "#B5A2E6"));
            card.Children.Add(Row(Button("Connect " + item.Item2, () => _ = Guard(() =>
            {
                var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Choose " + item.Item2 };
                if (picker.ShowDialog(this) == true)
                {
                    _ = new Hydra.Core.Security.Workspace(picker.FolderName);
                    if (item.Item1 == "backup_git" && !Directory.Exists(Path.Combine(picker.FolderName, ".git"))) throw new ArgumentException("Choose an existing regular Git repository root. Hydra does not initialize or push repositories automatically.");
                    chats.SetSetting(item.Item1, picker.FolderName); ShowSettings();
                }
                return Task.CompletedTask;
            })), Button("Disconnect", () => { chats.SetSetting(item.Item1, ""); ShowSettings(); })));
        }
        foreach (var item in new[] { ("backup_auto", "Automatically back up saved outputs and Hydra workspace edits"), ("backup_commit", "Create local Git commits for backup files only (requires git.exe)") })
        {
            var check = new CheckBox { Content = item.Item2, IsChecked = chats.GetSetting(item.Item1) == "true" };
            check.Click += (_, _) => chats.SetSetting(item.Item1, check.IsChecked == true ? "true" : "false"); card.Children.Add(check);
        }
        card.Children.Add(Text("No automatic remote push. Existing vault notes and unrelated Git changes are preserved. Backups contain saved files, not private chat transcripts.", 12, "#9197AD"));
        page.Children.Add(Card(card));
        var tools = new StackPanel(); tools.Children.Add(Text("File export tools", 22));
        tools.Children.Add(Text("HTML, source files, OBJ and Roblox XML places work directly. EXE builds need a .NET 10 SDK; binary RBXL needs Rojo; BLEND and FBX need Blender. Choose trusted executables. Nothing is downloaded or executed automatically.", 13, "#A0A5B8"));
        foreach (var item in new[] { ("tool_git", "Git", "git.exe"), ("tool_dotnet", ".NET 10 SDK", "dotnet.exe"), ("tool_rojo", "Rojo", "rojo.exe"), ("tool_blender", "Blender", "blender.exe") })
        {
            tools.Children.Add(Text(item.Item2 + ": " + chats.GetSetting(item.Item1, "Not configured"), 12, "#B2A0DE"));
            tools.Children.Add(Row(Button("Choose " + item.Item3, () =>
            {
                var picker = new Microsoft.Win32.OpenFileDialog { Title = "Select trusted " + item.Item2, Filter = item.Item2 + "|" + item.Item3 };
                if (picker.ShowDialog(this) == true) { chats.SetSetting(item.Item1, picker.FileName); ShowSettings(); }
            }), Button("Clear", () => { chats.SetSetting(item.Item1, ""); ShowSettings(); })));
        }
        page.Children.Add(Card(tools));
    }
}
