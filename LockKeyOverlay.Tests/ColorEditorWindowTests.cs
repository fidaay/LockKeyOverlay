namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class ColorEditorWindowTests
{
    [TestMethod]
    public void ResolveSelectedAlpha_PreservesExplicitHexAlpha()
    {
        byte alpha = ColorEditorWindow.ResolveSelectedAlpha((byte)1, opacityPercent: 0);

        Assert.AreEqual((byte)1, alpha);
    }

    [TestMethod]
    public void ResolveSelectedAlpha_UsesSliderWhenHexAlphaIsNotExplicit()
    {
        byte alpha = ColorEditorWindow.ResolveSelectedAlpha(null, opacityPercent: 50);

        Assert.AreEqual((byte)128, alpha);
    }

    [TestMethod]
    public void ResolveSelectedAlpha_PreservesParsedRgbaHexAlpha()
    {
        bool parsed = ColorHexParser.TryParse("#11223301", out ParsedHexColor color);

        byte alpha = ColorEditorWindow.ResolveSelectedAlpha(color.A, opacityPercent: 0);

        Assert.IsTrue(parsed);
        Assert.AreEqual((byte)1, alpha);
    }
}
