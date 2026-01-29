using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows;

namespace LLP.UI.Services;

public static class TextBlockExtensions
{
    public static readonly DependencyProperty HighlightTextProperty =
        DependencyProperty.RegisterAttached("HighlightText", typeof(string), typeof(TextBlockExtensions), new PropertyMetadata(null, OnHighlightTextChanged));

    public static string GetHighlightText(TextBlock target) => (string)target.GetValue(HighlightTextProperty);
    public static void SetHighlightText(TextBlock target, string value) => target.SetValue(HighlightTextProperty, value);

    private static void OnHighlightTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock textBlock)
        {
            UpdateHighlighting(textBlock);
        }
    }

    public static readonly DependencyProperty OriginalTextProperty =
        DependencyProperty.RegisterAttached("OriginalText", typeof(string), typeof(TextBlockExtensions), new PropertyMetadata(null, OnOriginalTextChanged));

    public static string GetOriginalText(TextBlock target) => (string)target.GetValue(OriginalTextProperty);
    public static void SetOriginalText(TextBlock target, string value) => target.SetValue(OriginalTextProperty, value);

    private static void OnOriginalTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock textBlock)
        {
            UpdateHighlighting(textBlock);
        }
    }

    private static void UpdateHighlighting(TextBlock textBlock)
    {
        string text = GetOriginalText(textBlock);
        string highlight = GetHighlightText(textBlock);

        textBlock.Inlines.Clear();

        if (string.IsNullOrEmpty(text)) return;
        if (string.IsNullOrEmpty(highlight))
        {
            textBlock.Inlines.Add(new Run(text));
            return;
        }

        int index = text.IndexOf(highlight, StringComparison.OrdinalIgnoreCase);
        int lastIndex = 0;

        while (index != -1)
        {
            textBlock.Inlines.Add(new Run(text.Substring(lastIndex, index - lastIndex)));
            textBlock.Inlines.Add(new Run(text.Substring(index, highlight.Length))
            {
                Background = Brushes.Yellow,
                FontWeight = FontWeights.Bold
            });

            lastIndex = index + highlight.Length;
            index = text.IndexOf(highlight, lastIndex, StringComparison.OrdinalIgnoreCase);
        }

        textBlock.Inlines.Add(new Run(text.Substring(lastIndex)));
    }
}
