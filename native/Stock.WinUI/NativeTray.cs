using System.Runtime.InteropServices;

namespace Stock.WinUI;

internal sealed class NativeTray : IDisposable
{
    public const string WindowTitle = "iPhone库存助手";
    private readonly nint hwnd;
    private readonly SubclassProc callback;
    private readonly Action show, exit;
    private Data data;
    private const uint TrayMessage = 0x8001;
    public nint IconHandle { get; }
    public NativeTray(nint window, Action show, Action exit)
    {
        hwnd = window; this.show = show; this.exit = exit; callback = Handle;
        SetWindowSubclass(hwnd, callback, 1, 0);
        IconHandle = LoadImage(0, Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"), 1, 32, 32, 0x10);
        if (IconHandle == 0) throw new InvalidOperationException("无法加载程序图标");
        data = new Data { Size = (uint)Marshal.SizeOf<Data>(), Window = hwnd, Id = 1, Flags = 1 | 2 | 4, Message = TrayMessage, Icon = IconHandle, Tip = WindowTitle, Info = "", Title = "" };
        if (!Shell_NotifyIcon(0, ref data)) throw new InvalidOperationException("无法创建系统托盘图标");
    }
    public void Notify(string title, string message, bool sound)
    {
        data.Flags = 1 | 2 | 4 | 16;
        data.Title = title.Length > 63 ? title[..63] : title;
        data.Info = message.Length > 255 ? message[..255] : message;
        data.InfoFlags = sound ? 1u : 0x11u;
        Shell_NotifyIcon(1, ref data);
        if (sound) MessageBeep(0x40);
    }
    private nint Handle(nint window, uint message, nuint wParam, nint lParam, nuint id, nuint reference)
    {
        if (message == TrayMessage)
        {
            var evt = (uint)(lParam.ToInt64() & 0xffff);
            if (evt is 0x202 or 0x203 or 0x405) show();
            else if (evt == 0x205)
            {
                var menu = CreatePopupMenu();
                AppendMenu(menu, 0, 1, "打开库存助手"); AppendMenu(menu, 0, 2, "退出并停止监控");
                GetCursorPos(out var point); SetForegroundWindow(hwnd);
                var command = TrackPopupMenu(menu, 0x100 | 0x2, point.X, point.Y, 0, hwnd, 0);
                DestroyMenu(menu);
                if (command == 1) show(); else if (command == 2) exit();
            }
            return 0;
        }
        return DefSubclassProc(window, message, wParam, lParam);
    }
    public static void ActivateExisting() { var window = FindWindow(null, WindowTitle); if (window != 0) { ShowWindow(window, 9); SetForegroundWindow(window); } }
    public static void Hide(nint window) => ShowWindow(window, 0);
    public static void Show(nint window) { ShowWindow(window, 9); SetForegroundWindow(window); }
    public void Dispose() { Shell_NotifyIcon(2, ref data); RemoveWindowSubclass(hwnd, callback, 1); if (IconHandle != 0) DestroyIcon(IconHandle); }
    private delegate nint SubclassProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint reference);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Data
    {
        public uint Size; public nint Window; public uint Id, Flags, Message; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint message, ref Data data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint reference);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern nint LoadIcon(nint instance, nint name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint LoadImage(nint instance, string name, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string? cls, string title);
    [DllImport("user32.dll")] private static extern bool MessageBeep(uint type);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint menu, uint flags, nuint id, string text);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
}
