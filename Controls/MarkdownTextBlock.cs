using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace UnturnedModManager.Controls;

/// <summary>
/// 轻量 Markdown 展示控件，用于社区详情的标题、段落、列表和基础强调文本。
/// 它保持 WPF 原生文本布局，不引入浏览器控件，也不会执行远程 HTML。
/// </summary>
public sealed class MarkdownTextBlock : TextBlock
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown),
        typeof(string),
        typeof(MarkdownTextBlock),
        new FrameworkPropertyMetadata("", OnMarkdownChanged));

    private static readonly Regex EmphasisPattern = new(@"(\*\*[^*]+\*\*|`[^`]+`)", RegexOptions.Compiled);

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject dependency, DependencyPropertyChangedEventArgs args)
    {
        if (dependency is MarkdownTextBlock block)
            block.RenderMarkdown(args.NewValue as string ?? "");
    }

    private void RenderMarkdown(string markdown)
    {
        Inlines.Clear();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd();
            var trimmed = line.TrimStart();
            var headingLevel = trimmed.TakeWhile(character => character == '#').Count();

            if (headingLevel > 0 && trimmed.Length > headingLevel && trimmed[headingLevel] == ' ')
                AddInlineRuns(trimmed[(headingLevel + 1)..], bold: true);
            else if (trimmed.StartsWith("- ", StringComparison.Ordinal)
                     || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                Inlines.Add(new Run("• ") { FontWeight = FontWeights.SemiBold });
                AddInlineRuns(trimmed[2..], bold: false);
            }
            else
                AddInlineRuns(line, bold: false);

            if (index < lines.Length - 1)
                Inlines.Add(new LineBreak());
        }
    }

    private void AddInlineRuns(string text, bool bold)
    {
        if (bold)
        {
            Inlines.Add(new Run(text) { FontWeight = FontWeights.SemiBold });
            return;
        }

        var start = 0;
        foreach (Match match in EmphasisPattern.Matches(text))
        {
            if (match.Index > start)
                Inlines.Add(new Run(text[start..match.Index]));

            var value = match.Value;
            if (value.StartsWith("**", StringComparison.Ordinal))
                Inlines.Add(new Run(value[2..^2]) { FontWeight = FontWeights.SemiBold });
            else
                Inlines.Add(new Run(value[1..^1]) { FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono") });
            start = match.Index + match.Length;
        }

        if (start < text.Length)
            Inlines.Add(new Run(text[start..]));
        if (text.Length == 0)
            Inlines.Add(new Run(""));
    }
}
