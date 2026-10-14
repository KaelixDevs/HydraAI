using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hydra.Core.Inference;
using Hydra.Core.Models;
using Hydra.Core.Security;
using Hydra.Core.Storage;
using Hydra.Core.Artifacts;

namespace Hydra.Desktop;

public partial class MainWindow : Window
{
    private readonly Database db;
    private readonly ChatStore chats;
    private readonly ModelRegistry registry;
    private readonly LlamaBackend engine = new();
    private readonly string dataRoot;
    private readonly ArtifactStore artifacts;
    private readonly FileBuilders builders;
    private string? currentChat;
    private string selectedProfile = "auto";
    private CancellationTokenSource? operation;
    private Workspace? workspace;
    private WorkspaceChange? lastChange;
    private bool busy;

    public MainWindow()
    {
        InitializeComponent();
        dataRoot = Environment.GetEnvironmentVariable("HYDRA_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HydraAI");
        db = new Database(Path.Combine(dataRoot, "hydra.db"));
        chats = new ChatStore(db); registry = new ModelRegistry(db);
        artifacts = new ArtifactStore(Path.Combine(dataRoot, "artifacts"));
        builders = new FileBuilders(Path.Combine(dataRoot, "build-jobs"));
        ShowChat();
        Closing += (_, e) =>
        {
            if (!busy) return;
            e.Cancel = true; operation?.Cancel(); engine.Stop();
            Status("Stopping the active operation. Close Hydra again once it finishes.");
        };
        Closed += (_, _) => { operation?.Cancel(); engine.Stop(); db.Dispose(); };
    }
    private void NavigateClick(object sender, RoutedEventArgs e)
    {
        if (busy) { Status("Stop the active operation before changing pages."); return; }
        Navigate((string)((Button)sender).Tag);
    }
    private void Navigate(string page)
    {
        PageTitle.Text = page;
        switch (page)
        {
            case "Chats": ShowHistory(); break;
            case "Models": ShowProfiles(); break;
            case "Model Manager": ShowModels(); break;
            case "Projects": ShowProjects(); break;
            case "Research": ShowResearch(); break;
            case "Files": ShowFiles(); break;
            case "Settings": ShowSettings(); break;
            case "Account": ShowAccount(); break;
            case "Benchmarks": ShowBenchmarks(); break;
        }
    }
    private void NewChatClick(object sender, RoutedEventArgs e)
    {
        if (busy) { Status("Stop generation before starting another conversation."); return; }
        currentChat = null; ShowChat();
    }
    private void Status(string message) => StatusText.Text = message;
    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static TextBlock Text(string value, double size = 14, string color = "#E9E9F2") => new() { Text = value, FontSize = size, Foreground = Brush(color), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
    private static Button Button(string label, Action action, bool primary = false)
    {
        var b = new Button { Content = label };
        if (primary) { b.Background = Brush("#7961CF"); b.BorderBrush = Brush("#9D82EB"); }
        b.Click += (_, _) => action(); return b;
    }
    private static Border Card(UIElement child, string background = "#1A1B24") => new()
    { Child = child, Background = Brush(background), BorderBrush = Brush("#30313D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(22), Margin = new Thickness(0, 0, 0, 14) };
    private StackPanel Page(string title, string subtitle)
    {
        var panel = new StackPanel { Margin = new Thickness(36, 30, 36, 30), MaxWidth = 1100 };
        panel.Children.Add(Text(title, 30)); panel.Children.Add(Text(subtitle, 14, "#9699AD"));
        PageHost.Content = new ScrollViewer { Content = panel }; return panel;
    }
    private static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 12) };
        foreach (var child in children) row.Children.Add(child); return row;
    }
    private async Task Guard(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { Status("Operation stopped."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or System.Net.Http.HttpRequestException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        { Status(ex.Message); }
    }
    private bool Confirm(string text) => MessageBox.Show(this, text, "Hydra · permission", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    private string[]? Form(string title, params (string Label, string Value)[] fields)
    {
        var dialog = new Window { Title = title, Width = 530, SizeToContent = SizeToContent.Height, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        var panel = new StackPanel { Margin = new Thickness(26) };
        panel.Children.Add(Text(title, 22));
        var inputs = new List<TextBox>();
        foreach (var field in fields)
        {
            panel.Children.Add(Text(field.Label, 12, "#A5A7BB"));
            var input = new TextBox { Text = field.Value, Margin = new Thickness(0, 0, 0, 16) };
            inputs.Add(input); panel.Children.Add(input);
        }
        panel.Children.Add(Row(Button("Save", () => dialog.DialogResult = true, true), Button("Cancel", () => dialog.DialogResult = false)));
        dialog.Content = panel;
        return dialog.ShowDialog() == true ? inputs.Select(i => i.Text).ToArray() : null;
    }
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (busy || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            _ = Guard(async () => { foreach (var path in paths.Take(10)) await AttachAsync(path); ShowChat(); });
    }
}
