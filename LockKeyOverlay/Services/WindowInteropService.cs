using System.Runtime.InteropServices;

namespace LockKeyOverlay;

internal sealed class WindowInteropService
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);

    private readonly IntPtr _windowHandle;

    public WindowInteropService(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public ServiceResult ApplyToolWindowStyle()
    {
        return UpdateExtendedStyle(style => style | WS_EX_TOOLWINDOW, "Tool window style applied.");
    }

    public ServiceResult ApplyClickThrough(bool clickThrough)
    {
        return UpdateExtendedStyle(
            style => clickThrough ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT,
            "Click-through style applied.");
    }

    public ServiceResult ApplyTopMost(bool enabled)
    {
        if (_windowHandle == IntPtr.Zero)
            return ServiceResult.Failure("Window handle is not available.");

        bool applied = SetWindowPos(
            _windowHandle,
            enabled ? HWND_TOPMOST : HWND_NOTOPMOST,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        return applied
            ? ServiceResult.Success("Topmost state applied.")
            : ServiceResult.Failure("Topmost state could not be applied.", nativeErrorCode: Marshal.GetLastWin32Error());
    }

    private ServiceResult UpdateExtendedStyle(Func<int, int> update, string successMessage)
    {
        if (_windowHandle == IntPtr.Zero)
            return ServiceResult.Failure("Window handle is not available.");

        try
        {
            int exStyle = GetWindowLongPtrCompat(_windowHandle, GWL_EXSTYLE).ToInt32();
            int updatedExStyle = update(exStyle);

            SetLastError(0);
            IntPtr previous = SetWindowLongPtrCompat(_windowHandle, GWL_EXSTYLE, new IntPtr(updatedExStyle));
            int error = Marshal.GetLastWin32Error();

            if (previous == IntPtr.Zero && error != 0)
                return ServiceResult.Failure("Extended window style could not be updated.", nativeErrorCode: error);

            return ServiceResult.Success(successMessage);
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentException)
        {
            return ServiceResult.Failure("Extended window style could not be updated.", ex);
        }
    }

    private static IntPtr GetWindowLongPtrCompat(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtrCompat(IntPtr hWnd, int nIndex, IntPtr newValue)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, newValue)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, newValue.ToInt32()));
    }

    [DllImport("kernel32.dll")]
    private static extern void SetLastError(uint dwErrCode);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);
}
