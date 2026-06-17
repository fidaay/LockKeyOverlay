using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace LockKeyOverlay;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstanceCoordinator;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceCoordinator = SingleInstanceCoordinator.CreateDefault();

        if (!_singleInstanceCoordinator.TryClaimPrimary())
        {
            _singleInstanceCoordinator.SignalPrimary();
            _singleInstanceCoordinator.Dispose();
            _singleInstanceCoordinator = null;
            Shutdown();
            return;
        }

        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;
        _mainWindow.Show();

        _singleInstanceCoordinator.ActivationRequested += SingleInstanceCoordinator_ActivationRequested;
        if (!_singleInstanceCoordinator.StartListening())
            Debug.WriteLine("Single-instance activation listener could not be started.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_singleInstanceCoordinator is not null)
        {
            _singleInstanceCoordinator.ActivationRequested -= SingleInstanceCoordinator_ActivationRequested;
            _singleInstanceCoordinator.Dispose();
            _singleInstanceCoordinator = null;
        }

        base.OnExit(e);
    }

    private void SingleInstanceCoordinator_ActivationRequested(object? sender, EventArgs e)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        Dispatcher.BeginInvoke(
            () => _mainWindow?.ShowFromExternalActivation(),
            DispatcherPriority.Normal);
    }
}
