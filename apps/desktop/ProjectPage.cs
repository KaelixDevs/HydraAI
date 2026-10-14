using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hydra.Core.Security;

namespace Hydra.Desktop;

public partial class MainWindow
{
    private void ShowProjects()
    {
        var page = Page("Build with context.", "A bounded local workspace for Kairo. Reads stay within your selected root; every write requires approval.");
        page.Children.Add(Row(Button("Choose project folder", () =>
        {
            var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Grant read access to a project folder" };
            if (picker.ShowDialog(this) == true) _ = Guard(() =>
            {
                workspace = new Workspace(picker.FolderName);
                db.Execute("INSERT INTO Projects(id,name,root) VALUES(?,?,?)", Guid.NewGuid().ToString("N"), Path.GetFileName(picker.FolderName), picker.FolderName);
                ShowProjects(); return Task.CompletedTask;
            });
        }, true), Button("Undo last write", () => _ = Guard(() =>
        {
            if (workspace is not null && lastChange is not null && Confirm("Undo the last Hydra edit to " + lastChange.RelativePath + "?"))
            { workspace.Undo(lastChange, true); lastChange = null; Status("Last write undone."); ShowProjects(); }
            return Task.CompletedTask;
        }))));
        page.Children.Add(Text("Terminal execution, automatic tool calling, Git operations and OS sandboxing are not implemented. This editor provides manual, permission-bound changes only.", 12, "#AA9ACA"));
        if (workspace is null)
        {
            foreach (var saved in db.Query("SELECT * FROM Projects ORDER BY rowid DESC LIMIT 10"))
                page.Children.Add(Button("Open " + saved["name"], () => _ = Guard(() => { workspace = new Workspace(saved["root"]); ShowProjects(); return Task.CompletedTask; })));
            return;
        }
        page.Children.Add(Text(workspace.Root, 12, "#B4A0EB"));
        var relative = new TextBox { ToolTip = "Project-relative file path", Margin = new Thickness(0, 8, 0, 8) }; page.Children.Add(relative);
        var editor = new TextBox { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new System.Windows.Media.FontFamily("Consolas"), Height = 280, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var list = new ComboBox { MaxDropDownHeight = 280, Margin = new Thickness(0, 8, 0, 10) };
        try { foreach (var file in workspace.ListFiles()) list.Items.Add(file); }
        catch (UnauthorizedAccessException) { Status("Some project entries could not be read."); }
        list.SelectionChanged += (_, _) => _ = Guard(() => { if (list.SelectedItem is string file) { relative.Text = file; editor.Text = workspace.Read(file); } return Task.CompletedTask; });
        page.Children.Add(list); page.Children.Add(editor);
        page.Children.Add(Row(Button("Read file", () => _ = Guard(() => { editor.Text = workspace.Read(relative.Text); return Task.CompletedTask; })),
            Button("Review & write", () => _ = Guard(async () =>
            {
                var path = workspace.Resolve(relative.Text);
                var before = File.Exists(path) ? workspace.Read(relative.Text) : "(new file)";
                if (ReviewChange(relative.Text, before, editor.Text))
                {
                    lastChange = workspace.Write(relative.Text, editor.Text, true); Status("File written. Undo is available until the next edit; intervening changes are protected.");
                    if (chats.GetSetting("backup_auto") == "true")
                    { var artifact = await artifacts.ImportAsync(path, currentChat, "User-approved workspace edit"); await BackupAsync(artifact, false); }
                }
            }), true), Button("Attach file to chat", () => _ = Guard(async () => { await AttachAsync(workspace.Resolve(relative.Text)); selectedProfile = "hydra-kairo"; ShowChat(); }))));
    }
    private bool ReviewChange(string path, string before, string after)
    {
        var dialog = new Window { Title = "Review write · " + path, Owner = this, Width = 960, Height = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var dock = new DockPanel { Margin = new Thickness(20) };
        var controls = Row(Button("Allow this write", () => dialog.DialogResult = true, true), Button("Deny", () => dialog.DialogResult = false));
        DockPanel.SetDock(controls, Dock.Bottom); dock.Children.Add(controls);
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var item in new[] { ("BEFORE", before), ("AFTER", after) })
        {
            var panel = new DockPanel { Margin = new Thickness(8) };
            var label = Text(item.Item1, 13, "#AA94E7"); DockPanel.SetDock(label, Dock.Top); panel.Children.Add(label);
            panel.Children.Add(new TextBox { Text = item.Item2, IsReadOnly = true, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas") }); grid.Children.Add(panel);
        }
        dock.Children.Add(grid); dialog.Content = dock; return dialog.ShowDialog() == true;
    }
}
