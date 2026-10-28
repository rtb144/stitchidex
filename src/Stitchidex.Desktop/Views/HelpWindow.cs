// SPDX-License-Identifier: MIT
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Stitchidex.Core.Help;
namespace Stitchidex.Desktop.Views;

/// <summary>Offline Help &amp; Wiki Viewer: topic sidebar, instant keyword filter, formatted rendering. No network.</summary>
public sealed class HelpWindow : Window
{
    readonly ListBox _topics = new() { Background = Brushes.Transparent };
    readonly TextBox _search = new() { Watermark = "Search help (e.g. Dropbox, .EMB, Send to USB)…" };
    readonly StackPanel _body = new() { Spacing = 8, Margin = new Thickness(24, 16), MaxWidth = 860, HorizontalAlignment = HorizontalAlignment.Left };
    readonly TextBlock _count = new() { Opacity = 0.7, FontSize = 12 };
    readonly ScrollViewer _scroll;

    public HelpWindow(string? topicId = null)
    {
        Title = "Stitchidex Help & User Guide (Offline)";
        Width = 1100; Height = 760; MinWidth = 640; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _topics.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<HelpTopic>((t, _) =>
            new TextBlock { Text = t?.Title, TextWrapping = TextWrapping.Wrap, [ToolTip.TipProperty] = t?.Title });
        _topics.SelectionChanged += (_, _) => Show(_topics.SelectedItem as HelpTopic);
        _search.TextChanged += (_, _) => Filter();
        _scroll = new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var side = new DockPanel { Margin = new Thickness(10), Width = 300 };
        var top = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 8) };
        top.Children.Add(new TextBlock { Text = "📖 Help Topics", FontWeight = FontWeight.Bold, FontSize = 16 });
        top.Children.Add(_search); top.Children.Add(_count);
        DockPanel.SetDock(top, Dock.Top); side.Children.Add(top); side.Children.Add(_topics);
        var root = new DockPanel();
        DockPanel.SetDock(side, Dock.Left); root.Children.Add(side);
        root.Children.Add(new Border { BorderThickness = new Thickness(1, 0, 0, 0), BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)), Child = _scroll });
        Content = root;
        Filter();
        if (topicId != null && OfflineHelpCatalog.Get(topicId) is { } t) _topics.SelectedItem = t;
    }

    void Filter()
    {
        var hits = OfflineHelpCatalog.Search(_search.Text);
        var cur = _topics.SelectedItem as HelpTopic;
        _topics.ItemsSource = hits;
        _count.Text = string.IsNullOrWhiteSpace(_search.Text) ? $"{hits.Count} topics • works offline" : $"{hits.Count} matching topic(s)";
        _topics.SelectedItem = cur != null && hits.Contains(cur) ? cur : hits.FirstOrDefault();
        if (_topics.SelectedItem == null) Show(null);
    }

    void Show(HelpTopic? t)
    {
        _body.Children.Clear();
        if (t == null) { _body.Children.Add(new TextBlock { Text = "No help topics match your search.", Opacity = 0.7 }); return; }
        var q = _search.Text?.Trim();
        foreach (var b in t.Blocks)
        {
            Control c = b.Kind switch
            {
                HelpBlockKind.Heading1 => new TextBlock { Text = b.Text, FontSize = 24, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) },
                HelpBlockKind.Heading2 => new TextBlock { Text = b.Text, FontSize = 19, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 2) },
                HelpBlockKind.Heading3 => new TextBlock { Text = b.Text, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) },
                HelpBlockKind.Bullet => new TextBlock { Text = "•  " + b.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(14, 0, 0, 0), FontSize = 14 },
                HelpBlockKind.Numbered => new TextBlock { Text = b.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(14, 0, 0, 0), FontSize = 14 },
                HelpBlockKind.Code => new Border { Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), CornerRadius = new CornerRadius(6), Padding = new Thickness(10),
                    Child = new SelectableTextBlock { Text = b.Text, FontFamily = new FontFamily("Consolas,monospace"), TextWrapping = TextWrapping.Wrap } },
                _ => new SelectableTextBlock { Text = b.Text, TextWrapping = TextWrapping.Wrap, FontSize = 14 },
            };
            if (!string.IsNullOrEmpty(q) && b.Text.Contains(q, StringComparison.OrdinalIgnoreCase) && c is TextBlock tb)
                tb.Background = new SolidColorBrush(Color.FromArgb(50, 250, 204, 21)); // highlight matches
            _body.Children.Add(c);
        }
        _scroll.Offset = default;
    }
}
