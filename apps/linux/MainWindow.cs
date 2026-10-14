using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Hydra.Core.Artifacts;
using Hydra.Core.Inference;
using Hydra.Core.Models;
using Hydra.Core.Storage;

namespace Hydra.Linux;

public sealed class MainWindow : Window
{
    private readonly Database database;
    private readonly ChatStore chats;
    private readonly ModelRegistry models;
    private readonly ArtifactStore files;
    private readonly LlamaBackend engine = new();
    private readonly string root;
    private readonly ContentControl page = new();
    private readonly TextBlock status = new() { Text = "Local workspace", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 8) };
    private readonly TextBox prompt = new() { PlaceholderText = "Message Hydra…", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 48, MaxHeight = 150 };
    private readonly StackPanel messages = new() { Spacing = 12, Margin = new Thickness(16) };
    private ScrollViewer? transcript;
    private string? chatId;
    private string profile = "auto";
    private CancellationTokenSource? operation;
    private bool busy;

    public MainWindow()
    {
        Title = "Hydra AI"; Width = 1100; Height = 800; MinWidth = 380; MinHeight = 480;
        Background = Brush.Parse("#101116"); Foreground = Brush.Parse("#EEEFF5");
        root = Environment.GetEnvironmentVariable("HYDRA_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HydraAI");
        database = new Database(Path.Combine(root, "hydra.db")); chats = new(database); models = new(database); files = new(Path.Combine(root, "artifacts"));
        var shell = new DockPanel(); var navigation = new WrapPanel { Margin = new Thickness(12), Orientation = Orientation.Horizontal };
        navigation.Children.Add(Label("HYDRA", 22));
        foreach (var (name, action) in new (string, Action)[] { ("New chat", () => { chatId = null; ShowChat(); }), ("Chats", ShowHistory), ("Models", ShowModels), ("Files", ShowFiles), ("Settings", ShowSettings) })
            navigation.Children.Add(Btn(name, () => { if (!busy) action(); }));
        DockPanel.SetDock(navigation, Dock.Top); shell.Children.Add(navigation);
        DockPanel.SetDock(status, Dock.Bottom); shell.Children.Add(status); shell.Children.Add(page); Content = shell;
        Closing += (_, e) => { if (busy) { e.Cancel = true; operation?.Cancel(); status.Text = "Stopping. Close again once the operation finishes."; } };
        Closed += (_, _) => { engine.Stop(); database.Dispose(); };
        ShowChat();
    }
    private static TextBlock Label(string text, double size = 14) => new() { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 6) };
    private Button Btn(string label, Action action)
    {
        var button = new Button { Content = label, MinHeight = 44, Margin = new Thickness(4), Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(8) };
        button.Click += (_, _) => action(); return button;
    }
    private void Run(Func<Task> action) => _ = Guard(action);
    private async Task Guard(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { status.Text = "Stopped."; }
        catch (Exception ex) { status.Text = ex.Message; }
    }
    private StackPanel Page(string title)
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(20), MaxWidth = 1000 };
        panel.Children.Add(Label(title, 26)); page.Content = new ScrollViewer { Content = panel }; return panel;
    }
    private void ShowChat()
    {
        if (prompt.Parent is Panel parent) parent.Children.Remove(prompt);
        if (transcript is not null) transcript.Content = null;
        messages.Children.Clear();
        if (chatId is null) messages.Children.Add(Label("A place for every idea. Choose a local model in Models to get started.", 22));
        else foreach (var message in chats.Messages(chatId)) AddMessage(message.Role, message.Content, message.Backend);
        var layout = new DockPanel(); var composer = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(16) };
        var menu = Btn("⋯", () =>
        {
            var options = new StackPanel { Margin = new Thickness(8), Spacing = 4 };
            var select = new ComboBox { ItemsSource = new[] { "Auto" }.Concat(Profiles.All.Select(p => p.Name)).ToArray(), SelectedIndex = profile == "auto" ? 0 : Profiles.All.ToList().FindIndex(p => p.Id == profile) + 1, MinWidth = 220 };
            select.SelectionChanged += (_, _) => profile = select.SelectedIndex <= 0 ? "auto" : Profiles.All[select.SelectedIndex - 1].Id;
            options.Children.Add(Label("Profile")); options.Children.Add(select);
            var dialog = new Window { Title = "Conversation options", Width = 320, Height = 330, Content = options, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            options.Children.Add(Btn("Attach text / source", () => Run(async () => { dialog.Close(); await Attach(); })));
            options.Children.Add(Btn("Model manager", () => { dialog.Close(); if (!busy) ShowModels(); }));
            options.Children.Add(Btn("Close", dialog.Close)); dialog.ShowDialog(this);
        });
        composer.Children.Add(menu); Grid.SetColumn(prompt, 1); composer.Children.Add(prompt);
        var send = Btn("↑ / Stop", () => { if (busy) operation?.Cancel(); else Run(Send); }); Grid.SetColumn(send, 2); composer.Children.Add(send);
        DockPanel.SetDock(composer, Dock.Bottom); layout.Children.Add(composer);
        transcript = new ScrollViewer { Content = messages }; layout.Children.Add(transcript); page.Content = layout;
        prompt.KeyDown -= PromptKey; prompt.KeyDown += PromptKey;
    }
    private void PromptKey(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter && !e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
        { e.Handled = true; if (!busy) Run(Send); }
    }
    private TextBlock AddMessage(string role, string text, string backend)
    {
        var panel = new StackPanel { Spacing = 6 }; panel.Children.Add(Label(role == "user" ? "You" : backend, 12));
        var content = Label(text, 15); panel.Children.Add(content);
        if (role == "assistant") foreach (var file in GeneratedFiles.Extract(text))
            panel.Children.Add(Btn("Save " + file.Name, () => Run(async () =>
            {
                var artifact = await files.SaveTextAsync(file, chatId, backend); await Backup(artifact, false); await SaveCopy(artifact);
            })));
        messages.Children.Add(new Border { Child = panel, Background = Brush.Parse(role == "user" ? "#252335" : "#1A1B24"), CornerRadius = new CornerRadius(12), Padding = new Thickness(16) });
        return content;
    }
    private async Task Send()
    {
        var text = prompt.Text?.Trim(); if (busy || string.IsNullOrWhiteSpace(text)) return;
        var model = engine.ActiveModel; if (!engine.IsRunning || model is null) { status.Text = "Load a local model in Models first."; return; }
        var specialist = profile == "auto" ? Profiles.Route(text) : Profiles.Get(profile);
        chatId ??= chats.Create(text[..Math.Min(70, text.Length)], specialist.Id);
        chats.Add(chatId, "user", text); prompt.Text = ""; ShowChat();
        var output = new System.Text.StringBuilder(); var label = specialist.Name + " / " + model.DisplayName;
        var rendered = AddMessage("assistant", "", label); busy = true; operation = new CancellationTokenSource(); var state = "complete";
        try
        {
            var context = ContextBuilder.Build(Profiles.SystemPrompt(specialist, model), chats.Messages(chatId).Select(m => new InferenceMessage(m.Role, m.Content)).ToList(), model.ContextLength);
            await foreach (var token in engine.GenerateAsync(context, operation.Token)) { output.Append(token); rendered.Text = output.ToString(); transcript?.ScrollToEnd(); }
        }
        catch (OperationCanceledException) { state = "cancelled"; status.Text = "Generation stopped."; }
        catch (Exception ex) { state = "error"; status.Text = ex.Message; }
        finally { chats.Add(chatId, "assistant", output.ToString(), label, state); busy = false; operation.Dispose(); operation = null; ShowChat(); }
    }
    private async Task Attach()
    {
        if (busy) return; var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Attach UTF-8 source/text" });
        if (picked.Count == 0) return;
        await using var stream = await picked[0].OpenReadAsync(); if (stream.Length > 128 * 1024) throw new IOException("Attachments are limited to 128 KB.");
        using var reader = new StreamReader(stream); var text = await reader.ReadToEndAsync(); if (text.Contains('\0')) throw new IOException("Choose a text file.");
        prompt.Text += "\n\nAttached reference (untrusted): " + picked[0].Name + "\n" + text;
    }
    private void ShowHistory()
    {
        var panel = Page("Conversations");
        foreach (var chat in chats.List()) panel.Children.Add(Btn(chat.Title, () => { chatId = chat.Id; ShowChat(); }));
    }
    private void ShowModels()
    {
        var panel = Page("Local models"); panel.Children.Add(Label("Runs llama.cpp on this computer. Your prompts stay local. Choose a GGUF and a trusted Linux llama-server."));
        var runtime = new TextBox { Text = chats.GetSetting("linux_runtime", Path.Combine(AppContext.BaseDirectory, "runtime", "llama-server")), PlaceholderText = "Path to llama-server" };
        panel.Children.Add(runtime); panel.Children.Add(Btn("Choose inference runtime", () => Run(async () =>
        { var selection = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose trusted llama-server" }); if (selection.Count > 0) { runtime.Text = selection[0].TryGetLocalPath(); chats.SetSetting("linux_runtime", runtime.Text ?? ""); } })));
        panel.Children.Add(Btn("Set up included starter", () => Run(async () =>
        {
            await models.ImportAsync(Path.Combine(AppContext.BaseDirectory, "development", StarterBackend.FileName), StarterBackend.Name, StarterBackend.License, StarterBackend.Source);
            chats.SetSetting("linux_runtime", runtime.Text ?? ""); ShowModels();
        })));
        var name = new TextBox { PlaceholderText = "Original model name" }; var license = new TextBox { PlaceholderText = "License" }; var source = new TextBox { PlaceholderText = "Source URL" };
        panel.Children.Add(name); panel.Children.Add(license); panel.Children.Add(source);
        panel.Children.Add(Btn("Import GGUF", () => Run(async () =>
        {
            var selection = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Import GGUF", FileTypeFilter = [new FilePickerFileType("GGUF") { Patterns = ["*.gguf"] }] });
            if (selection.Count == 0) return;
            if (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(license.Text)) throw new ArgumentException("Enter the original model name and license.");
            await models.ImportAsync(selection[0].TryGetLocalPath() ?? throw new IOException("Choose a local file."), name.Text, license.Text, source.Text ?? ""); ShowModels();
        })));
        foreach (var model in models.List())
        {
            panel.Children.Add(Label(model.DisplayName + " · " + model.License));
            panel.Children.Add(Btn("Load locally", () => Run(async () =>
            {
                if (busy) return; busy = true; operation = new CancellationTokenSource(); status.Text = "Loading model…";
                try { chats.SetSetting("linux_runtime", runtime.Text ?? ""); await engine.StartAsync(runtime.Text ?? "", model, operation.Token); status.Text = "Loaded " + model.DisplayName; }
                finally { busy = false; operation.Dispose(); operation = null; }
            })));
        }
        panel.Children.Add(Btn("Unload / cancel loading", () => { operation?.Cancel(); engine.Stop(); status.Text = "Model unloaded."; }));
    }
    private async Task SaveCopy(Artifact artifact)
    {
        var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = artifact.Name, ShowOverwritePrompt = true });
        if (destination is null) { status.Text = "Saved in Files."; return; }
        await Hydra.Core.Security.UpdateVerifier.VerifyArtifactAsync(files.PathFor(artifact), artifact.Bytes, artifact.Sha256, default);
        await using var source = File.OpenRead(files.PathFor(artifact)); await using var output = await destination.OpenWriteAsync(); output.SetLength(0); await source.CopyToAsync(output); status.Text = "Saved " + destination.Name;
    }
    private void ShowFiles()
    {
        var panel = Page("Saved files");
        panel.Children.Add(Label("Save code blocks from chat, or import an existing output. Files are never run automatically."));
        panel.Children.Add(Btn("Add file", () => Run(async () =>
        { var chosen = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()); if (chosen.Count == 0) return; var file = await files.ImportAsync(chosen[0].TryGetLocalPath() ?? throw new IOException("Choose a local file."), chatId, "User file"); await Backup(file, false); ShowFiles(); })));
        foreach (var file in files.List())
        {
            panel.Children.Add(Label(file.Name + " · " + file.Bytes.ToString("N0") + " bytes"));
            panel.Children.Add(Btn("Save a copy", () => Run(() => SaveCopy(file))));
            panel.Children.Add(Btn("Back up now", () => Run(() => Backup(file, true))));
        }
    }
    private async Task Backup(Artifact file, bool requested)
    {
        if (!requested && chats.GetSetting("backup_auto") != "true") return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var configuration = new BackupSettings(chats.GetSetting("backup_vault"), chats.GetSetting("backup_git"), chats.GetSetting("linux_git", "/usr/bin/git"), chats.GetSetting("backup_commit") == "true");
        var result = await new BackupService(Path.Combine(root, "backup-jobs")).BackUpAsync(files, file, configuration, timeout.Token);
        status.Text = result.Count == 0 ? "Connect a backup destination in Settings." : string.Join(" ", result);
    }
    private void ShowSettings()
    {
        var panel = Page("Settings"); panel.Children.Add(Label("Obsidian and Git backups", 20));
        foreach (var (key, title) in new[] { ("backup_vault", "Obsidian vault"), ("backup_git", "Git repository") })
        {
            panel.Children.Add(Label(title + ": " + chats.GetSetting(key, "Not connected")));
            panel.Children.Add(Btn("Choose " + title, () => Run(async () =>
            { var chosen = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()); if (chosen.Count > 0) { chats.SetSetting(key, chosen[0].TryGetLocalPath() ?? ""); ShowSettings(); } })));
            panel.Children.Add(Btn("Disconnect " + title, () => { chats.SetSetting(key, ""); ShowSettings(); }));
        }
        foreach (var (key, title) in new[] { ("backup_auto", "Automatically back up saved files"), ("backup_commit", "Create local Git commits") })
        {
            var toggle = new CheckBox { Content = title, IsChecked = chats.GetSetting(key) == "true", MinHeight = 44 };
            toggle.IsCheckedChanged += (_, _) => chats.SetSetting(key, toggle.IsChecked == true ? "true" : "false"); panel.Children.Add(toggle);
        }
        panel.Children.Add(Label("Backups use separate snapshot folders. Git never pushes to a remote. Files and chat history stay on this computer. Hydra AI 0.2.0."));
    }
}
