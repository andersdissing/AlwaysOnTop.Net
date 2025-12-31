using System.Runtime.InteropServices;
using System.Text;

namespace AlwaysOnTop;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyWindow _hotkeyWindow;
    private readonly HashSet<IntPtr> _topMostWindows = new();

    public TrayApplicationContext()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Visible = true,
            Text = "Always On Top"
        };

        _trayIcon.MouseClick += TrayIcon_MouseClick;

        _hotkeyWindow = new HotkeyWindow(this);
        _hotkeyWindow.RegisterHotkey();
    }

    private static Icon CreateDefaultIcon()
    {
        // Create a simple icon programmatically (a pin symbol)
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw a pin shape
            using var pen = new Pen(Color.DodgerBlue, 2);
            using var brush = new SolidBrush(Color.DodgerBlue);

            // Pin head (circle)
            g.FillEllipse(brush, 3, 2, 10, 10);

            // Pin point (line)
            g.DrawLine(pen, 8, 12, 8, 15);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void TrayIcon_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            ShowWindowMenu();
        }
    }

    private void ShowWindowMenu()
    {
        var menu = new ContextMenuStrip();

        var windows = GetOpenWindows();

        if (windows.Count == 0)
        {
            menu.Items.Add(new ToolStripMenuItem("No windows found") { Enabled = false });
        }
        else
        {
            foreach (var window in windows.OrderBy(w => w.Title))
            {
                var isTopMost = _topMostWindows.Contains(window.Handle) || IsWindowTopMost(window.Handle);
                var menuItem = new ToolStripMenuItem(window.Title)
                {
                    Tag = window.Handle,
                    Checked = isTopMost
                };
                menuItem.Click += WindowMenuItem_Click;
                menu.Items.Add(menuItem);
            }
        }

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitApplication();
        menu.Items.Add(exitItem);

        // Show the menu at the cursor position
        menu.Show(Cursor.Position);
    }

    private void WindowMenuItem_Click(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem menuItem && menuItem.Tag is IntPtr hwnd)
        {
            ToggleAlwaysOnTop(hwnd);
        }
    }

    public void ToggleAlwaysOnTopForActiveWindow()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd != IntPtr.Zero)
        {
            ToggleAlwaysOnTop(hwnd);
        }
    }

    private void ToggleAlwaysOnTop(IntPtr hwnd)
    {
        bool isCurrentlyTopMost = IsWindowTopMost(hwnd);

        if (isCurrentlyTopMost)
        {
            // Remove topmost
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_NOTOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);
            _topMostWindows.Remove(hwnd);
            ShowNotification("Always On Top", "Window is no longer always on top");
        }
        else
        {
            // Set topmost
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);
            _topMostWindows.Add(hwnd);
            ShowNotification("Always On Top", "Window is now always on top");
        }
    }

    private static bool IsWindowTopMost(IntPtr hwnd)
    {
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        return (exStyle & NativeMethods.WS_EX_TOPMOST) != 0;
    }

    private void ShowNotification(string title, string message)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = message;
        _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(1000);
    }

    private static List<WindowInfo> GetOpenWindows()
    {
        var windows = new List<WindowInfo>();

        NativeMethods.EnumWindows((hwnd, lParam) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd))
                return true;

            int length = NativeMethods.GetWindowTextLength(hwnd);
            if (length == 0)
                return true;

            // Skip windows without a title or that are tool windows
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
                return true;

            var sb = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);

            string title = sb.ToString();
            if (!string.IsNullOrWhiteSpace(title))
            {
                windows.Add(new WindowInfo(hwnd, title));
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private void ExitApplication()
    {
        _hotkeyWindow.UnregisterHotkey();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeyWindow.Dispose();
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}

public record WindowInfo(IntPtr Handle, string Title);

public class HotkeyWindow : NativeWindow, IDisposable
{
    private const int HOTKEY_ID = 1;
    private const int WM_HOTKEY = 0x0312;
    private readonly TrayApplicationContext _context;
    private bool _isRegistered;

    public HotkeyWindow(TrayApplicationContext context)
    {
        _context = context;
        CreateHandle(new CreateParams());
    }

    public void RegisterHotkey()
    {
        // MOD_SHIFT = 0x0004, MOD_CONTROL = 0x0002, MOD_ALT = 0x0001
        // Combined: Shift + Ctrl + Alt = 0x0007
        const uint MOD_ALT = 0x0001;
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;
        const uint VK_T = 0x54;

        _isRegistered = NativeMethods.RegisterHotKey(Handle, HOTKEY_ID,
            MOD_ALT | MOD_CONTROL | MOD_SHIFT, VK_T);

        if (!_isRegistered)
        {
            MessageBox.Show("Failed to register hotkey (Shift+Ctrl+Alt+T). " +
                "It may be in use by another application.",
                "Always On Top", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public void UnregisterHotkey()
    {
        if (_isRegistered)
        {
            NativeMethods.UnregisterHotKey(Handle, HOTKEY_ID);
            _isRegistered = false;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            _context.ToggleAlwaysOnTopForActiveWindow();
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterHotkey();
        DestroyHandle();
    }
}

internal static class NativeMethods
{
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);

    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    public static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    public static extern int GetWindowLong(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
