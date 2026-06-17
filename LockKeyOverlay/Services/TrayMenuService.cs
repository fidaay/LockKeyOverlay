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
    private readonly Forms.ToolStripMenuItem _physicalNumLockBlinkMenuItem;
    private readonly Forms.ToolStripMenuItem _capsLockBlinkTargetMenuItem;
    private readonly Forms.ToolStripMenuItem _numLockBlinkTargetMenuItem;
    private readonly Forms.ToolStripMenuItem _scrollLockBlinkTargetMenuItem;

    private bool _suppressEvents;
    private PhysicalBlinkTargetKey _physicalBlinkTargetKey = PhysicalBlinkTargetKey.CapsLock;

    public TrayMenuService(bool runAtStartupEnabled, Drawing.Icon? icon = null)
    {
        _ownedIcon = icon;
        _trayMenu = new Forms.ContextMenuStrip();

        _visibleMenuItem = CreateCheckedItem("Ventana visible", checkedState: true);
        _movementMenuItem = CreateCheckedItem("Activar movimiento", checkedState: true);
        _topMostMenuItem = CreateCheckedItem("Siempre encima", checkedState: true);
        _runAtStartupMenuItem = CreateCheckedItem("Iniciar con Windows", runAtStartupEnabled);
        _physicalNumLockBlinkMenuItem = CreateCheckedItem("Parpadear LED físico con Num Lock activo", checkedState: false);
        Forms.ToolStripMenuItem physicalBlinkTargetMenuItem = new("LED físico usado");
        _capsLockBlinkTargetMenuItem = CreateRadioItem("Caps Lock");
        _numLockBlinkTargetMenuItem = CreateRadioItem("Num Lock");
        _scrollLockBlinkTargetMenuItem = CreateRadioItem("Scroll Lock");
        SetPhysicalBlinkTargetKeySilently(_physicalBlinkTargetKey);

        Forms.ToolStripMenuItem resetConfigMenuItem = new("Eliminar configuración...");
        Forms.ToolStripMenuItem activeColorMenuItem = new("Cambiar color estado activo...");
        Forms.ToolStripMenuItem inactiveColorMenuItem = new("Cambiar color estado inactivo...");
        Forms.ToolStripMenuItem exitMenuItem = new("Salir");

        _visibleMenuItem.CheckedChanged += (_, _) => RaiseIfAllowed(VisibleChanged);
        _movementMenuItem.CheckedChanged += (_, _) => RaiseIfAllowed(MovementChanged);
        _topMostMenuItem.CheckedChanged += (_, _) => RaiseIfAllowed(TopMostChanged);
        _runAtStartupMenuItem.CheckedChanged += (_, _) => RaiseIfAllowed(RunAtStartupChanged);
        _physicalNumLockBlinkMenuItem.CheckedChanged += (_, _) => RaiseIfAllowed(PhysicalNumLockBlinkChanged);
        _capsLockBlinkTargetMenuItem.Click += (_, _) => SetPhysicalBlinkTargetKey(PhysicalBlinkTargetKey.CapsLock, raiseEvent: true);
        _numLockBlinkTargetMenuItem.Click += (_, _) => SetPhysicalBlinkTargetKey(PhysicalBlinkTargetKey.NumLock, raiseEvent: true);
        _scrollLockBlinkTargetMenuItem.Click += (_, _) => SetPhysicalBlinkTargetKey(PhysicalBlinkTargetKey.ScrollLock, raiseEvent: true);
        resetConfigMenuItem.Click += (_, _) => RaiseIfAllowed(ResetConfigurationRequested);
        activeColorMenuItem.Click += (_, _) => RaiseIfAllowed(ActiveColorChangeRequested);
        inactiveColorMenuItem.Click += (_, _) => RaiseIfAllowed(InactiveColorChangeRequested);
        exitMenuItem.Click += (_, _) => RaiseIfAllowed(ExitRequested);

        physicalBlinkTargetMenuItem.DropDownItems.Add(_capsLockBlinkTargetMenuItem);
        physicalBlinkTargetMenuItem.DropDownItems.Add(_numLockBlinkTargetMenuItem);
        physicalBlinkTargetMenuItem.DropDownItems.Add(_scrollLockBlinkTargetMenuItem);

        _trayMenu.Items.Add(_visibleMenuItem);
        _trayMenu.Items.Add(_movementMenuItem);
        _trayMenu.Items.Add(_topMostMenuItem);
        _trayMenu.Items.Add(_runAtStartupMenuItem);
        _trayMenu.Items.Add(_physicalNumLockBlinkMenuItem);
        _trayMenu.Items.Add(physicalBlinkTargetMenuItem);
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
    public event EventHandler? PhysicalNumLockBlinkChanged;
    public event EventHandler? PhysicalBlinkTargetChanged;
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

    public bool PhysicalNumLockBlinkWhenOnEnabled
    {
        get => _physicalNumLockBlinkMenuItem.Checked;
        set => SetChecked(_physicalNumLockBlinkMenuItem, value, raiseEvent: true);
    }

    public PhysicalBlinkTargetKey PhysicalBlinkTargetKey
    {
        get => _physicalBlinkTargetKey;
        set => SetPhysicalBlinkTargetKey(value, raiseEvent: true);
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

    public void SetPhysicalNumLockBlinkWhenOnEnabledSilently(bool checkedState)
    {
        SetChecked(_physicalNumLockBlinkMenuItem, checkedState, raiseEvent: false);
    }

    public void SetPhysicalBlinkTargetKeySilently(PhysicalBlinkTargetKey targetKey)
    {
        SetPhysicalBlinkTargetKey(targetKey, raiseEvent: false);
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

    private static Forms.ToolStripMenuItem CreateRadioItem(string text)
    {
        return new Forms.ToolStripMenuItem(text)
        {
            CheckOnClick = false
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

    private void SetPhysicalBlinkTargetKey(PhysicalBlinkTargetKey targetKey, bool raiseEvent)
    {
        if (!Enum.IsDefined(targetKey))
            targetKey = PhysicalBlinkTargetKey.CapsLock;

        if (_physicalBlinkTargetKey == targetKey &&
            _capsLockBlinkTargetMenuItem.Checked == (targetKey == PhysicalBlinkTargetKey.CapsLock) &&
            _numLockBlinkTargetMenuItem.Checked == (targetKey == PhysicalBlinkTargetKey.NumLock) &&
            _scrollLockBlinkTargetMenuItem.Checked == (targetKey == PhysicalBlinkTargetKey.ScrollLock))
        {
            return;
        }

        if (!raiseEvent)
            _suppressEvents = true;

        try
        {
            _physicalBlinkTargetKey = targetKey;
            _capsLockBlinkTargetMenuItem.Checked = targetKey == PhysicalBlinkTargetKey.CapsLock;
            _numLockBlinkTargetMenuItem.Checked = targetKey == PhysicalBlinkTargetKey.NumLock;
            _scrollLockBlinkTargetMenuItem.Checked = targetKey == PhysicalBlinkTargetKey.ScrollLock;
        }
        finally
        {
            if (!raiseEvent)
                _suppressEvents = false;
        }

        if (raiseEvent)
            RaiseIfAllowed(PhysicalBlinkTargetChanged);
    }

    private void RaiseIfAllowed(EventHandler? handler)
    {
        if (!_suppressEvents)
            handler?.Invoke(this, EventArgs.Empty);
    }
}
