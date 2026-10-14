using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hydra.Core.Models;
using Hydra.Core.SystemInfo;

namespace Hydra.Desktop;

public partial class MainWindow
{
    private void ShowProfiles()
    {
        var page = Page("Choose your specialist.", "Six specialist behavior profiles. Independent Hydra weights are in development.");
        foreach (var p in Profiles.All)
        {
            var panel = new StackPanel(); panel.Children.Add(Text("HYDRA " + p.Name.ToUpperInvariant(), 20, p.Accent));
            panel.Children.Add(Text(p.Purpose, 15)); panel.Children.Add(Text(p.Instruction, 13, "#9C9FB3"));
            panel.Children.Add(Text("Modes: " + string.Join(" · ", p.Modes), 12, "#ADA0D1"));
            panel.Children.Add(Text("Weights: developmental / unavailable", 11, "#9B9EB0"));
            panel.Children.Add(Button("Use this profile", () => { selectedProfile = p.Id; currentChat = null; ShowChat(); })); page.Children.Add(Card(panel));
        }
    }
    private void ShowModels()
    {
        var page = Page("Your models. Your machine.", "Register open weights with their original identity. No Hydra-trained weights exist and nothing is automatically downloaded.");
        var bundledModel = Path.Combine(AppContext.BaseDirectory, "development", StarterBackend.FileName);
        var bundledRuntime = Path.Combine(AppContext.BaseDirectory, "runtime", "llama-server.exe");
        if (File.Exists(bundledModel) && File.Exists(bundledRuntime))
        {
            var starter = new StackPanel();
            starter.Children.Add(Text("Included starter backend", 20, "#B09AED"));
            starter.Children.Add(Text(StarterBackend.Name + " · Apache-2.0", 15));
            starter.Children.Add(Text("A small third-party model for trying local inference. Limited quality; these are not Hydra weights. Setup stays entirely offline.", 13, "#9CA0B5"));
            starter.Children.Add(Button("Set up starter backend", () => _ = Guard(async () =>
            {
                if (busy) return;
                busy = true; operation = new CancellationTokenSource();
                Status("Verifying the included upstream model.");
                try
                {
                    await Hydra.Core.Security.UpdateVerifier.VerifyArtifactAsync(bundledModel, new FileInfo(bundledModel).Length, StarterBackend.Sha256, operation.Token);
                    await registry.ImportAsync(bundledModel, StarterBackend.Name, StarterBackend.License, StarterBackend.Source, 4096, operation.Token);
                    chats.SetSetting("runtime", bundledRuntime); Status("Starter backend configured. Click Load locally below, then start a conversation.");
                }
                finally { busy = false; operation.Dispose(); operation = null; ShowModels(); }
            }), true));
            page.Children.Add(Card(starter, "#1C1928"));
        }
        var runtime = new StackPanel();
        runtime.Children.Add(Text("01 / Local inference runtime", 18));
        var path = chats.GetSetting("runtime");
        runtime.Children.Add(Text(string.IsNullOrEmpty(path) ? "Choose a trusted llama.cpp server distribution. Keep its DLLs alongside the executable." : path, 13, "#A0A3B8"));
        runtime.Children.Add(Row(Button("Choose llama-server.exe", () =>
        {
            if (busy) return;
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "llama.cpp server|llama-server.exe", Title = "Select a trusted local inference runtime" };
            if (picker.ShowDialog(this) == true && Confirm("Allow Hydra to run this native inference engine as your Windows user? Only choose a distribution you trust.\n\n" + picker.FileName))
            { chats.SetSetting("runtime", picker.FileName); ShowModels(); }
        }), Button("Stop / unload", () => { operation?.Cancel(); engine.Stop(); EngineStatus.Text = "No model loaded"; Status("Inference process stopped; model unloaded."); })));
        page.Children.Add(Card(runtime));
        var header = new StackPanel(); header.Children.Add(Text("02 / Development backends", 18));
        header.Children.Add(Text("Import records the local path and SHA-256. Removing a registration leaves your source weights intact.", 13, "#9C9FB3"));
        header.Children.Add(Button("＋ Import GGUF", () => _ = Guard(ImportModelAsync), true)); page.Children.Add(Card(header));
        foreach (var model in registry.List())
        {
            var p = new StackPanel(); p.Children.Add(Text(model.DisplayName, 20));
            p.Children.Add(Text($"DEVELOPMENT BACKEND  ·  {model.License}  ·  {model.SizeBytes / 1073741824.0:F2} GiB  ·  {model.ContextLength:N0} context", 11, "#B19CEB"));
            p.Children.Add(Text(model.ModelPath, 12, "#9599AD"));
            p.Children.Add(Text("SHA-256: " + model.Sha256, 10, "#73788C"));
            p.Children.Add(Row(Button("Load locally", () => _ = Guard(async () =>
            {
                if (busy) return;
                busy = true; operation = new CancellationTokenSource();
                Status("Verifying weights and starting the local engine. Large models can take several minutes. Stop / unload cancels.");
                try
                {
                    await engine.StartAsync(chats.GetSetting("runtime"), model, operation.Token);
                    EngineStatus.Text = "Local · " + model.DisplayName;
                    Status("Model loaded. CPU inference is active; no remote intelligence endpoint is used.");
                }
                finally { busy = false; operation.Dispose(); operation = null; }
            }), true), Button("Remove registration", () =>
            {
                if (busy) return;
                if (Confirm($"Remove {model.DisplayName} from Hydra? The GGUF file will be kept."))
                { if (engine.ActiveModel?.ModelId == model.ModelId) { engine.Stop(); EngineStatus.Text = "No model loaded"; } registry.Remove(model.ModelId); ShowModels(); }
            })));
            page.Children.Add(Card(p));
        }
        if (registry.List().Count == 0) page.Children.Add(Text("No development backends installed. Hydra-trained weights are not available yet.", 14, "#9296AC"));
    }
    private async Task ImportModelAsync()
    {
        if (busy) return;
        var picker = new Microsoft.Win32.OpenFileDialog { Filter = "GGUF weights|*.gguf", Title = "Import a local development model" };
        if (picker.ShowDialog(this) != true) return;
        var values = Form("Model provenance", ("Original upstream name (never rename as Hydra)", Path.GetFileNameWithoutExtension(picker.FileName)),
            ("License identifier from the model card", ""), ("Source URL or provenance note", ""), ("Supported context length (tokens)", "4096"));
        if (values is null) return;
        if (!int.TryParse(values[3], out var context)) throw new ArgumentException("Context length must be an integer.");
        busy = true; operation = new CancellationTokenSource(); Status("Hashing and registering the selected GGUF. Stop / unload cancels.");
        try { await registry.ImportAsync(picker.FileName, values[0], values[1], values[2], context, operation.Token); Status("Development backend registered. Choose Load locally to start inference."); }
        finally { busy = false; operation.Dispose(); operation = null; ShowModels(); }
    }
    private void ShowSettings()
    {
        var page = Page("Privacy by default.", "Network features are explicit. Local inference does not require an account.");
        AddBackupSettings(page);
        var privacy = new StackPanel(); privacy.Children.Add(Text("Privacy dashboard", 20));
        privacy.Children.Add(Text("Telemetry                 Not implemented / no collection\nCloud sync               Unavailable / no uploads\nInference                  Loopback only, when you start an engine\nPrivate chat training   Disabled / no training data export", 14, "#A4A7BA"));
        foreach (var item in new[] { ("research_opt_in", "Allow future research web access"), ("downloads_opt_in", "Allow future model downloads") })
        {
            var check = new CheckBox { Content = item.Item2, IsChecked = chats.GetSetting(item.Item1, "false") == "true" };
            check.Click += (_, _) => chats.SetSetting(item.Item1, check.IsChecked == true ? "true" : "false"); privacy.Children.Add(check);
        }
        privacy.Children.Add(Text("These preferences reserve consent for future network adapters. This build does not download models or browse research sites.", 12, "#8B8FA6"));
        page.Children.Add(Card(privacy));
        var hardware = Hardware.Detect(); var info = new StackPanel(); info.Children.Add(Text("This machine", 20));
        info.Children.Add(Text($"{hardware.OS}  /  {hardware.Architecture}\n{hardware.LogicalCores} logical CPU cores  /  {hardware.RamBytes / 1073741824.0:F1} GiB RAM\nCapacity profile: {hardware.Profile}", 14));
        info.Children.Add(Text(hardware.Guidance, 12, "#959AB0")); page.Children.Add(Card(info));
        var storage = new StackPanel(); storage.Children.Add(Text("Local storage", 20)); storage.Children.Add(Text(dataRoot, 12, "#AD9BE0"));
        storage.Children.Add(Text("Conversations and attached excerpts are plaintext on disk. Optional long-term memory and cloud sync are not yet implemented.", 13, "#959AAF"));
        storage.Children.Add(Button("Export redacted diagnostics", () => _ = Guard(async () =>
        {
            var picker = new Microsoft.Win32.SaveFileDialog { FileName = "hydra-diagnostics.json", Filter = "JSON diagnostics|*.json" };
            if (picker.ShowDialog(this) == true) { await File.WriteAllTextAsync(picker.FileName, DiagnosticLog.Export(registry.List().Count)); Status("Diagnostics exported without chats, credentials, file contents or user paths."); }
        })));
        page.Children.Add(Card(storage));
    }
    private void ShowAccount()
    {
        var page = Page("You can stay local.", "There is no account requirement, subscription, message limit, or telemetry.");
        var card = new StackPanel(); card.Children.Add(Text("Local user", 23));
        card.Children.Add(Text("Your conversations and settings belong to this Windows account. You can start using any compatible installed development backend now.", 15, "#9DA1B5"));
        card.Children.Add(Row(Button("Continue locally", ShowChat, true)));
        card.Children.Add(Text("Sign in / Create account: planned. A cloud service is not deployed and no credentials are collected in this release.", 13, "#ADA0CD")); page.Children.Add(Card(card));
    }
    private void ShowResearch()
    {
        var page = Page("Research with a paper trail.", "Veyra's research contracts are ready for provider integration. Live search is not implemented in this release.");
        var p = new StackPanel(); p.Children.Add(Text("Plan → retrieve → compare → cite", 22));
        p.Children.Add(Text("The framework separates search providers, page retrieval, parsing, planning, ranking and citation records. Citations are validated against registered content hashes. This does not itself verify that a claim is true.", 15, "#A3A6BA"));
        p.Children.Add(Text("Quick Search / Research / Deep Research remain planned modes. For now, attach source text and ask Veyra to compare the evidence. No invented sources or demo reports are shown here.", 14, "#989EB2"));
        p.Children.Add(Button("Analyze supplied text with Veyra", () => { selectedProfile = "hydra-veyra"; ShowChat(); }, true)); page.Children.Add(Card(p));
    }
    private void ShowFiles()
    {
        var page = Page("Context, kept close.", "This version supports UTF-8 text and source code, up to 2 MB per file and 10 files per chat. PDF, Office and image parsing are planned.");
        AddOutputLibrary(page);
        page.Children.Add(Text("Conversation attachments", 22));
        page.Children.Add(Button("Attach to current conversation", SelectAttachment, true));
        if (currentChat is null) { page.Children.Add(Text("Start a conversation or attach a file to create one.", 14, "#989CB0")); return; }
        foreach (var file in db.Query("SELECT id,name,length(content) AS size FROM Files WHERE chat_id=?", currentChat))
        {
            var p = new StackPanel(); p.Children.Add(Text(file["name"], 18)); p.Children.Add(Text(file["size"] + " characters stored locally", 12, "#979CB1"));
            p.Children.Add(Button("Remove excerpt", () => { if (Confirm("Remove this stored attachment? The original file stays untouched.")) { db.Execute("DELETE FROM Files WHERE id=?", file["id"]); ShowFiles(); } })); page.Children.Add(Card(p));
        }
    }
    private void ShowBenchmarks()
    {
        var page = Page("Evidence before claims.", "No Hydra model benchmarks have been run. Test coverage and model quality are different things.");
        foreach (var p in Profiles.All)
        {
            var item = new StackPanel(); item.Children.Add(Text("Hydra " + p.Name, 19, p.Accent));
            item.Children.Add(Text("Not evaluated · no trained checkpoint", 14, "#A0A5B8"));
            item.Children.Add(Text("Use hydra-ml/evaluation to create provenance-bearing evaluation reports from real predictions. Automated exact-match scores are not a substitute for specialist review.", 12, "#8D93AA")); page.Children.Add(Card(item));
        }
    }
}
