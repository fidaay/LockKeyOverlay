using System.Drawing;

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

    private static ScreenPlacementService CreateService()
    {
        return new ScreenPlacementService(screen =>
            screen.DeviceName == SecondaryScreen.DeviceName
                ? new DpiScale(1.5, 1.5)
                : DpiScale.One);
    }
}
