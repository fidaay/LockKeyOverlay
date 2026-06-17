using System.Runtime.InteropServices;

namespace LockKeyOverlay;

internal sealed class KeyboardHookService : IDisposable
{
    private const int VK_NUMLOCK = 0x90;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;

    private readonly LowLevelKeyboardProc _keyboardProc;
    private IntPtr _keyboardHook = IntPtr.Zero;

    public KeyboardHookService()
    {
        _keyboardProc = HookCallback;
    }

    public event EventHandler? NumLockReleased;

    public static bool IsNumLockOn()
    {
        return (GetKeyState(VK_NUMLOCK) & 1) != 0;
    }

    public ServiceResult Start()
    {
        if (_keyboardHook != IntPtr.Zero)
            return ServiceResult.Success("Keyboard hook is already installed.");

        IntPtr moduleHandle = GetModuleHandle(null);

        _keyboardHook = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            _keyboardProc,
            moduleHandle,
            0);

        if (_keyboardHook == IntPtr.Zero)
            return ServiceResult.Failure("Keyboard hook could not be installed.", nativeErrorCode: Marshal.GetLastWin32Error());

        return ServiceResult.Success("Keyboard hook installed.");
    }

    public ServiceResult Stop()
    {
        if (_keyboardHook == IntPtr.Zero)
            return ServiceResult.Success("Keyboard hook is not installed.");

        bool removed = UnhookWindowsHookEx(_keyboardHook);
        int error = Marshal.GetLastWin32Error();
        _keyboardHook = IntPtr.Zero;

        return removed
            ? ServiceResult.Success("Keyboard hook removed.")
            : ServiceResult.Failure("Keyboard hook could not be removed.", nativeErrorCode: error);
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = wParam.ToInt32();

            if (message == WM_KEYUP || message == WM_SYSKEYUP)
            {
                KbdLlHookStruct keyInfo = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);

                if (keyInfo.vkCode == VK_NUMLOCK)
                    EventInvocation.Raise(NumLockReleased, this, EventArgs.Empty);
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }
}
