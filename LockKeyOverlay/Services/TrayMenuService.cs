using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace LockKeyOverlay;

internal sealed class TrayMenuService : IDisposable
{
    private readonly Forms.ContextMenuStrip _trayMenu;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Drawing.Icon? _ownedIcon;
    private readonly Forms.ToolStripMenuItem _visibleMenuItem;
    private readonly Forms.ToolStripMenuItem _movementMenuItem;
    private readonly Forms.ToolStripMenuItem _topMostMenuItem;
    private readonly Forms.ToolStripMenuItem _runAtStartupMenuItem;

    private bool _suppressEvents;

    public TrayMenuService(bool runAtStartupEnabled, Drawing.Icon? icon = null)
    {
        _ownedIcon = icon;
        _trayMenu = new Forms.ContextMenuStrip();

        _visibleMenuItem = CreateCheckedItem("Ventana visible", checkedState: true);
        _movementMenuItem = CreateCheckedItem("Activar movimiento", checkedState: true);
        _topMostMenuItem = CreateCheckedItem("Siempre encima", checkedState: true);
        _runAtStartupMenuItem = CreateCheckedItem("Iniciar con Windows", runAtStartupEnabled);

        Forms.ToolStripMenuItem resetConfigMenuItem = new("Eliminar configuración...");
        Forms.ToolStripMenuItem activeColorMenuItem = new("Cambiar color estado activo...");
        Forms.ToolStripMenuItem inactiveColorMenuItem = new("Cambiar color estado inactivo...");
        Forms.ToolStripMenuItem exitMenuItem = new("Salir");

        _visibleMenuItem.CheckedChanged += (_, _) => RaiseIfAllowed(VisibleChanged);
        _movementMenuItem.CheckedChanged += (_, _) => RaiseIfAllowed(MovementChanged);
        _topMostMenuItem.CheckedChanged += (_, _) => RaiseIfAllowed(TopMostChanged);
        _runAtStartupMenuItem.CheckedChanged += (_, _) => RaiseIfAllowed(RunAtStartupChanged);
        resetConfigMenuItem.Click += (_, _) => RaiseIfAllowed(ResetConfigurationRequested);
        activeColorMenuItem.Click += (_, _) => RaiseIfAllowed(ActiveColorChangeRequested);
        inactiveColorMenuItem.Click += (_, _) => RaiseIfAllowed(InactiveColorChangeRequested);
        exitMenuItem.Click += (_, _) => RaiseIfAllowed(ExitRequested);

        _trayMenu.Items.Add(_visibleMenuItem);
        _trayMenu.Items.Add(_movementMenuItem);
        _trayMenu.Items.Add(_topMostMenuItem);
        _trayMenu.Items.Add(_runAtStartupMenuItem);
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayMenu.Items.Add(activeColorMenuItem);
        _trayMenu.Items.Add(inactiveColorMenuItem);
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayMenu.Items.Add(resetConfigMenuItem);
        _trayMenu.Items.Add(exitMenuItem);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _ownedIcon ?? Drawing.SystemIcons.Application,
            Text = "LockKeyOverlay",
            Visible = true,
            ContextMenuStrip = _trayMenu
        };

        _trayIcon.DoubleClick += (_, _) => IsVisibleChecked = !IsVisibleChecked;
    }

    public event EventHandler? VisibleChanged;
    public event EventHandler? MovementChanged;
    public event EventHandler? TopMostChanged;
    public event EventHandler? RunAtStartupChanged;
    public event EventHandler? ResetConfigurationRequested;
    public event EventHandler? ActiveColorChangeRequested;
    public event EventHandler? InactiveColorChangeRequested;
    public event EventHandler? ExitRequested;

    public bool IsVisibleChecked
    {
        get => _visibleMenuItem.Checked;
        set => SetChecked(_visibleMenuItem, value, raiseEvent: true);
    }

    public bool MovementEnabled
    {
        get => _movementMenuItem.Checked;
        set => SetChecked(_movementMenuItem, value, raiseEvent: true);
    }

    public bool TopMostEnabled
    {
        get => _topMostMenuItem.Checked;
        set => SetChecked(_topMostMenuItem, value, raiseEvent: true);
    }

    public bool RunAtStartupEnabled
    {
        get => _runAtStartupMenuItem.Checked;
        set => SetChecked(_runAtStartupMenuItem, value, raiseEvent: true);
    }

    public void SetVisibleCheckedSilently(bool checkedState)
    {
        SetChecked(_visibleMenuItem, checkedState, raiseEvent: false);
    }

    public void SetMovementEnabledSilently(bool checkedState)
    {
        SetChecked(_movementMenuItem, checkedState, raiseEvent: false);
    }

    public void SetTopMostEnabledSilently(bool checkedState)
    {
        SetChecked(_topMostMenuItem, checkedState, raiseEvent: false);
    }

    public void SetRunAtStartupEnabledSilently(bool checkedState)
    {
        SetChecked(_runAtStartupMenuItem, checkedState, raiseEvent: false);
    }

    public void HideIcon()
    {
        _trayIcon.Visible = false;
    }

    public void Dispose()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _ownedIcon?.Dispose();
        _trayMenu.Dispose();
    }

    private static Forms.ToolStripMenuItem CreateCheckedItem(string text, bool checkedState)
    {
        return new Forms.ToolStripMenuItem(text)
        {
            CheckOnClick = true,
            Checked = checkedState
        };
    }

    private void SetChecked(Forms.ToolStripMenuItem item, bool checkedState, bool raiseEvent)
    {
        if (item.Checked == checkedState)
            return;

        if (!raiseEvent)
            _suppressEvents = true;

        try
        {
            item.Checked = checkedState;
        }
        finally
        {
            if (!raiseEvent)
                _suppressEvents = false;
        }
    }

    private void RaiseIfAllowed(EventHandler? handler)
    {
        if (!_suppressEvents)
            handler?.Invoke(this, EventArgs.Empty);
    }
}
