using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;
using Hydra.Linux;

var output = Path.GetFullPath(args.FirstOrDefault() ?? "linux-smoke"); Directory.CreateDirectory(output);
Environment.SetEnvironmentVariable("HYDRA_DATA_DIR", Path.Combine(output, "data-" + Guid.NewGuid().ToString("N")));
AppBuilder.Configure<HydraApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var window = new MainWindow(); window.Show();
void Click(string label) => window.GetVisualDescendants().OfType<Button>().First(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
foreach (var page in new[] { "New chat", "Chats", "Models", "Files", "Settings" })
{
    Click(page); Dispatcher.UIThread.RunJobs();
    using var frame = window.CaptureRenderedFrame() ?? throw new Exception("No rendered frame: " + page);
    frame.Save(Path.Combine(output, page.Replace(' ', '-') + ".png"), PngBitmapEncoderOptions.Default); Console.WriteLine("PASS Linux client layout: " + page);
}
Click("New chat"); window.Width = 420; window.Height = 700; Dispatcher.UIThread.RunJobs();
using (var frame = window.CaptureRenderedFrame() ?? throw new Exception("No compact frame")) frame.Save(Path.Combine(output, "compact.png"), PngBitmapEncoderOptions.Default);
var prompt = window.GetVisualDescendants().OfType<TextBox>().First(); prompt.Text = "Test missing backend"; Click("↑ / Stop");
if (!window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Load a local model in Models first.")) throw new Exception("Missing backend guard failed.");
Console.WriteLine("PASS compact layout and missing-backend guard"); window.Close();
