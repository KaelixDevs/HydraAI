using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Hydra.Desktop;

/// <summary>Safe basic Markdown renderer; no HTML, remote images, or active hyperlinks.</summary>
internal static class Markdown
{
    public static FlowDocument Render(string text)
    {
        var document = new FlowDocument { PagePadding = new Thickness(0), FontFamily = new FontFamily("Segoe UI"), FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(225, 225, 237)), LineHeight = 23 };
        bool code = false;
        foreach (var line in text.Replace("\r", "").Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal)) { code = !code; continue; }
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, code ? 0 : 7) };
            if (code)
            {
                paragraph.FontFamily = new FontFamily("Cascadia Code,Consolas");
                paragraph.Background = new SolidColorBrush(Color.FromRgb(20, 21, 29));
                paragraph.Padding = new Thickness(9, 0, 9, 0); paragraph.Inlines.Add(new Run(line));
            }
            else
            {
                var content = line;
                if (line.StartsWith("#", StringComparison.Ordinal))
                { paragraph.FontSize = line.StartsWith("##", StringComparison.Ordinal) ? 18 : 22; paragraph.FontWeight = FontWeights.SemiBold; content = line.TrimStart('#', ' '); }
                else if (line.StartsWith("- ", StringComparison.Ordinal)) content = "•  " + line[2..];
                var parts = content.Split("**");
                for (var i = 0; i < parts.Length; i++) paragraph.Inlines.Add(i % 2 == 1 ? new Bold(new Run(parts[i])) : new Run(parts[i]));
            }
            document.Blocks.Add(paragraph);
        }
        return document;
    }
}
