using System.Diagnostics;
using System.Windows;

namespace LockKeyOverlay;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstanceCoordinator;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppStartupOptions startupOptions = AppStartupOptions.Parse(e.Args);

        if (startupOptions.ShutdownExisting)
        {
            using SingleInstanceCoordinator shutdownCoordinator = SingleInstanceCoordinator.CreateDefault();
            shutdownCoordinator.SignalPrimaryShutdown();
            if (!shutdownCoordinator.WaitForPrimaryExit(TimeSpan.FromSeconds(10)))
                Debug.WriteLine("Existing LockKeyOverlay instance did not exit before timeout.");

            Shutdown();
            return;
        }

        _singleInstanceCoordinator = SingleInstanceCoordinator.CreateDefault();

        if (!_singleInstanceCoordinator.TryClaimPrimary())
        {
            _singleInstanceCoordinator.SignalPrimary();
            _singleInstanceCoordinator.Dispose();
            _singleInstanceCoordinator = null;
            Shutdown();
            return;
        }

        ConfigLoadResult startupConfigLoadResult = new ConfigService().Load();

        _mainWindow = new MainWindow(startupConfigLoadResult);
        MainWindow = _mainWindow;
        _mainWindow.Show();

        _singleInstanceCoordinator.ActivationRequested += SingleInstanceCoordinator_ActivationRequested;
        _singleInstanceCoordinator.ShutdownRequested += SingleInstanceCoordinator_ShutdownRequested;
        if (!_singleInstanceCoordinator.StartListening())
            Debug.WriteLine("Single-instance activation listener could not be started.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_singleInstanceCoordinator is not null)
        {
            _singleInstanceCoordinator.ActivationRequested -= SingleInstanceCoordinator_ActivationRequested;
            _singleInstanceCoordinator.ShutdownRequested -= SingleInstanceCoordinator_ShutdownRequested;
            _singleInstanceCoordinator.Dispose();
            _singleInstanceCoordinator = null;
        }

        base.OnExit(e);
    }

    private void SingleInstanceCoordinator_ActivationRequested(object? sender, EventArgs e)
    {
        DispatcherInvocation.TryBeginInvoke(Dispatcher, () => _mainWindow?.ShowFromExternalActivation());
    }

    private void SingleInstanceCoordinator_ShutdownRequested(object? sender, EventArgs e)
    {
        DispatcherInvocation.TryBeginInvoke(Dispatcher, () => _mainWindow?.ExitFromExternalShutdown());
    }
}
