// lang: C#, file: OverlayService.cs, target: Windows 10+ (net6.0-windows10.0.19041.0)
// Self-contained overlay service. Creates its own hidden Win32 message window
// on a dedicated STA thread so the global hotkey works even when Roblox has focus.
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SherStrapp.UI.Elements.Overlay;

namespace SherStrapp.Services
{
    public static class OverlayService
    {
        // ── Win32 ──────────────────────────────────────────────────────────────
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll")]
        private static extern void PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX, ptY;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WNDCLASS
        {
            public uint style;
            [MarshalAs(UnmanagedType.FunctionPtr)] public WndProcDelegate lpfnWndProc;
            public int cbClsExtra, cbWndExtra;
            public IntPtr hInstance, hIcon, hCursor, hbrBackground;
            [MarshalAs(UnmanagedType.LPTStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPTStr)] public string lpszClassName;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

        private const int    WM_HOTKEY  = 0x0312;
        private const int    WM_DESTROY = 0x0002;
        private const uint   WM_QUIT    = 0x0012;
        private const int    HOTKEY_ID  = 0x534F;
        private const uint   MOD_ALT    = 0x0001;
        private const uint   MOD_CTRL   = 0x0002;
        private const uint   VK_X       = 0x58;
        private const IntPtr HWND_MESSAGE = (IntPtr)(-3);

        // ── State ──────────────────────────────────────────────────────────────
        private static Thread?        _thread;
        private static IntPtr         _hwnd = IntPtr.Zero;
        private static GameOverlay?   _overlay;
        // keep delegate alive so GC doesn't collect it
        private static WndProcDelegate? _wndProcDelegate;

        // ── Public API ─────────────────────────────────────────────────────────
        public static void Start()
        {
            if (_thread is { IsAlive: true }) return;

            _thread = new Thread(MessageLoop) { IsBackground = true, Name = "OverlayHotkeyThread" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public static void Stop()
        {
            if (_hwnd != IntPtr.Zero)
                PostMessage(_hwnd, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        public static bool IsOpen => _overlay?.IsVisible == true;

        // ── Hotkey message loop (runs on background STA thread) ────────────────
        private static void MessageLoop()
        {
            _wndProcDelegate = WndProc;

            var wc = new WNDCLASS
            {
                lpszClassName = "SherStrappOverlayHotkey",
                lpfnWndProc   = _wndProcDelegate,
            };
            RegisterClass(ref wc);

            _hwnd = CreateWindowEx(0, "SherStrappOverlayHotkey", "",
                                   0, 0, 0, 0, 0,
                                   HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (_hwnd == IntPtr.Zero) return;

            RegisterHotKey(_hwnd, HOTKEY_ID, MOD_CTRL | MOD_ALT, VK_X);

            while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            UnregisterHotKey(_hwnd, HOTKEY_ID);
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        private static IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            if (uMsg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                // marshal back onto the WPF UI thread
                Application.Current?.Dispatcher.Invoke(Toggle);
                return IntPtr.Zero;
            }
            if (uMsg == WM_DESTROY)
            {
                PostQuitMessage(0);
                return IntPtr.Zero;
            }
            return DefWindowProc(hWnd, uMsg, wParam, lParam);
        }

        // ── Toggle (always called on UI thread) ────────────────────────────────
        public static void Toggle()
        {
            if (_overlay is null || !_overlay.IsLoaded)
            {
                _overlay = new GameOverlay();
                _overlay.Closed += (_, _) => _overlay = null;
                _overlay.Show();
                return;
            }

            if (_overlay.IsVisible)
                _overlay.Hide();
            else
                _overlay.Show();
        }
    }
}
