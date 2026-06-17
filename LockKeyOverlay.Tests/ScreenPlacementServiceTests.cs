using System.Drawing;
using System.Runtime.InteropServices;

namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class ScreenPlacementServiceTests
{
    private static readonly ScreenSnapshot PrimaryScreen = new(
        @"\\.\DISPLAY1",
        new Rectangle(0, 0, 1920, 1080),
        new Rectangle(0, 0, 1920, 1040));

    private static readonly ScreenSnapshot SecondaryScreen = new(
        @"\\.\DISPLAY2",
        new Rectangle(1920, 0, 2560, 1440),
        new Rectangle(1920, 0, 2560, 1400));

    private static readonly ScreenSnapshot LeftScreen = new(
        @"\\.\DISPLAY3",
        new Rectangle(-2560, 0, 2560, 1440),
        new Rectangle(-2560, 0, 2560, 1400));

    [TestMethod]
    public void RestoreWindowPosition_UsesTargetDpiForLegacyDipCoordinates()
    {
        ScreenPlacementService service = CreateService();
        AppConfig config = new()
        {
            ScreenDeviceName = SecondaryScreen.DeviceName,
            Left = 1300,
            Top = 20
        };

        WindowPlacement placement = service.RestoreWindowPosition(
            config,
            [PrimaryScreen, SecondaryScreen],
            PrimaryScreen,
            DpiScale.One,
            windowWidthDip: 128,
            windowHeightDip: 32);

        Assert.AreEqual(1300, placement.Left, 0.001);
        Assert.AreEqual(20, placement.Top, 0.001);
    }

    [TestMethod]
    public void RestoreWindowPosition_ClampsPhysicalCoordinatesWithinTargetScreen()
    {
        ScreenPlacementService service = CreateService();
        AppConfig config = new()
        {
            ScreenDeviceName = SecondaryScreen.DeviceName,
            LeftPx = 5000,
            TopPx = 2000
        };

        WindowPlacement placement = service.RestoreWindowPosition(
            config,
            [PrimaryScreen, SecondaryScreen],
            PrimaryScreen,
            DpiScale.One,
            windowWidthDip: 128,
            windowHeightDip: 32);

        Assert.AreEqual(2858.666, placement.Left, 0.001);
        Assert.AreEqual(928, placement.Top, 0.001);
    }

    [TestMethod]
    public void CenterInScreen_UsesTargetDpiScale()
    {
        ScreenPlacementService service = CreateService();

        WindowPlacement placement = service.CenterInScreen(
            SecondaryScreen,
            DpiScale.One,
            windowWidthDip: 128,
            windowHeightDip: 32);

        Assert.AreEqual(2069.333, placement.Left, 0.001);
        Assert.AreEqual(450.666, placement.Top, 0.001);
    }

    [TestMethod]
    public void CaptureWindowPosition_PrefersScreenMatchingFallbackDpi()
    {
        ScreenPlacementService service = CreateService();

        WindowPositionSnapshot? snapshot = service.CaptureWindowPosition(
            leftDip: 1280,
            topDip: 100,
            [PrimaryScreen, SecondaryScreen],
            PrimaryScreen,
            new DpiScale(1.5, 1.5));

        Assert.IsNotNull(snapshot);
        Assert.AreEqual(SecondaryScreen.DeviceName, snapshot.Value.Screen.DeviceName);
        Assert.AreEqual(1920, snapshot.Value.TopLeftPx.X);
        Assert.AreEqual(150, snapshot.Value.TopLeftPx.Y);
    }

    [TestMethod]
    public void CaptureWindowPosition_UsesTargetDpiForNegativeMonitor()
    {
        ScreenPlacementService service = CreateService();

        WindowPositionSnapshot? snapshot = service.CaptureWindowPosition(
            leftDip: -1000,
            topDip: 100,
            [LeftScreen, PrimaryScreen],
            PrimaryScreen,
            new DpiScale(1.5, 1.5));

        Assert.IsNotNull(snapshot);
        Assert.AreEqual(LeftScreen.DeviceName, snapshot.Value.Screen.DeviceName);
        Assert.AreEqual(-1500, snapshot.Value.TopLeftPx.X);
        Assert.AreEqual(150, snapshot.Value.TopLeftPx.Y);
    }

    [TestMethod]
    public void MoveWindowByPhysicalDelta_ClampsInsideNegativeMonitorUsingTargetDpi()
    {
        ScreenPlacementService service = CreateService();

        WindowPlacement placement = service.MoveWindowByPhysicalDelta(
            startTopLeftPx: new Point(-1500, 150),
            startMousePx: new Point(-1400, 200),
            currentMousePx: new Point(-3000, 2000),
            [LeftScreen, PrimaryScreen],
            PrimaryScreen,
            DpiScale.One,
            windowWidthDip: 128,
            windowHeightDip: 32);

        Assert.AreEqual(-1706.666, placement.Left, 0.001);
        Assert.AreEqual(928, placement.Top, 0.001);
    }

    [TestMethod]
    public void RestoreWindowPosition_UsesFallbackDpiWhenProviderThrowsExpectedInteropException()
    {
        ScreenPlacementService service = new(_ => throw new COMException("DPI unavailable."));
        AppConfig config = new()
        {
            ScreenDeviceName = SecondaryScreen.DeviceName,
            LeftPx = 2500,
            TopPx = 300
        };

        WindowPlacement placement = service.RestoreWindowPosition(
            config,
            [PrimaryScreen, SecondaryScreen],
            PrimaryScreen,
            new DpiScale(1.25, 1.25),
            windowWidthDip: 128,
            windowHeightDip: 32);

        Assert.AreEqual(2000, placement.Left, 0.001);
        Assert.AreEqual(240, placement.Top, 0.001);
    }

    private static ScreenPlacementService CreateService()
    {
        return new ScreenPlacementService(screen =>
            screen.DeviceName == SecondaryScreen.DeviceName || screen.DeviceName == LeftScreen.DeviceName
                ? new DpiScale(1.5, 1.5)
                : DpiScale.One);
    }
}
