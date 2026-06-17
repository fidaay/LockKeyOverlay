namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class RgbaStyleTests
{
    [TestMethod]
    public void FromOpacity_ClampsOpacityToByteRange()
    {
        RgbaStyle low = RgbaStyle.FromOpacity(1, 2, 3, -1);
        RgbaStyle high = RgbaStyle.FromOpacity(1, 2, 3, 2);

        Assert.AreEqual((byte)0, low.A);
        Assert.AreEqual((byte)255, high.A);
    }

    [TestMethod]
    public void OverlayOpacity_KeepsMinimumVisibleOpacity()
    {
        RgbaStyle transparent = new(1, 2, 3, 0);

        Assert.AreEqual(0.05, transparent.OverlayOpacity);
    }

    [TestMethod]
    public void UsesDarkForeground_DependsOnLuminance()
    {
        RgbaStyle light = new(255, 255, 255, 255);
        RgbaStyle dark = new(0, 0, 0, 255);

        Assert.IsTrue(light.UsesDarkForeground);
        Assert.IsFalse(dark.UsesDarkForeground);
    }
}
