using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Hydra.Core.Models;

namespace Hydra.Desktop;

public partial class MainWindow
{
    private Button? optionsButton;
    private TextBlock? composerHint;
    private Border? composerBar;
    private ContextMenu? composerMenu;

    private UIElement CreateComposer()
    {
        var panel = new StackPanel();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        optionsButton = Button("⋯", OpenComposerMenu);
        optionsButton.FontSize = 24; optionsButton.Padding = new Thickness(10, 2, 10, 6);
        optionsButton.Margin = new Thickness(0, 0, 8, 0); optionsButton.VerticalAlignment = VerticalAlignment.Bottom;
        optionsButton.ToolTip = "Model, attachments and conversation options";
        System.Windows.Automation.AutomationProperties.SetName(optionsButton, "Conversation options");
        grid.Children.Add(optionsButton);
        var inputArea = new Grid(); Grid.SetColumn(inputArea, 1);
        composer = new TextBox { MinHeight = 38, MaxHeight = 150, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0), Background = Brush("#1A1B24"), Padding = new Thickness(6, 8, 6, 8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, ToolTip = "Enter to send · Shift+Enter for a new line" };
        var placeholder = Text("Message Hydra…", 14, "#878A9E"); placeholder.Margin = new Thickness(8, 8, 0, 0); placeholder.IsHitTestVisible = false;
        inputArea.Children.Add(composer); inputArea.Children.Add(placeholder); grid.Children.Add(inputArea);
        composer.TextChanged += (_, _) => placeholder.Visibility = composer.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        composer.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            { e.Handled = true; _ = Guard(SendAsync); }
        };
        sendButton = Button(busy ? "■" : "↑", () =>
        {
            if (busy) { operation?.Cancel(); engine.Stop(); EngineStatus.Text = "Engine stopped"; }
            else _ = Guard(SendAsync);
        }, true);
        sendButton.ToolTip = busy ? "Stop generation" : "Send message";
        sendButton.FontSize = 19; sendButton.Padding = new Thickness(14, 6, 14, 6);
        sendButton.Margin = new Thickness(8, 0, 0, 0); sendButton.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(sendButton, 2); grid.Children.Add(sendButton);
        composerBar = new Border { Child = grid, Background = Brush("#1A1B24"), BorderBrush = Brush("#383545"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(10), Margin = new Thickness(0, 8, 0, 9) };
        panel.Children.Add(composerBar);
        composerHint = Text("", 10, "#85889E"); UpdateComposerHint(); panel.Children.Add(composerHint);
        return panel;
    }
    private void UpdateComposerHint()
    {
        if (composerHint is not null) composerHint.Text = (selectedProfile == "auto" ? "Hydra Auto" : "Hydra " + Profiles.Get(selectedProfile).Name)
            + " · " + (engine.ActiveModel?.DisplayName ?? "No backend loaded") + " · Shift+Enter for a new line";
    }
    private void OpenComposerMenu()
    {
        var menu = new ContextMenu { Background = Brush("#22232E"), Foreground = Brush("#E9E9F2"), BorderBrush = Brush("#474052"),
            PlacementTarget = optionsButton, Placement = PlacementMode.Top, MinWidth = 260 };
        composerMenu = menu;
        void Add(string text, Action action, bool enabled = true)
        {
            var item = new MenuItem { Header = text, IsEnabled = enabled }; item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        var models = new MenuItem { Header = "Change profile", IsEnabled = !busy };
        foreach (var (id, name) in new[] { ("auto", "Hydra Auto") }.Concat(Profiles.All.Select(p => (p.Id, "Hydra " + p.Name))))
        {
            var item = new MenuItem { Header = name, IsCheckable = true, IsChecked = selectedProfile == id };
            item.Click += (_, _) => { selectedProfile = id; UpdateComposerHint(); }; models.Items.Add(item);
        }
        menu.Items.Add(models);
        Add("Change backend / Model Manager", () => Navigate("Model Manager"), !busy);
        menu.Items.Add(new Separator());
        Add("Attach text or source files…", SelectAttachment, !busy);
        Add("Files & exports", () => Navigate("Files"), !busy);
        Add("Backup & export settings", () => Navigate("Settings"), !busy);
        if (currentChat is not null)
        {
            menu.Items.Add(new Separator());
            Add("Branch conversation", () => { currentChat = chats.Branch(currentChat); ShowChat(); }, !busy);
            Add("Continue response", () => { composer!.Text = "Continue your previous response."; _ = Guard(SendAsync); }, !busy);
            Add("Regenerate into a branch", () => _ = Guard(RegenerateAsync), !busy);
        }
        menu.IsOpen = true;
    }
}
