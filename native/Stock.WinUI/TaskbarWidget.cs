using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Stock.WinUI;

/// <summary>A native taskbar child; never changes Explorer layout or registry settings.</summary>
internal sealed class TaskbarWidget : IDisposable
{
    private readonly WindowProc proc;
    private readonly string className = "IphoneStockTaskbar_" + Environment.ProcessId;
    private readonly Action open, toggle, check, hide, exit;
    private nint image, gdiplus;
    private ProductImages.Entry? productImage;
    private string productTitle = "", imagePath = "";
    private nint menuHost;
    private nint window, parent, font;
    private int fontDpi;
    private bool light, disposed;
    private string left = "iPhone · 已暂停", right = "待查询";
    private uint tone = 0;
    public string Placement { get; private set; } = "等待连接任务栏";
    public TaskbarWidget(Action open, Action toggle, Action check, Action hide, Action exit)
    {
        var input = new GdiStartup { Version = 1 };
        if (GdiplusStartup(out gdiplus, ref input, 0) != 0) throw new InvalidOperationException("无法初始化商品图绘制");
        this.open = open; this.toggle = toggle; this.check = check; this.hide = hide; this.exit = exit;
        proc = Procedure;
        var cls = new WindowClass { Proc = Marshal.GetFunctionPointerForDelegate(proc), Instance = GetModuleHandle(null), Name = className, Cursor = LoadCursor(0, 32649) };
        if (RegisterClass(ref cls) == 0) throw new System.ComponentModel.Win32Exception();
        menuHost = CreateWindowEx(0x80, className, "iPhone 库存助手菜单", 0x80000000, 0, 0, 0, 0, 0, 0, GetModuleHandle(null), 0);
    }
    public void SetProduct(ProductImages.Entry? entry, string title)
    {
        productTitle = title;
        var path = entry?.Path ?? "";
        if (path == imagePath) return;
        if (image != 0) { GdipDisposeImage(image); image = 0; }
        imagePath = path; productImage = entry;
        if (entry is not null && GdipLoadImageFromFile(path, out image) != 0) image = 0;
    }
    public void Update(string mode, string stock, string countdown, uint statusTone)
    {
        left = "iPhone · " + mode; right = stock + (countdown.Length > 0 ? " · " + countdown : ""); tone = statusTone;
        if (IsWindow(window)) { SetWindowText(window, $"{productTitle} | {left} | {right} · 点击打开库存助手"); InvalidateRect(window, 0, false); }
    }
    public void Sync(bool enabled)
    {
        if (disposed) return;
        if (!enabled) { if (IsWindow(window)) ShowWindow(window, 0); Placement = "已关闭任务栏状态条"; return; }
        var shell = FindWindow("Shell_TrayWnd", null);
        if (shell == 0) { Placement = "任务栏暂不可用，仍可从托盘打开"; return; }
        if (parent != shell || !IsWindow(window))
        {
            if (IsWindow(window)) DestroyWindow(window);
            parent = shell;
            window = CreateWindowEx(0x08080080, className, left + " | " + right, 0x40000000 | 0x04000000, 0, 0, 1, 1, parent, 0, GetModuleHandle(null), 0);
            if (window == 0) { Placement = "无法创建任务栏状态条，仍可从托盘打开"; return; }
        }
        GetClientRect(parent, out var bar); GetWindowRect(parent, out var screen);
        var dpi = (int)GetDpiForWindow(parent); if (dpi <= 0) dpi = 96;
        var scale = dpi / 96d; var width = (int)Math.Round(108 * scale); var margin = (int)Math.Round(8 * scale);
        using var advanced = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
        var centered = Convert.ToInt32(advanced?.GetValue("TaskbarAl", 1)) != 0;
        var widgets = Convert.ToInt32(advanced?.GetValue("TaskbarDa", 0)) != 0;
        if (!centered || bar.Bottom > bar.Right) { ShowWindow(window, 0); Placement = "当前任务栏布局暂不支持嵌入，使用托盘即可"; return; }
        var occupied = new List<(int Left, int Right)>();
        EnumChildWindows(parent, (child, _) =>
        {
            if (child == window || !IsWindowVisible(child)) return true;
            GetWindowRect(child, out var r);
            var cls = new StringBuilder(256); GetClassName(child, cls, cls.Capacity);
            // Full-width composition bridges contain the real taskbar, rather than taking up a slot.
            if (r.Right - r.Left > 0 && r.Right - r.Left < bar.Right * 0.8 && r.Bottom - r.Top >= bar.Bottom / 2 && !cls.ToString().Contains("InputSite"))
                occupied.Add((Math.Max(0, r.Left - screen.Left), r.Right - screen.Left));
            return true;
        }, 0);
        var x = widgets ? (int)Math.Round(180 * scale) : margin;
        foreach (var block in occupied.OrderBy(b => b.Left))
        {
            if (x + width <= block.Left - margin) break;
            if (x < block.Right + margin && x + width > block.Left - margin) x = block.Right + margin;
        }
        if (x + width + margin > bar.Right) { ShowWindow(window, 0); Placement = "任务栏没有足够空位，仍可从托盘打开"; return; }
        using var theme = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        light = Convert.ToInt32(theme?.GetValue("SystemUsesLightTheme", 0)) != 0;
        if (fontDpi != dpi)
        {
            if (font != 0) DeleteObject(font);
            font = CreateFont(-(int)Math.Round(13 * scale), 0, 0, 0, 500, 0, 0, 0, 1, 0, 0, 4, 0, "Microsoft YaHei UI"); fontDpi = dpi;
        }
        SetWindowPos(window, 0, x, 0, width, bar.Bottom, 0x50);
        SetWindowText(window, $"{productTitle} | {left} | {right} · 点击打开库存助手");
        InvalidateRect(window, 0, false);
        Present();
        Placement = "已嵌入主任务栏 · 单击打开，右键操作";
    }
    private nint Procedure(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == 0xF)
        {
            var dc = BeginPaint(hwnd, out var paint);
            EndPaint(hwnd, ref paint);
            return 0;
        }
        if (message is 0x317 or 0x318) { GetClientRect(hwnd, out var rect); Draw(wParam, rect); return 0; }
        if (message == 0x14) return 1;
        if (message == 0x21) return 3; // Keep the taskbar from taking keyboard focus.
        if (message == 0x202) { open(); return 0; }
        if (message == 0x205)
        {
            Trace("right-button-up");
            var menu = CreatePopupMenu();
            AppendMenu(menu, 0, 1, "打开库存助手"); AppendMenu(menu, 0, 2, "开始 / 暂停监控");
            AppendMenu(menu, 0, 3, "查询一次"); AppendMenu(menu, 0x800, 0, "");
            AppendMenu(menu, 0, 4, "隐藏任务栏状态条"); AppendMenu(menu, 0, 5, "退出并停止监控");
            GetCursorPos(out var point); SetForegroundWindow(menuHost);
            var command = TrackPopupMenu(menu, 0x102, point.X, point.Y, 0, menuHost, 0); DestroyMenu(menu);
            Trace($"menu-result={command}, win32={Marshal.GetLastWin32Error()}");
            switch (command) { case 1: open(); break; case 2: toggle(); break; case 3: check(); break; case 4: hide(); break; case 5: exit(); break; }
            return 0;
        }
        if (message == 2 && window == hwnd) window = 0;
        return DefWindowProc(hwnd, message, wParam, lParam);
    }
    private void Draw(nint dc, Rect rect)
    {
        var scale = (fontDpi <= 0 ? 96 : fontDpi) / 96d;
        var bg = light ? Rgb(240, 243, 247) : Rgb(35, 37, 42);
        var brush = CreateSolidBrush(bg); FillRect(dc, ref rect, brush); DeleteObject(brush);
        var pill = new Rect { Left = 0, Top = (int)(7 * scale), Right = rect.Right, Bottom = rect.Bottom - (int)(7 * scale) };
        brush = CreateSolidBrush(light ? Rgb(255, 255, 255) : Rgb(49, 52, 58));
        var oldBrush = SelectObject(dc, brush); var oldPen = SelectObject(dc, GetStockObject(8));
        RoundRect(dc, pill.Left, pill.Top, pill.Right, pill.Bottom, (int)(12 * scale), (int)(12 * scale));
        SelectObject(dc, oldBrush); SelectObject(dc, oldPen); DeleteObject(brush);
        var previousFont = SelectObject(dc, font); SetBkMode(dc, 1);
        if (image != 0 && productImage is { } entry && GdipCreateFromHDC(dc, out var graphics) == 0)
        {
            GdipSetInterpolationMode(graphics, 7);
            var b = entry.Bounds; var factor = Math.Min(30 * scale / (b[2] - b[0]), 28 * scale / (b[3] - b[1]));
            var w = (float)((b[2] - b[0]) * factor); var h = (float)((b[3] - b[1]) * factor);
            GdipDrawImageRectRectI(graphics, image, (int)(7 * scale + (30 * scale - w) / 2), (int)((rect.Bottom - h) / 2), (int)w, (int)h, b[0], b[1], b[2] - b[0], b[3] - b[1], 2, 0, 0, 0);
            GdipDeleteGraphics(graphics);
        }
        var text = new Rect { Left = (int)(43 * scale), Top = 0, Right = rect.Right - (int)(6 * scale), Bottom = rect.Bottom };
        SetTextColor(dc, tone switch { 1 => light ? Rgb(15, 120, 64) : Rgb(110, 220, 150), 2 => light ? Rgb(155, 87, 0) : Rgb(255, 196, 90), _ => light ? Rgb(85, 99, 119) : Rgb(190, 202, 220) });
        var compact = left.EndsWith("已暂停", StringComparison.Ordinal) ? "已暂停" : left.EndsWith("查询中", StringComparison.Ordinal) ? "查询中" : right.Split(" · ")[0];
        DrawText(dc, compact, -1, ref text, 0x24 | 0x8000); SelectObject(dc, previousFont);
    }
    private static uint Rgb(uint r, uint g, uint b) => r | g << 8 | b << 16;
    private static void Trace(string message)
    {
        if (Environment.GetEnvironmentVariable("IPHONE_STOCK_TRACE") != "1") return;
        try { File.AppendAllText(Path.Combine(Stock.Core.SettingsStore.DefaultDirectory, "taskbar-events.log"), $"{DateTimeOffset.Now:O} {message}\n"); } catch { }
    }
    private void Present()
    {
        GetClientRect(window, out var rect);
        if (rect.Right <= 0 || rect.Bottom <= 0) return;
        var screen = GetDC(0); var dc = CreateCompatibleDC(screen);
        var info = new BitmapInfo { Size = 40, Width = rect.Right, Height = -rect.Bottom, Planes = 1, Bits = 32 };
        var bitmap = CreateDIBSection(screen, ref info, 0, out var pixels, 0, 0);
        if (bitmap == 0) { DeleteDC(dc); ReleaseDC(0, screen); return; }
        var previous = SelectObject(dc, bitmap);
        Draw(dc, rect); GdiFlush();
        var data = new byte[rect.Right * rect.Bottom * 4]; Marshal.Copy(pixels, data, 0, data.Length);
        for (var i = 0; i < data.Length; i += 4)
        {
            var outside = light ? data[i] == 247 && data[i + 1] == 243 && data[i + 2] == 240 : data[i] == 42 && data[i + 1] == 37 && data[i + 2] == 35;
            if (outside) data[i] = data[i + 1] = data[i + 2] = data[i + 3] = 0;
            else data[i + 3] = 255;
        }
        Marshal.Copy(data, 0, pixels, data.Length);
        var size = new Point { X = rect.Right, Y = rect.Bottom }; var origin = new Point(); var blend = new Blend { Alpha = 255, Format = 1 };
        UpdateLayeredWindow(window, screen, 0, ref size, dc, ref origin, 0, ref blend, 2);
        SelectObject(dc, previous); DeleteObject(bitmap); DeleteDC(dc); ReleaseDC(0, screen);
    }
    public void Dispose() { disposed = true; if (IsWindow(window)) DestroyWindow(window); if (IsWindow(menuHost)) DestroyWindow(menuHost); if (font != 0) DeleteObject(font); if (image != 0) GdipDisposeImage(image); if (gdiplus != 0) GdiplusShutdown(gdiplus); UnregisterClass(className, GetModuleHandle(null)); }
    [StructLayout(LayoutKind.Sequential)] private struct GdiStartup { public uint Version; public nint Callback; public int SuppressThread, SuppressCodecs; }
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern int GdiplusStartup(out nint token, ref GdiStartup input, nint output);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern void GdiplusShutdown(nint token);
    [DllImport("gdiplus.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] private static extern int GdipLoadImageFromFile(string file, out nint image);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern int GdipDisposeImage(nint image);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern int GdipCreateFromHDC(nint dc, out nint graphics);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern int GdipDeleteGraphics(nint graphics);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern int GdipSetInterpolationMode(nint graphics, int mode);
    [DllImport("gdiplus.dll", ExactSpelling = true)] private static extern int GdipDrawImageRectRectI(nint graphics, nint image, int x, int y, int w, int h, int sx, int sy, int sw, int sh, int unit, nint attributes, nint callback, nint data);
    private delegate nint WindowProc(nint hwnd, uint message, nint wParam, nint lParam);
    private delegate bool EnumProc(nint hwnd, nint lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass { public uint Style; public nint Proc; public int ClsExtra, WndExtra; public nint Instance, Icon, Cursor, Background, Menu; [MarshalAs(UnmanagedType.LPWStr)] public string Name; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public uint Size; public int Width, Height; public ushort Planes, Bits; public uint Compression, ImageSize; public int X, Y; public uint Used, Important, Color; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct Blend { public byte Op, Flags, Alpha, Format; }
    [StructLayout(LayoutKind.Sequential)] private struct Paint { public nint Dc; public int Erase; public Rect Rect; public int Restore, Update; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Reserved; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClass(ref WindowClass cls);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string cls, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint DefWindowProc(nint hwnd, uint msg, nint wp, nint lp);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder cls, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowText(nint hwnd, string text);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint hwnd, EnumProc proc, nint param);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(nint hwnd, nint rect, bool erase);
    [DllImport("user32.dll")] private static extern nint BeginPaint(nint hwnd, out Paint paint);
    [DllImport("user32.dll")] private static extern bool EndPaint(nint hwnd, ref Paint paint);
    [DllImport("user32.dll")] private static extern int FillRect(nint dc, ref Rect rect, nint brush);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DrawText(nint dc, string text, int count, ref Rect rect, uint flags);
    [DllImport("gdi32.dll")] private static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern nint GetStockObject(int obj);
    [DllImport("gdi32.dll")] private static extern bool RoundRect(nint dc, int l, int t, int r, int b, int ew, int eh);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(nint dc, uint color);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(nint dc, int mode);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateFont(int h,int w,int e,int o,int weight,uint italic,uint underline,uint strike,uint charset,uint output,uint clip,uint quality,uint pitch,string face);
    [DllImport("user32.dll")] private static extern nint LoadCursor(nint instance, nint name);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint menu, uint flags, nuint id, string title);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(nint dc, int x, int y, nint icon, int width, int height, uint step, nint brush, uint flags);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint pixels, nint section, uint offset);
    [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(nint hwnd, nint dest, nint position, ref Point size, nint source, ref Point origin, uint key, ref Blend blend, uint flags);
}
