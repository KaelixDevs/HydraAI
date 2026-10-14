using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Hydra.Desktop;

public partial class MainWindow
{
    internal async Task SmokeAsync(string[] args)
    {
        var outputIndex = Array.IndexOf(args, "--output");
        var output = outputIndex >= 0 ? Path.GetFullPath(args[outputIndex + 1]) : Path.Combine(dataRoot, "smoke");
        Directory.CreateDirectory(output);
        var report = new List<string>();
        try
        {
            await Task.Delay(200);
            foreach (var page in new[] { "Welcome", "Models", "Model Manager", "Projects", "Research", "Files", "Settings", "Account", "Benchmarks", "Chats" })
            {
                if (page == "Welcome") ShowChat(); else Navigate(page);
                UpdateLayout(); await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(this);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, page.Replace(" ", "-").ToLowerInvariant() + ".png")); encoder.Save(file);
                report.Add("PASS layout/render: " + page);
            }
            ShowChat(); composer!.Text = "Can you help me?"; await SendAsync();
            if (!StatusText.Text.StartsWith("No model loaded", StringComparison.Ordinal)) throw new InvalidOperationException("Missing-backend guard failed.");
            report.Add("PASS missing-backend generation guard");
            UpdateLayout();
            if (composerBar!.ActualHeight > 90) throw new InvalidOperationException("Empty composer is not compact.");
            OpenComposerMenu(); await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            if (composerMenu!.Items.Count < 5) throw new InvalidOperationException("Composer options missing.");
            var profiles = (System.Windows.Controls.MenuItem)composerMenu.Items[0];
            if (profiles.Items.Count != 7) throw new InvalidOperationException("Profile menu is incomplete.");
            ((System.Windows.Controls.MenuItem)profiles.Items[1]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            if (selectedProfile != "hydra-kairo") throw new InvalidOperationException("Menu profile selection failed.");
            var menuImage = new RenderTargetBitmap((int)composerMenu.ActualWidth, (int)composerMenu.ActualHeight, 96, 96, PixelFormats.Pbgra32); menuImage.Render(composerMenu);
            var menuPng = new PngBitmapEncoder(); menuPng.Frames.Add(BitmapFrame.Create(menuImage));
            using (var menuFile = File.Create(Path.Combine(output, "composer-menu.png"))) menuPng.Save(menuFile);
            composerMenu.IsOpen = false; selectedProfile = "auto"; UpdateComposerHint();
            report.Add("PASS compact composer, options menu and profile selection");
            var fixture = await artifacts.SaveTextAsync(new Hydra.Core.Artifacts.GeneratedFile("smoke.html", "<h1>UI test fixture</h1>", "html"), null, "UI test fixture");
            if (!File.Exists(artifacts.PathFor(fixture))) throw new InvalidOperationException("File output was not persisted.");
            ShowFiles(); UpdateLayout(); report.Add("PASS generated files library");
            if (args.Contains("--with-starter"))
            {
                var model = await registry.ImportAsync(Path.Combine(AppContext.BaseDirectory, "development", Hydra.Core.Models.StarterBackend.FileName), Hydra.Core.Models.StarterBackend.Name,
                    Hydra.Core.Models.StarterBackend.License, Hydra.Core.Models.StarterBackend.Source);
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                await engine.StartAsync(Path.Combine(AppContext.BaseDirectory, "runtime", "llama-server.exe"), model, timeout.Token);
                EngineStatus.Text = "Local · " + model.DisplayName;
                ShowChat(); composer!.Text = "Describe a quiet forest in one short sentence.";
                await SendAsync();
                var messages = chats.Messages(currentChat!);
                if (messages.Count != 2 || messages[1].State != "complete" || string.IsNullOrWhiteSpace(messages[1].Content)) throw new InvalidOperationException("Real UI streaming/persistence test failed.");
                if (!messages[1].Backend.Contains(model.DisplayName, StringComparison.Ordinal)) throw new InvalidOperationException("Backend identity missing from persisted response.");
                report.Add("PASS real UI generation, upstream attribution, and persistence");
                UpdateLayout();
                var shot = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); shot.Render(this);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(shot));
                using (var file = File.Create(Path.Combine(output, "real-inference.png"))) png.Save(file);
                engine.Stop();
            }
            File.WriteAllLines(Path.Combine(output, "ui-smoke.txt"), report);
            Application.Current.Shutdown(0);
        }
        catch (Exception ex)
        { File.WriteAllText(Path.Combine(output, "ui-smoke-failure.txt"), ex.ToString()); Application.Current.Shutdown(1); }
    }
}
