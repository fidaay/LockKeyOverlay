using System.Runtime.InteropServices;

namespace LockKeyOverlay;

internal sealed class ForegroundHookService : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private readonly WinEventDelegate _foregroundChangedProc;
    private IntPtr _foregroundEventHook = IntPtr.Zero;

    public ForegroundHookService()
    {
        _foregroundChangedProc = ForegroundChangedCallback;
    }

    public event EventHandler? ForegroundChanged;

    public ServiceResult Start()
    {
        if (_foregroundEventHook != IntPtr.Zero)
            return ServiceResult.Success("Foreground hook is already installed.");

        _foregroundEventHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND,
            EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _foregroundChangedProc,
            0,
            0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        if (_foregroundEventHook == IntPtr.Zero)
            return ServiceResult.Failure("Foreground hook could not be installed.", nativeErrorCode: Marshal.GetLastWin32Error());

        return ServiceResult.Success("Foreground hook installed.");
    }

    public ServiceResult Stop()
    {
        if (_foregroundEventHook == IntPtr.Zero)
            return ServiceResult.Success("Foreground hook is not installed.");

        bool removed = UnhookWinEvent(_foregroundEventHook);
        int error = Marshal.GetLastWin32Error();
        _foregroundEventHook = IntPtr.Zero;

        return removed
            ? ServiceResult.Success("Foreground hook removed.")
            : ServiceResult.Failure("Foreground hook could not be removed.", nativeErrorCode: error);
    }

    public void Dispose()
    {
        Stop();
    }

    private void ForegroundChangedCallback(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        EventInvocation.Raise(ForegroundChanged, this, EventArgs.Empty);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);
}
