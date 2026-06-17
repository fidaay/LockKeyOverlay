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

    [TestMethod]
    public void ResolveSelectedAlpha_PreservesExactAlphaWhenOpenedAndAcceptedWithoutOpacityChange()
    {
        byte alpha = ColorEditorWindow.ResolveSelectedAlpha((byte)254, opacityPercent: 100);

        Assert.AreEqual((byte)254, alpha);
    }

    [TestMethod]
    public void UpdateExplicitAlphaAfterSliderChange_PreservesAlphaWhenRgbSliderChanges()
    {
        byte? alpha = ColorEditorWindow.UpdateExplicitAlphaAfterSliderChange(
            changedOpacity: false,
            explicitHexAlpha: 1);

        Assert.AreEqual((byte)1, alpha);
    }

    [TestMethod]
    public void UpdateExplicitAlphaAfterSliderChange_ClearsAlphaWhenOpacitySliderChanges()
    {
        byte? alpha = ColorEditorWindow.UpdateExplicitAlphaAfterSliderChange(
            changedOpacity: true,
            explicitHexAlpha: 1);

        Assert.IsNull(alpha);
    }

    [TestMethod]
    public void BuildHexText_IncludesAlphaWhenPercentWouldLoseExactValue()
    {
        string text = ColorEditorWindow.BuildHexText(
            0x11,
            0x22,
            0x33,
            resolvedAlpha: 1,
            explicitHexAlpha: 1,
            opacityPercent: 0);

        Assert.AreEqual("#11223301", text);
    }

    [TestMethod]
    public void BuildHexText_OmitsAlphaWhenSliderPercentPreservesValue()
    {
        string text = ColorEditorWindow.BuildHexText(
            0x11,
            0x22,
            0x33,
            resolvedAlpha: 128,
            explicitHexAlpha: 128,
            opacityPercent: 50);

        Assert.AreEqual("#112233", text);
    }
}
