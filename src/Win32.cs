using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace AstralFocus
{
    /// <summary>窗口前置所需的 Win32 调用。只做前台切换/还原/临时置顶，不修改游戏任何数据。</summary>
    internal static class Win32
    {
        public const int SW_RESTORE = 9;
        public const int SW_SHOW = 5;

        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_TOP = IntPtr.Zero;
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint GW_OWNER = 4;

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")] public static extern IntPtr GetFocus();

        [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        // PeekMessage 用于给轮询线程创建消息队列。没有消息队列的线程调用
        // AttachThreadInput 会失败（这是抢前台间歇性失败的主因）。
        [DllImport("user32.dll", EntryPoint = "PeekMessageW")]
        private static extern bool PeekMessageRaw(IntPtr lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        private const uint PM_NOREMOVE = 0x0000;
        private const byte VK_MENU = 0x12;      // ALT
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int MSG_BUFFER_SIZE = 128; // MSG 在 x64 上是 48 字节，留足余量

        [ThreadStatic] private static bool _queueReady;

        /// <summary>
        /// 确保当前线程有消息队列。AttachThreadInput / SetFocus 都要求调用线程
        /// 自己也有消息队列，否则调用失败。轮询线程默认没有，这里补上。
        /// </summary>
        public static void EnsureMessageQueue()
        {
            if (_queueReady) return;
            try
            {
                IntPtr buf = Marshal.AllocHGlobal(MSG_BUFFER_SIZE);
                try
                {
                    PeekMessageRaw(buf, IntPtr.Zero, 0, 0, PM_NOREMOVE);
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
            catch { }
            _queueReady = true;
        }

        /// <summary>
        /// 把窗口抢到前台。返回 true 表示窗口确实成为前台窗口。
        /// SetForegroundWindow 有前台锁限制：仅当前台进程或最近有用户输入时才允许调用。
        /// 这里用 AttachThreadInput 接管前台线程 + 模拟 ALT 键击"解冻"前台锁，并做多轮重试。
        /// </summary>
        public static bool ForceForeground(IntPtr hWnd, out bool restored)
        {
            restored = false;
            if (hWnd == IntPtr.Zero) return false;

            EnsureMessageQueue();

            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
                ShowWindowAsync(hWnd, SW_RESTORE);
                restored = true;
            }
            else
            {
                ShowWindow(hWnd, SW_SHOW);
            }

            if (GetForegroundWindow() == hWnd)
            {
                BringWindowToTop(hWnd);
                return true;
            }

            uint targetThread = GetWindowThreadProcessId(hWnd, out _);
            uint currentThread = GetCurrentThreadId();

            for (int attempt = 0; attempt < 3; attempt++)
            {
                var fgNow = GetForegroundWindow();
                if (fgNow == hWnd) return true;
                uint fgThread = GetWindowThreadProcessId(fgNow, out _);

                bool attachedTarget = false, attachedFg = false;
                try
                {
                    if (targetThread != 0 && targetThread != currentThread)
                        attachedTarget = AttachThreadInput(currentThread, targetThread, true);
                    if (fgThread != 0 && fgThread != currentThread && fgThread != targetThread)
                        attachedFg = AttachThreadInput(currentThread, fgThread, true);

                    // 解冻前台锁：模拟一次 ALT 键击，让系统认为刚发生过用户输入
                    keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    Thread.Sleep(attempt == 0 ? 1 : 4);

                    BringWindowToTop(hWnd);
                    SetForegroundWindow(hWnd);
                    if (GetForegroundWindow() == hWnd) return true;

                    // 退路 1：SetWindowPos 抬到最上层再抢（不带 NOACTIVATE，允许激活）
                    SetWindowPos(hWnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    SetForegroundWindow(hWnd);
                    if (GetForegroundWindow() == hWnd) return true;

                    // 退路 2：SwitchToThisWindow（未公开 API，但对前台锁更宽松）
                    SwitchToThisWindow(hWnd, true);
                    if (GetForegroundWindow() == hWnd) return true;

                    // 退路 3：把焦点直接给窗口
                    SetFocus(hWnd);
                    SetForegroundWindow(hWnd);
                    if (GetForegroundWindow() == hWnd) return true;
                }
                finally
                {
                    if (attachedFg) AttachThreadInput(currentThread, fgThread, false);
                    if (attachedTarget) AttachThreadInput(currentThread, targetThread, false);
                }

                if (attempt < 2) Thread.Sleep(15);
            }

            return GetForegroundWindow() == hWnd;
        }

        public static void SetTopMost(IntPtr hWnd, bool topMost)
        {
            if (hWnd == IntPtr.Zero) return;
            SetWindowPos(hWnd, topMost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }
}
