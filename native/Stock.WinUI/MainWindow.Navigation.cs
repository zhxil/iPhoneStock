using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Stock.Core;

namespace Stock.WinUI;

public sealed partial class MainWindow
{
    private readonly Frame pageFrame = new() { CacheSize = 4 };
    private NavigationView navigation = null!;
    private readonly ContentControl productPreview = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };

    private UIElement Build()
    {
        navigation = new NavigationView
        {
            PaneDisplayMode = NavigationViewPaneDisplayMode.Auto,
            IsPaneOpen = true, OpenPaneLength = 200, CompactPaneLength = 48,
            ExpandedModeThresholdWidth = 800, CompactModeThresholdWidth = 640,
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = true, PaneTitle = "", AlwaysShowHeader = false
        };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.png")), Width = 24, Height = 24 });
        brand.Children.Add(Text("iPhone库存助手", 14, true)); navigation.PaneHeader = brand;
        AutomationProperties.SetAutomationId(navigation, "AppNavigation");
        var monitorItem = NavItem("监控", Symbol.ViewAll, "monitor", "NavMonitor");
        navigation.MenuItems.Add(monitorItem);
        navigation.MenuItems.Add(NavItem("添加目标", Symbol.Add, "add", "NavAdd"));
        navigation.MenuItems.Add(NavItem("运行记录", Symbol.Clock, "logs", "NavLogs"));
        var content = new Grid { RowDefinitions = { new() { Height = GridLength.Auto }, new() { Height = new GridLength(1, GridUnitType.Star) } } };
        notice.Margin = new Thickness(24, 0, 24, 0); content.Children.Add(notice);
        Grid.SetRow(pageFrame, 1); content.Children.Add(pageFrame); navigation.Content = content;
        navigation.SelectionChanged += (_, e) =>
        {
            var key = e.IsSettingsSelected ? "settings" : (e.SelectedItem as NavigationViewItem)?.Tag as string;
            var type = key switch { "add" => typeof(AddTargetPage), "logs" => typeof(LogsPage), "settings" => typeof(SettingsPage), _ => typeof(MonitorPage) };
            if (notice.Severity is InfoBarSeverity.Success or InfoBarSeverity.Informational) notice.IsOpen = false;
            pageFrame.Navigate(type, this, new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
            pageFrame.BackStack.Clear();
        };
        navigation.Loaded += (_, _) =>
        {
            if (navigation.SettingsItem is NavigationViewItem item) { item.Content = "设置"; AutomationProperties.SetAutomationId(item, "NavSettings"); }
        };
        navigation.SelectedItem = monitorItem;
        return navigation;
    }
    private static NavigationViewItem NavItem(string label, Symbol icon, string key, string id)
    {
        var item = new NavigationViewItem { Content = label, Icon = new SymbolIcon(icon), Tag = key };
        AutomationProperties.SetAutomationId(item, id); return item;
    }
    internal FrameworkElement BuildMonitorPage()
    {
        var root = new Grid { Margin = new Thickness(24, 12, 24, 24), RowSpacing = 18, RowDefinitions = { new() { Height = GridLength.Auto }, new() { Height = GridLength.Auto }, new() { Height = new GridLength(1, GridUnitType.Star) }, new() { Height = GridLength.Auto } } };
        var toolbar = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new() { Width = new GridLength(1, GridUnitType.Star) }, new() { Width = GridLength.Auto } } };
        var status = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        runText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold; status.Children.Add(runText); nextText.FontSize = 12; nextText.Opacity = 0.7; status.Children.Add(nextText); toolbar.Children.Add(status);
        var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        start.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        AutomationProperties.SetAutomationId(start, "StartMonitor"); AutomationProperties.SetAutomationId(check, "CheckOnce");
        commands.Children.Add(check); commands.Children.Add(start); Grid.SetColumn(commands, 1); toolbar.Children.Add(commands); root.Children.Add(toolbar);
        summaryText.FontSize = 13; summaryText.Opacity = 0.7; Grid.SetRow(summaryText, 1); root.Children.Add(summaryText);
        var list = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(list, 2); root.Children.Add(list);
        var footer = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new() { Width = GridLength.Auto }, new() { Width = new GridLength(1, GridUnitType.Star) } } };
        footer.Children.Add(new FontIcon { Glyph = "\uE946", FontSize = 14, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center });
        var hint = Text("关闭窗口收进托盘 · 库存以 Apple 官网为准", 12); hint.Opacity = 0.65; Grid.SetColumn(hint, 1); footer.Children.Add(hint);
        Grid.SetRow(footer, 3); root.Children.Add(footer); return PageLayout("监控", root);
    }
    internal FrameworkElement BuildAddPage()
    {
        var root = new Grid { RowDefinitions = { new() { Height = new GridLength(1, GridUnitType.Star) }, new() { Height = GridLength.Auto } } };
        var page = PageStack();
        page.Spacing = 12;
        foreach (var combo in new[] { cities, stores, families, capacities, colors }) { combo.Width = double.NaN; combo.HorizontalAlignment = HorizontalAlignment.Stretch; }
        page.Children.Add(Text("取货门店", 16, true)); page.Children.Add(Card(Pair(cities, stores)));
        page.Children.Add(Text("选择商品", 16, true));
        var product = new Grid { ColumnSpacing = 20, ColumnDefinitions = { new() { Width = new GridLength(112) }, new() { Width = new GridLength(1, GridUnitType.Star) } } };
        product.Children.Add(productPreview);
        var choices = new StackPanel { Spacing = 14, VerticalAlignment = VerticalAlignment.Center }; choices.Children.Add(families); choices.Children.Add(Pair(capacities, colors)); Grid.SetColumn(choices, 1); product.Children.Add(choices); page.Children.Add(Card(product));
        add.Style = (Style)Application.Current.Resources["AccentButtonStyle"]; add.HorizontalAlignment = HorizontalAlignment.Left; AutomationProperties.SetAutomationId(add, "AddTarget");
        var help = Text("监控运行中请先暂停，再添加或移除目标。", 12); help.Opacity = 0.65; page.Children.Add(help);
        root.Children.Add(Scroll(page));
        var footer = new Border { Padding = new Thickness(24, 12, 24, 20), Child = add }; Grid.SetRow(footer, 1); root.Children.Add(footer);
        UpdateProductPreview(); return PageLayout("添加目标", root);
    }
    internal FrameworkElement BuildSettingsPage()
    {
        var root = new Grid { RowDefinitions = { new() { Height = new GridLength(1, GridUnitType.Star) }, new() { Height = GridLength.Auto } } };
        var page = PageStack();
        page.Spacing = 10;
        page.Children.Add(Text("常规", 16, true));
        interval.Header = null; interval.Width = 120; interval.HorizontalAlignment = HorizontalAlignment.Right;
        page.Children.Add(SettingRow("查询间隔", "单位为秒，最少 60 秒。", interval));
        foreach (var toggle in new[] { taskbarToggle, startupToggle, sound }) { toggle.Header = null; toggle.OnContent = ""; toggle.OffContent = ""; toggle.MinWidth = 0; toggle.Width = 50; toggle.HorizontalAlignment = HorizontalAlignment.Right; }
        taskbarPlacement.Opacity = 0.7;
        page.Children.Add(SettingRow("任务栏状态条", taskbarPlacement, taskbarToggle));
        page.Children.Add(SettingRow("开机启动并开始监控", "登录后在后台监控。移动程序后需重新保存设置。", startupToggle));
        var alertTitle = Text("到货提醒", 16, true); alertTitle.Margin = new Thickness(0, 10, 0, 0); page.Children.Add(alertTitle);
        page.Children.Add(SettingRow("到货提示音", "确认有货时播放系统提示音。", sound));
        var alerts = new StackPanel { Spacing = 16 }; alerts.Children.Add(bark); alerts.Children.Add(feishu);
        var pushHelp = Text("推送地址仅保存在本机。飞书机器人关键词请设置为“有货”。", 12); pushHelp.Opacity = 0.65; alerts.Children.Add(pushHelp); page.Children.Add(Card(alerts));
        page.Children.Add(Text("应用", 16, true));
        var app = new StackPanel { Spacing = 12 }; app.Children.Add(Text("iPhone库存助手  1.3.1", 14));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var folder = new Button { Content = "打开配置目录" }; folder.Click += (_, _) => { Directory.CreateDirectory(SettingsStore.DefaultDirectory); Process.Start(new ProcessStartInfo(SettingsStore.DefaultDirectory) { UseShellExecute = true }); };
        var exit = new Button { Content = "退出程序" }; exit.Click += (_, _) => Quit(); actions.Children.Add(folder); actions.Children.Add(exit); app.Children.Add(actions); page.Children.Add(Card(app));
        root.Children.Add(Scroll(page));
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Padding = new Thickness(24, 12, 24, 20) };
        var save = new Button { Content = "保存设置", Style = (Style)Application.Current.Resources["AccentButtonStyle"] }; AutomationProperties.SetAutomationId(save, "SaveSettings"); save.Click += (_, _) => { if (SaveInputs()) ShowNotice("设置已保存", "新的查询间隔将在下一轮生效。", InfoBarSeverity.Success); };
        var test = new Button { Content = "测试提醒" }; AutomationProperties.SetAutomationId(test, "TestNotification"); test.Click += async (_, _) =>
        {
            if (!SaveInputs()) return;
            test.IsEnabled = false;
            try
            {
                tray.Notify("有货了（测试）", "iPhone库存助手：本机通知测试。", settings.SoundEnabled);
                var errors = await push.SendAsync(settings, "有货了（测试）", "iPhone库存助手提醒链路测试");
                AddLog(errors.Count == 0 ? "请检查系统通知及已配置的手机推送。" : string.Join("；", errors), errors.Count == 0 ? "提醒" : "异常", errors.Count == 0 ? "已提交测试提醒" : "测试推送失败");
                ShowNotice(errors.Count == 0 ? "已提交测试提醒" : "推送失败", errors.Count == 0 ? "请检查系统通知、声音及已配置的手机推送。" : string.Join("；", errors), errors.Count == 0 ? InfoBarSeverity.Informational : InfoBarSeverity.Warning);
            }
            finally { test.IsEnabled = true; }
        };
        AutomationProperties.SetAutomationId(taskbarToggle, "TaskbarToggle"); AutomationProperties.SetAutomationId(startupToggle, "StartupToggle");
        footer.Children.Add(save); footer.Children.Add(test); Grid.SetRow(footer, 1); root.Children.Add(footer); return PageLayout("设置", root);
    }
    private void UpdateProductPreview()
    {
        var product = catalog.Products.FirstOrDefault(p => p.Family == families.SelectedItem as string && p.Capacity == capacities.SelectedItem as string && p.Color == colors.SelectedItem as string);
        if (product is null) { productPreview.Content = null; return; }
        var preview = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        preview.Children.Add(ProductImages.Picture(product.PartNumber, product.Title, 112, 100));
        var variant = Text(product.Color, 12); variant.HorizontalAlignment = HorizontalAlignment.Center; variant.Opacity = 0.7; preview.Children.Add(variant);
        AutomationProperties.SetName(preview, product.Title + " " + product.PartNumber);
        AutomationProperties.SetAutomationId(preview, "ProductPreviewDetails"); productPreview.Content = preview;
    }
    private static FrameworkElement PageLayout(string title, FrameworkElement body)
    {
        var layout = new Grid { RowDefinitions = { new() { Height = GridLength.Auto }, new() { Height = new GridLength(1, GridUnitType.Star) } } };
        var heading = Text(title, 28, true); heading.Margin = new Thickness(24, 24, 24, 12); layout.Children.Add(heading);
        Grid.SetRow(body, 1); layout.Children.Add(body); return layout;
    }
    private static StackPanel PageStack() => new() { Spacing = 16, Padding = new Thickness(24, 12, 24, 24) };
    private static Border SettingRow(string title, string description, FrameworkElement control) => SettingRow(title, Text(description, 12), control);
    private static Border SettingRow(string title, TextBlock description, FrameworkElement control)
    {
        var row = new Grid { ColumnSpacing = 20, MinHeight = 40, ColumnDefinitions = { new() { Width = new GridLength(1, GridUnitType.Star) }, new() { Width = GridLength.Auto } } };
        var labels = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center }; labels.Children.Add(Text(title, 14, true)); description.FontSize = 12; description.Opacity = 0.65; labels.Children.Add(description); row.Children.Add(labels);
        control.VerticalAlignment = VerticalAlignment.Center; AutomationProperties.SetName(control, title); Grid.SetColumn(control, 1); row.Children.Add(control); var card = Card(row); card.Padding = new Thickness(16, 12, 16, 12); return card;
    }
    private static ScrollViewer Scroll(UIElement content) => new() { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private static Grid Pair(FrameworkElement first, FrameworkElement second)
    {
        var grid = new Grid { ColumnSpacing = 16, ColumnDefinitions = { new() { Width = new GridLength(1, GridUnitType.Star) }, new() { Width = new GridLength(1, GridUnitType.Star) } } };
        grid.Children.Add(first); Grid.SetColumn(second, 1); grid.Children.Add(second); return grid;
    }
}

public sealed partial class MonitorPage : Page
{
    public MonitorPage() { InitializeComponent(); NavigationCacheMode = NavigationCacheMode.Required; }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (Content is null) Content = ((MainWindow)e.Parameter).BuildMonitorPage();
    }
}

public sealed partial class AddTargetPage : Page
{
    public AddTargetPage() { InitializeComponent(); NavigationCacheMode = NavigationCacheMode.Required; }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (Content is null) Content = ((MainWindow)e.Parameter).BuildAddPage();
    }
}

public sealed partial class SettingsPage : Page
{
    public SettingsPage() { InitializeComponent(); NavigationCacheMode = NavigationCacheMode.Required; }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (Content is null) Content = ((MainWindow)e.Parameter).BuildSettingsPage();
    }
}

public sealed partial class LogsPage : Page
{
    public LogsPage() { InitializeComponent(); NavigationCacheMode = NavigationCacheMode.Required; }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (Content is null) Content = ((MainWindow)e.Parameter).BuildLogsPage();
    }
}
