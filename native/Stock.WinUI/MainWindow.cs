using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Stock.Core;
using Windows.UI;
using Monitor = Stock.Core.Monitor;

namespace Stock.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly Catalog catalog = new();
    private readonly SettingsStore store = new(Path.Combine(SettingsStore.DefaultDirectory, "settings.v1.json"));
    private readonly AppleClient client = new(cooldownPath: Path.Combine(SettingsStore.DefaultDirectory, "request-cooldown.txt"));
    private readonly PushService push = new();
    private readonly Monitor monitor;
    private Settings settings;
    private readonly Dictionary<Target, Result> results = [];
    private readonly NativeTray tray;
    private readonly TaskbarWidget? taskbar;
    private readonly nint hwnd;
    private CancellationTokenSource? cancellation;
    private bool exiting, running, checking, troubleNotified;
    private DateTimeOffset? nextCheck;
    private readonly TextBlock runText = Text("尚未开始", 14);
    private readonly TextBlock nextText = Text("点击开始监控，每分钟查询一次", 13);
    private readonly TextBlock summaryText = Text("1 个监控目标", 14);
    private readonly StackPanel rows = new() { Spacing = 10 };
    private readonly InfoBar notice = new() { IsOpen = false, IsClosable = true };
    private readonly Button start = new() { Content = "开始监控" };
    private readonly Button check = new() { Content = "查询一次" };
    private readonly Button add = new() { Content = "添加到监控" };
    private readonly ComboBox cities = Combo("城市", 140), stores = Combo("Apple 直营店", 225), families = Combo("机型", 190), capacities = Combo("容量", 100), colors = Combo("颜色", 150);
    private readonly NumberBox interval = new() { Header = "查询间隔（秒）", Minimum = 60, Maximum = 3600, SmallChange = 30, Width = 180, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
    private readonly ToggleSwitch sound = new() { Header = "到货提示音", OnContent = "开启", OffContent = "关闭" };
    private readonly ToggleSwitch taskbarToggle = new() { Header = "在任务栏显示状态条", OnContent = "显示", OffContent = "隐藏" };
    private readonly ToggleSwitch startupToggle = new() { Header = "开机启动并开始监控", OnContent = "开启", OffContent = "关闭" };
    private readonly TextBlock taskbarPlacement = Text("", 12);
    private readonly PasswordBox bark = new() { Header = "Bark 推送地址（可选）", PlaceholderText = "https://api.day.app/你的密钥" };
    private readonly PasswordBox feishu = new() { Header = "飞书群机器人地址（可选）", PlaceholderText = "https://open.feishu.cn/open-apis/bot/v2/hook/…" };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        Title = NativeTray.WindowTitle;
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1120, 760));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        settings = store.Load();
        monitor = new(client, catalog);
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        tray = new(hwnd, () => { NativeTray.Show(hwnd); Activate(); }, Quit);
        try
        {
            taskbar = new TaskbarWidget(
                () => { NativeTray.Show(hwnd); Activate(); },
                () => { if (running) Stop(); else _ = StartAsync(); },
                () => _ = CheckOnceAsync(),
                () => { taskbarToggle.IsOn = false; SaveInputs(); UpdateTaskbar(); }, Quit);
        }
        catch (Exception ex) { App.Log(ex); }
        Content = Build();
        interval.Value = settings.IntervalSeconds; sound.IsOn = settings.SoundEnabled; taskbarToggle.IsOn = settings.TaskbarEnabled;
        startupToggle.IsOn = settings.StartupEnabled;
        bark.Password = settings.BarkUrl; feishu.Password = settings.FeishuUrl;
        PopulateSelectors();
        start.Click += async (_, _) => { if (running) Stop(); else await StartAsync(); };
        check.Click += async (_, _) => await CheckOnceAsync();
        add.Click += (_, _) => AddTarget();
        monitor.Results += batch => DispatcherQueue.TryEnqueue(() =>
        {
            foreach (var r in batch) { results[r.Target] = r; AddLog(r.Target.StoreTitle + "\n" + r.Detail, r.Kind == StockKind.Unknown ? "异常" : "查询", Label(r.Kind) + " · " + r.Target.ProductName); }
            RenderRows();
            var unknown = batch.FirstOrDefault(r => r.Kind == StockKind.Unknown);
            if (unknown is not null)
            {
                ShowNotice("查询未取得可信结果", unknown.Detail, InfoBarSeverity.Warning);
                if (!troubleNotified) tray.Notify("监控暂不可信", unknown.Detail, false);
                troubleNotified = true;
            }
            else
            {
                troubleNotified = false;
                if (notice.Title == "查询未取得可信结果") notice.IsOpen = false;
            }
        });
        monitor.Arrived += r => DispatcherQueue.TryEnqueue(() => _ = NotifyArrivalAsync(r));
        monitor.Scheduled += date => DispatcherQueue.TryEnqueue(() => { nextCheck = date; UpdateCountdown(); });
        AppWindow.Closing += (_, e) =>
        {
            if (exiting) return;
            e.Cancel = true;
            NativeTray.Hide(hwnd);
            tray.Notify("已收进系统托盘", running ? "库存监控继续运行。右键托盘图标可退出。" : "当前未开始监控。点击托盘图标可恢复窗口。", false);
        };
        Closed += (_, _) => { cancellation?.Cancel(); timer.Stop(); taskbar?.Dispose(); tray.Dispose(); push.Dispose(); client.Dispose(); };
        timer.Tick += (_, _) => { UpdateCountdown(); UpdateTaskbar(); }; timer.Start();
        RenderRows();
        if (store.Warning is not null) ShowNotice("配置文件需要处理", store.Warning, InfoBarSeverity.Error);
        else TrySave(settings);
        UpdateTaskbar();
        AddLog("程序已就绪。尚未发出库存查询；关闭窗口会收进托盘。目录为项目内置快照，实际库存以 Apple 查询结果为准。");
    }

    private void PopulateSelectors()
    {
        cities.ItemsSource = catalog.Stores.Select(s => s.City).Distinct().ToArray();
        cities.SelectionChanged += (_, _) => { stores.ItemsSource = catalog.Stores.Where(s => s.City == cities.SelectedItem as string).ToArray(); stores.SelectedIndex = 0; };
        families.ItemsSource = catalog.Products.Select(p => p.Family).Distinct().ToArray();
        families.SelectionChanged += (_, _) => { capacities.ItemsSource = catalog.Products.Where(p => p.Family == families.SelectedItem as string).Select(p => p.Capacity).Distinct().ToArray(); capacities.SelectedIndex = 0; };
        capacities.SelectionChanged += (_, _) => { colors.ItemsSource = catalog.Products.Where(p => p.Family == families.SelectedItem as string && p.Capacity == capacities.SelectedItem as string).Select(p => p.Color).Distinct().ToArray(); colors.SelectedIndex = 0; };
        colors.SelectionChanged += (_, _) => UpdateProductPreview();
        cities.SelectedItem = "青岛"; families.SelectedItem = "iPhone Duo"; capacities.SelectedItem = "512GB"; colors.SelectedItem = "星光白色";
    }
    private void AddTarget()
    {
        if (running || checking) return;
        var product = catalog.Products.FirstOrDefault(p => p.Family == families.SelectedItem as string && p.Capacity == capacities.SelectedItem as string && p.Color == colors.SelectedItem as string);
        if (stores.SelectedItem is not Store selected || product is null) return;
        if (settings.Targets.Any(t => t.StoreNumber == selected.Number && t.PartNumber == product.PartNumber)) { ShowNotice("已在监控列表中", "相同门店和商品不会重复添加。", InfoBarSeverity.Informational); return; }
        var updated = settings with { Targets = [.. settings.Targets, new(selected.Number, selected.Title, product.PartNumber, product.Title)] };
        if (TrySave(updated)) { settings = updated; RenderRows(); }
    }
    private void RenderRows()
    {
        rows.Children.Clear();
        foreach (var target in settings.Targets)
        {
            results.TryGetValue(target, out var result);
            var grid = new Grid { ColumnSpacing = 16, RowSpacing = 10, ColumnDefinitions = { new() { Width = new GridLength(84) }, new() { Width = new GridLength(1, GridUnitType.Star) }, new() { Width = new GridLength(124) }, new() { Width = GridLength.Auto } }, RowDefinitions = { new() { Height = GridLength.Auto }, new() { Height = GridLength.Auto }, new() { Height = GridLength.Auto } } };
            var product = catalog.Products.FirstOrDefault(p => p.PartNumber == target.PartNumber);
            grid.Children.Add(ProductImages.Picture(target.PartNumber, target.ProductName, 84, 88));
            var details = new StackPanel { Spacing = 5 };
            details.VerticalAlignment = VerticalAlignment.Center;
            details.Children.Add(Text(product?.Family ?? target.ProductName, 17, true));
            if (product is not null) { var variant = Text(product.Color + " · " + product.Capacity, 13); variant.Opacity = 0.75; details.Children.Add(variant); }
            details.Children.Add(Text(target.StoreTitle, 13));
            details.Children.Add(new TextBlock { Text = result?.Detail ?? "等待首次查询", FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = 0.75 });
            Grid.SetColumn(details, 1); grid.Children.Add(details);
            var state = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
            var label = Text(Label(result?.Kind ?? StockKind.Pending), 18, true);
            label.Style = (Style)Application.Current.Resources[result?.Kind switch { StockKind.InStock => "StockAvailableStyle", StockKind.Unknown => "StockUnknownStyle", _ => "StockPendingStyle" }];
            state.Children.Add(label); var checkedText = Text(result is null ? "尚未查询" : $"更新于 {result.CheckedAt:HH:mm:ss}", 12); checkedText.Opacity = 0.65; state.Children.Add(checkedText); Grid.SetColumn(state, 2); grid.Children.Add(state);
            var remove = new MenuFlyoutItem { Text = "移除监控目标", Icon = new SymbolIcon(Symbol.Delete), IsEnabled = !running && !checking };
            AutomationProperties.SetName(remove, "移除 " + target.ProductName + " " + target.StoreTitle);
            remove.Click += async (_, _) =>
            {
                var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = "移除此监控目标？", Content = target.ProductName + "\n" + target.StoreTitle, PrimaryButtonText = "移除", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    var updated = settings with { Targets = settings.Targets.Where(t => t != target).ToList() };
                    if (TrySave(updated)) { settings = updated; results.Remove(target); RenderRows(); }
                }
            };
            var more = new Button { Content = new FontIcon { Glyph = "\uE712", FontSize = 16 }, VerticalAlignment = VerticalAlignment.Top, Padding = new Thickness(8), Flyout = new MenuFlyout { Items = { remove } } };
            AutomationProperties.SetName(more, "更多操作 " + target.ProductName); AutomationProperties.SetAutomationId(more, "TargetActions");
            ToolTipService.SetToolTip(more, "更多操作"); Grid.SetColumn(more, 3); grid.Children.Add(more);
            var familySlug = product?.Family.ToLowerInvariant().Replace(" ", "-");
            var buyUri = new Uri(familySlug is null ? "https://www.apple.com.cn/shop/buy-iphone" : $"https://www.apple.com.cn/shop/buy-iphone/{familySlug}/{Uri.EscapeDataString(target.PartNumber.ToLowerInvariant()).Replace("%2F", "/", StringComparison.OrdinalIgnoreCase)}");
            var buyContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }; buyContent.Children.Add(new FontIcon { Glyph = "\uE8A7", FontSize = 14 }); buyContent.Children.Add(Text("在 Apple 查看", 14));
            var buy = new Button { Content = buyContent, HorizontalAlignment = HorizontalAlignment.Right };
            buy.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(buyUri.AbsoluteUri) { UseShellExecute = true }); } catch (Exception ex) { ShowNotice("无法打开购买页", ex.Message, InfoBarSeverity.Warning); } };
            AutomationProperties.SetName(buy, "打开 Apple 购买页 " + target.ProductName);
            AutomationProperties.SetAutomationId(buy, "OpenProductPage"); AutomationProperties.SetHelpText(buy, buyUri.AbsoluteUri);
            var actions = new Border { Child = buy, BorderThickness = new Thickness(0, 1, 0, 0), Style = (Style)Application.Current.Resources["StockCardDividerStyle"], Padding = new Thickness(0, 10, 0, 0) };
            Grid.SetColumnSpan(actions, 4); Grid.SetRow(actions, 1); grid.Children.Add(actions);
            grid.SizeChanged += (_, e) =>
            {
                var compact = e.NewSize.Width < 580;
                grid.ColumnDefinitions[2].Width = compact ? new GridLength(0) : new GridLength(124);
                Grid.SetColumn(state, compact ? 1 : 2); Grid.SetRow(state, compact ? 1 : 0);
                Grid.SetColumnSpan(state, compact ? 2 : 1);
                Grid.SetRow(actions, compact ? 2 : 1);
            };
            rows.Children.Add(Card(grid));
        }
        if (settings.Targets.Count == 0) rows.Children.Add(Text("还没有监控目标。从左侧“添加目标”选择商品和门店。", 14));
        summaryText.Text = $"{settings.Targets.Count} 项 · {results.Values.Count(r => r.Kind == StockKind.InStock)} 项有货";
        start.IsEnabled = running || settings.Targets.Count > 0; check.IsEnabled = !running && !checking && settings.Targets.Count > 0; add.IsEnabled = !running && !checking;
        UpdateTaskbar();
    }
    private bool SaveInputs()
    {
        if (double.IsNaN(interval.Value) || interval.Value < 60 || interval.Value > 3600) { ShowNotice("无法保存", "查询间隔应为 60–3600 秒", InfoBarSeverity.Error); return false; }
        var updated = settings with { IntervalSeconds = (int)interval.Value, SoundEnabled = sound.IsOn, TaskbarEnabled = taskbarToggle.IsOn, StartupEnabled = startupToggle.IsOn, BarkUrl = bark.Password.Trim(), FeishuUrl = feishu.Password.Trim() };
        var changed = updated != settings;
        // Validate and save before touching the login registration. If it fails, keep disk and UI aligned.
        if (!TrySave(updated)) return false;
        try
        {
            if (updated.StartupEnabled != settings.StartupEnabled || (updated.StartupEnabled && !StartupRegistration.IsEnabled()))
                StartupRegistration.Apply(updated.StartupEnabled);
        }
        catch (Exception ex)
        {
            TrySave(settings); ShowNotice("开机启动设置失败", ex.Message, InfoBarSeverity.Error); return false;
        }
        settings = updated; UpdateTaskbar(); if (changed) AddLog("设置已保存。正在运行的监控会在下一轮采用新的间隔。"); return true;
    }
    private bool TrySave(Settings value)
    {
        try { store.Save(value); return true; }
        catch (Exception ex) { ShowNotice("保存失败", ex.Message, InfoBarSeverity.Error); return false; }
    }
    private async Task StartAsync()
    {
        if (checking || !SaveInputs()) return;
        cancellation = new(); var token = cancellation.Token; running = true; start.Content = "暂停监控"; runText.Text = "● 正在监控"; nextText.Text = "正在查询…"; RenderRows();
        AddLog($"正在监控 {settings.Targets.Count} 个目标。查询间隔 {settings.IntervalSeconds} 秒。", "系统", "监控已开始");
        try { await monitor.RunAsync(() => settings, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { ShowNotice("监控已停止", "发生异常，请查看运行记录", InfoBarSeverity.Error); AddLog(ex.Message, "异常", "监控已停止"); App.Log(ex); }
        finally { running = false; start.Content = "开始监控"; runText.Text = "已暂停"; nextCheck = null; nextText.Text = "监控已暂停；列表为最近一次查询结果"; RenderRows(); }
    }
    private void Stop() { cancellation?.Cancel(); start.IsEnabled = false; }
    private async Task CheckOnceAsync()
    {
        if (running || checking) return;
        if (client.NextAllowed > DateTimeOffset.UtcNow) { ShowNotice("请稍后查询", $"下一次可查询时间：{client.NextAllowed.ToLocalTime():HH:mm:ss}", InfoBarSeverity.Informational); return; }
        if (!SaveInputs()) return;
        cancellation = new(); var token = cancellation.Token; checking = true; start.IsEnabled = false; nextText.Text = "正在查询…"; runText.Text = "单次查询中"; RenderRows(); start.IsEnabled = false;
        try { await monitor.CheckOnceAsync(settings, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { ShowNotice("查询失败", "发生异常，请查看运行记录", InfoBarSeverity.Error); AddLog(ex.Message, "异常", "查询失败"); App.Log(ex); }
        finally { checking = false; runText.Text = "单次查询完成"; nextText.Text = $"下次可查询 {client.NextAllowed.ToLocalTime():HH:mm:ss}；持续监控请点击开始"; RenderRows(); }
    }
    private async Task NotifyArrivalAsync(Result result)
    {
        tray.Notify("有货了 · iPhone 库存助手", result.Target.ProductName + "\n" + result.Target.StoreTitle, settings.SoundEnabled);
        AddLog(result.Target.ProductName + "\n" + result.Target.StoreTitle, "提醒", "发现有货，已提交到货提醒");
        var errors = await push.SendAsync(settings, "有货了 · iPhone 库存助手", result.Target.ProductName + "\n" + result.Target.StoreTitle);
        foreach (var error in errors) AddLog(error, "异常", "到货推送失败");
        if (errors.Count > 0) ShowNotice("到货推送未全部成功", string.Join("；", errors), InfoBarSeverity.Warning);
    }
    private void Quit() { exiting = true; cancellation?.Cancel(); Close(); }
    public void StartInBackground()
    {
        NativeTray.Hide(hwnd);
        if (store.Warning is not null) tray.Notify("配置需要处理，未自动监控", "点击任务栏状态条或托盘图标查看原因。", false);
        else if (settings.StartupEnabled) _ = StartAsync();
    }
    private void UpdateCountdown()
    {
        if (nextCheck is not { } date || (!running && !checking)) return;
        var seconds = Math.Max(0, (int)Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds));
        nextText.Text = seconds == 0 ? "正在查询…" : $"下次查询 {date.ToLocalTime():HH:mm:ss} · {seconds / 60:00}:{seconds % 60:00}";
    }
    private void UpdateTaskbar()
    {
        if (taskbar is null) { taskbarPlacement.Text = "任务栏状态条未能启动，仍可使用托盘。"; return; }
        var inStock = results.Values.Count(r => r.Kind == StockKind.InStock);
        var unknown = results.Values.Any(r => r.Kind == StockKind.Unknown);
        var pending = settings.Targets.Any(t => !results.ContainsKey(t));
        var status = settings.Targets.Count == 0 ? "无目标" : inStock > 0 ? $"有货 {inStock}" : unknown ? "未知" : pending ? "待查询" : "无货";
        var remaining = nextCheck is { } next && running ? Math.Max(0, (int)(next - DateTimeOffset.UtcNow).TotalSeconds) : -1;
        var countdown = remaining < 0 ? "" : remaining > 5999 ? ">99分" : $"{remaining / 60:00}:{remaining % 60:00}";
        var featured = settings.Targets.FirstOrDefault(t => results.TryGetValue(t, out var r) && r.Kind == StockKind.InStock) ?? settings.Targets.FirstOrDefault();
        taskbar.SetProduct(ProductImages.Find(featured?.PartNumber), featured?.ProductName ?? "无监控目标");
        taskbar.Update(running ? "监控中" : checking ? "查询中" : "已暂停", status, countdown, inStock > 0 ? 1u : unknown ? 2u : 0u);
        taskbar.Sync(settings.TaskbarEnabled); taskbarPlacement.Text = taskbar.Placement;
    }
    private void ShowNotice(string title, string message, InfoBarSeverity severity) { notice.Title = title; notice.Message = message; notice.Severity = severity; notice.IsOpen = true; }
    private static string Label(StockKind kind) => kind switch { StockKind.InStock => "有货", StockKind.OutOfStock => "无货", StockKind.Unknown => "未知 · 查询失败", _ => "待查询" };
    private static TextBlock Text(string text, double size, bool bold = false) => new() { Text = text, FontSize = size, FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
    private static ComboBox Combo(string header, double width) => new() { Header = header, Width = width, MaxDropDownHeight = 320 };
    private static Border Card(UIElement content) => new() { Child = content, Style = (Style)Application.Current.Resources["StockCardStyle"] };
}
