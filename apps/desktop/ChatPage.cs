using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hydra.Core.Inference;
using Hydra.Core.Models;
using Hydra.Core.Retrieval;

namespace Hydra.Desktop;

public partial class MainWindow
{
    private TextBox? composer;
    private StackPanel? transcript;
    private ScrollViewer? transcriptScroll;
    private Button? sendButton;

    private void ShowChat()
    {
        PageTitle.Text = currentChat is null ? "Workspace" : "Conversation";
        var layout = new DockPanel { Margin = new Thickness(34, 24, 34, 18), LastChildFill = true };
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom);
        footer.Children.Add(CreateComposer());
        layout.Children.Add(footer);
        transcript = new StackPanel { Margin = new Thickness(0, 0, 8, 16) };
        transcriptScroll = new ScrollViewer { Content = transcript };
        layout.Children.Add(transcriptScroll); PageHost.Content = layout;
        if (currentChat is null || chats.Messages(currentChat).Count == 0) ShowWelcome();
        else
        {
            foreach (var message in chats.Messages(currentChat)) AddMessage(message.Role, message.Content, message.Backend, message.State);
        }
        if (currentChat is not null)
        {
            var files = db.Query("SELECT name FROM Files WHERE chat_id=?", currentChat);
            if (files.Count > 0) footer.Children.Add(Text("Attached: " + string.Join(", ", files.Select(f => f["name"])), 11, "#AD9DE3"));
        }
    }
    private void ShowWelcome()
    {
        if (transcript is null) return;
        transcript.Children.Add(Text("YOUR LOCAL INTELLIGENCE", 10, "#AC92F1"));
        transcript.Children.Add(Text("A place for every idea.", 38));
        transcript.Children.Add(Text("Six focused minds. One private workspace.\nChoose a direction and make something worth keeping.", 16, "#9B9EB2"));
        var cards = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 20, 0, 10) };
        foreach (var p in Profiles.All)
        {
            var content = new StackPanel();
            content.Children.Add(Text(p.Name.ToUpperInvariant(), 12, p.Accent));
            content.Children.Add(Text(p.Purpose, 16));
            content.Children.Add(Text(p.Id == "hydra-nyra" ? "Mesh export assistance" : "Development profile", 10, "#868A9F"));
            var b = Button("", () => { if (!busy) { selectedProfile = p.Id; UpdateComposerHint(); composer!.Focus(); } });
            b.Content = content; b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Margin = new Thickness(0, 0, 10, 10); b.Padding = new Thickness(18); cards.Children.Add(b);
        }
        transcript.Children.Add(cards);
        var notice = new StackPanel();
        notice.Children.Add(Text(engine.IsRunning ? "Your local engine is ready." : "Bring your own open weights.", 15));
        notice.Children.Add(Text("Hydra weights are still in development. Connect a compatible local GGUF model to begin. Its original name is always visible.", 12, "#9699AF"));
        notice.Children.Add(Row(Button("Open Model Manager  →", () => Navigate("Model Manager"))));
        transcript.Children.Add(Card(notice, "#1C1928"));
    }
    private void AddMessage(string role, string content, string backend = "", string state = "complete")
    {
        var panel = new StackPanel();
        panel.Children.Add(Text(role == "user" ? "YOU" : $"HYDRA · {backend}" + (state == "complete" ? "" : $" · {state}"), 10, role == "user" ? "#A3A7B9" : "#AE98EE"));
        panel.Children.Add(new FlowDocumentScrollViewer { Document = Markdown.Render(content), IsToolBarVisible = false, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = true });
        panel.Children.Add(Row(Button("Copy", () => Clipboard.SetText(content)), Button("Edit as new message", () => { if (!busy) composer!.Text = content; })));
        if (role == "assistant") AddFileActions(panel, content, backend);
        transcript!.Children.Add(Card(panel, role == "user" ? "#1D1E29" : "#16171F"));
    }
    private async Task SendAsync()
    {
        if (busy || composer is null || string.IsNullOrWhiteSpace(composer.Text)) return;
        if (!engine.IsRunning || engine.ActiveModel is null) { Status("No model loaded. Open Model Manager, import a GGUF, and start its local engine."); return; }
        var input = composer.Text.Trim();
        var profile = selectedProfile == "auto" ? Profiles.Route(input) : Profiles.Get(selectedProfile);
        var model = engine.ActiveModel;
        var system = Profiles.SystemPrompt(profile, model);
        var messages = currentChat is null ? [] : chats.Messages(currentChat).Where(m => m.State == "complete" && m.Content.Length > 0).Select(m => new InferenceMessage(m.Role, m.Content)).ToList();
        var context = "";
        if (currentChat is not null)
        {
            var chunks = db.Query("SELECT name,content FROM Files WHERE chat_id=?", currentChat).SelectMany(f => TextRetrieval.Chunk(f["name"], f["content"]));
            context = TextRetrieval.Format(TextRetrieval.Search(chunks, input, 1));
        }
        var inferenceInput = input + (context.Length == 0 ? "" : "\n\n" + context);
        messages.Add(new("user", inferenceInput));
        var bounded = ContextBuilder.Build(system, messages, model.ContextLength);
        currentChat ??= chats.Create(input[..Math.Min(input.Length, 60)], selectedProfile);
        var chatId = currentChat;
        chats.Add(chatId, "user", input);
        composer.Clear(); ShowChat();
        var generationLabel = $"{profile.Name} profile / {model.DisplayName}";
        var live = Text("", 14); var livePanel = new StackPanel();
        livePanel.Children.Add(Text($"HYDRA {profile.Name.ToUpperInvariant()} · {model.DisplayName} · generating", 10, profile.Accent));
        livePanel.Children.Add(live); transcript!.Children.Add(Card(livePanel));
        busy = true; sendButton!.Content = "■"; sendButton.ToolTip = "Stop generation";
        operation = new CancellationTokenSource(); var output = new StringBuilder(); var state = "complete";
        Status($"{(selectedProfile == "auto" ? "Auto selected " : "Using ")}{profile.Name}. Generating locally with {model.DisplayName}.");
        try
        {
            var lastRender = DateTime.UtcNow;
            await foreach (var token in engine.GenerateAsync(bounded, operation.Token))
            {
                output.Append(token);
                if ((DateTime.UtcNow - lastRender).TotalMilliseconds > 60)
                { live.Text = output.ToString(); transcriptScroll!.ScrollToEnd(); lastRender = DateTime.UtcNow; }
            }
            Status("Response saved locally.");
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException or System.Net.Http.HttpRequestException or System.Text.Json.JsonException)
        { state = operation.IsCancellationRequested ? "stopped" : "failed"; Status(state == "stopped" ? "Generation stopped. Partial response saved." : "Generation failed: " + ex.Message); }
        finally
        {
            if (output.Length == 0 && state == "complete") state = "empty";
            chats.Add(chatId, "assistant", output.ToString(), generationLabel, state);
            busy = false; operation.Dispose(); operation = null;
            EngineStatus.Text = engine.IsRunning ? $"Local · {model.DisplayName}" : "No model loaded";
            ShowChat(); transcriptScroll!.ScrollToEnd();
        }
    }
    private async Task RegenerateAsync()
    {
        if (busy || currentChat is null) return;
        var last = chats.Messages(currentChat).LastOrDefault(m => m.Role == "user");
        if (last is null) return;
        currentChat = chats.Branch(currentChat);
        // Keep the original branch intact and trim the copied final exchange before resending.
        var copied = chats.Messages(currentChat); var index = copied.ToList().FindLastIndex(m => m.Role == "user");
        foreach (var m in copied.Skip(index)) db.Execute("DELETE FROM Messages WHERE id=?", m.Id);
        ShowChat(); composer!.Text = last.Content; await SendAsync();
    }
    private void SelectAttachment()
    {
        if (busy) return;
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Attach text or source files", Multiselect = true };
        if (picker.ShowDialog(this) == true) _ = Guard(async () => { foreach (var file in picker.FileNames.Take(10)) await AttachAsync(file); ShowChat(); });
    }
    private async Task AttachAsync(string path)
    {
        var content = await TextRetrieval.ReadTextAsync(path);
        currentChat ??= chats.Create("Conversation with attachments", selectedProfile);
        var count = db.Query("SELECT COUNT(*) AS n FROM Files WHERE chat_id=?", currentChat)[0]["n"];
        if (int.Parse(count) >= 10) throw new IOException("This version supports up to 10 text attachments per conversation.");
        db.Execute("INSERT INTO Files VALUES(?,?,?,?,?)", Guid.NewGuid().ToString("N"), currentChat, Path.GetFileName(path), content, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
        Status($"Attached {Path.GetFileName(path)} locally. Relevant text excerpts can be included in generation.");
    }
    private void ShowHistory()
    {
        var panel = Page("Your conversations", "Saved on this device. Search titles, pin important work, or branch an idea.");
        var search = new TextBox { ToolTip = "Search conversation titles", Margin = new Thickness(0, 12, 0, 18) };
        panel.Children.Add(search); var list = new StackPanel(); panel.Children.Add(list);
        void Refresh()
        {
            list.Children.Clear();
            foreach (var chat in chats.List(search.Text))
            {
                var item = new StackPanel(); item.Children.Add(Text((chat.Pinned ? "◆  " : "") + chat.Title, 17));
                item.Children.Add(Row(Button("Open", () => { currentChat = chat.Id; selectedProfile = chat.Profile; ShowChat(); }),
                    Button(chat.Pinned ? "Unpin" : "Pin", () => { chats.Pin(chat.Id, !chat.Pinned); Refresh(); }),
                    Button("Rename", () => { var v = Form("Rename conversation", ("Title", chat.Title)); if (v is not null && !string.IsNullOrWhiteSpace(v[0])) { chats.Rename(chat.Id, v[0]); Refresh(); } }),
                    Button("Delete", () => { if (Confirm("Delete this conversation and its attached excerpts?")) { chats.Delete(chat.Id); if (currentChat == chat.Id) currentChat = null; Refresh(); } })));
                list.Children.Add(Card(item));
            }
            if (list.Children.Count == 0) list.Children.Add(Text("No conversations yet. Your next idea starts with New conversation.", 15, "#9498AC"));
        }
        search.TextChanged += (_, _) => Refresh(); Refresh();
    }
}
