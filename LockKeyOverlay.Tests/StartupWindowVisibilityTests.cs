namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class StartupWindowVisibilityTests
{
    [TestMethod]
    public void ShouldShowOnStartup_ReturnsFalseWhenLoadedConfigIsHidden()
    {
        ConfigLoadResult result = ConfigLoadResult.Loaded(new AppConfig { IsVisible = false });

        bool shouldShow = StartupWindowVisibility.ShouldShowOnStartup(result);

        Assert.IsFalse(shouldShow);
    }

    [TestMethod]
    public void ShouldShowOnStartup_ReturnsTrueWhenLoadedConfigIsVisible()
    {
        ConfigLoadResult result = ConfigLoadResult.Loaded(new AppConfig { IsVisible = true });

        bool shouldShow = StartupWindowVisibility.ShouldShowOnStartup(result);

        Assert.IsTrue(shouldShow);
    }

    [TestMethod]
    public void ShouldShowOnStartup_ReturnsTrueWhenConfigIsUnavailable()
    {
        bool shouldShow = StartupWindowVisibility.ShouldShowOnStartup(null);

        Assert.IsTrue(shouldShow);
    }
}
