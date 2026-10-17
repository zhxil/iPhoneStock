using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Stock.WinUI;

public sealed partial class MainWindow
{
    private sealed record LogEntry(DateTime At, string Category, string Title, string Detail)
    {
        public string PlainText => $"{At:yyyy-MM-dd HH:mm:ss} [{Category}] {Title}" + (Detail.Length == 0 ? "" : "\n" + Detail);
    }
    private readonly List<LogEntry> logEntries = [];
    private readonly ListView logList = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly ComboBox logFilter = new() { Width = 112, ItemsSource = new[] { "全部类型", "查询", "异常", "提醒", "系统" }, SelectedIndex = 0 };
    private readonly TextBox logSearch = new() { PlaceholderText = "搜索机型、门店或内容", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock logCount = Text("", 12);
    private readonly TextBlock logEmpty = Text("暂无运行记录", 14);
    private IEnumerable<LogEntry> VisibleLogs() => logEntries.AsEnumerable().Reverse().Where(e =>
        (logFilter.SelectedIndex <= 0 || e.Category == logFilter.SelectedItem as string) &&
        (string.IsNullOrWhiteSpace(logSearch.Text) || (e.Title + e.Detail + e.Category).Contains(logSearch.Text.Trim(), StringComparison.OrdinalIgnoreCase)));

    internal FrameworkElement BuildLogsPage()
    {
        var root = new Grid { Margin = new Thickness(24, 12, 24, 24), RowSpacing = 12,
            RowDefinitions = { new() { Height = GridLength.Auto }, new() { Height = GridLength.Auto }, new() { Height = new GridLength(1, GridUnitType.Star) }, new() { Height = GridLength.Auto } } };
        var tools = new Grid { ColumnSpacing = 10, ColumnDefinitions = { new() { Width = GridLength.Auto }, new() { Width = new GridLength(1, GridUnitType.Star) } } };
        AutomationProperties.SetName(logFilter, "记录类型"); AutomationProperties.SetAutomationId(logFilter, "LogFilter");
        AutomationProperties.SetName(logSearch, "搜索运行记录"); AutomationProperties.SetAutomationId(logSearch, "LogSearch");
        tools.Children.Add(logFilter); Grid.SetColumn(logSearch, 1); tools.Children.Add(logSearch); root.Children.Add(tools);
        logFilter.SelectionChanged += (_, _) => RenderLogs(); logSearch.TextChanged += (_, _) => RenderLogs();
        logCount.Opacity = 0.65; Grid.SetRow(logCount, 1); root.Children.Add(logCount);
        var listArea = new Grid();
        logList.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        logList.ItemContainerStyle = new Style(typeof(ListViewItem)) { Setters = { new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch), new Setter(Control.PaddingProperty, new Thickness(0)), new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 8)) } };
        AutomationProperties.SetAutomationId(logList, "RuntimeLog"); AutomationProperties.SetName(logList, "运行记录列表");
        listArea.Children.Add(logList); logEmpty.HorizontalAlignment = HorizontalAlignment.Center; logEmpty.VerticalAlignment = VerticalAlignment.Center; logEmpty.Opacity = 0.65; listArea.Children.Add(logEmpty);
        Grid.SetRow(listArea, 2); root.Children.Add(listArea);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var copy = new Button { Content = "复制当前列表" }; AutomationProperties.SetAutomationId(copy, "CopyLogs");
        copy.Click += (_, _) => { var data = new Windows.ApplicationModel.DataTransfer.DataPackage(); data.SetText(string.Join("\n\n", VisibleLogs().Select(e => e.PlainText))); Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data); };
        var clear = new Button { Content = "清空记录" }; AutomationProperties.SetAutomationId(clear, "ClearLogs");
        clear.Click += (_, _) => { logEntries.Clear(); RenderLogs(); }; footer.Children.Add(copy); footer.Children.Add(clear);
        Grid.SetRow(footer, 3); root.Children.Add(footer); RenderLogs(); return PageLayout("运行记录", root);
    }
    private void AddLog(string message, string category = "系统", string? title = null)
    {
        var detail = message;
        if (title is null)
        {
            var end = message.IndexOf('。'); title = end >= 0 ? message[..end] : message;
            detail = end >= 0 ? message[(end + 1)..].Trim() : "";
        }
        logEntries.Add(new(DateTime.Now, category, title, detail));
        if (logEntries.Count > 150) logEntries.RemoveRange(0, logEntries.Count - 150);
        RenderLogs();
    }
    private void RenderLogs()
    {
        var visible = VisibleLogs().ToArray(); logList.Items.Clear();
        foreach (var entry in visible)
        {
            var grid = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new() { Width = new GridLength(24) }, new() { Width = new GridLength(1, GridUnitType.Star) } } };
            var icon = new SymbolIcon(entry.Category switch { "查询" => Symbol.Sync, "异常" => Symbol.Important, "提醒" => Symbol.Message, _ => Symbol.Setting }) { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Top, Opacity = 0.65, Margin = new Thickness(0, 2, 0, 0) }; grid.Children.Add(icon);
            var content = new StackPanel { Spacing = 5 };
            var meta = new Grid { ColumnDefinitions = { new() { Width = new GridLength(1, GridUnitType.Star) }, new() { Width = GridLength.Auto } } };
            var category = Text(entry.Category, 12); category.Opacity = 0.65; meta.Children.Add(category);
            var time = Text(entry.At.ToString("HH:mm:ss"), 12); time.Opacity = 0.65; Grid.SetColumn(time, 1); meta.Children.Add(time); content.Children.Add(meta);
            content.Children.Add(Text(entry.Title, 14, true));
            if (entry.Detail.Length > 0) { var detail = Text(entry.Detail, 12); detail.Opacity = 0.75; content.Children.Add(detail); }
            Grid.SetColumn(content, 1); grid.Children.Add(content);
            var card = Card(grid); card.Padding = new Thickness(14, 12, 14, 12);
            var item = new ListViewItem { Content = card }; AutomationProperties.SetName(item, entry.PlainText); logList.Items.Add(item);
        }
        logCount.Text = $"显示 {visible.Length} 条 · 本次会话共 {logEntries.Count} 条 · 最近记录在前（最多 150 条）";
        logEmpty.Text = logEntries.Count == 0 ? "暂无运行记录" : "没有符合筛选条件的记录";
        logEmpty.Visibility = visible.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
