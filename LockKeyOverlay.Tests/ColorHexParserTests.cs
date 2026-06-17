namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class ColorHexParserTests
{
    [TestMethod]
    public void TryParse_AcceptsRgbHexWithHash()
    {
        bool parsed = ColorHexParser.TryParse("#1E90FF", out ParsedHexColor color);

        Assert.IsTrue(parsed);
        Assert.AreEqual((byte)0x1E, color.R);
        Assert.AreEqual((byte)0x90, color.G);
        Assert.AreEqual((byte)0xFF, color.B);
        Assert.IsNull(color.A);
    }

    [TestMethod]
    public void TryParse_AcceptsRgbaHexWithoutHash()
    {
        bool parsed = ColorHexParser.TryParse("FF8C00D9", out ParsedHexColor color);

        Assert.IsTrue(parsed);
        Assert.AreEqual((byte)0xFF, color.R);
        Assert.AreEqual((byte)0x8C, color.G);
        Assert.AreEqual((byte)0x00, color.B);
        Assert.AreEqual((byte)0xD9, color.A);
    }

    [TestMethod]
    public void TryParse_RejectsInvalidHex()
    {
        bool parsed = ColorHexParser.TryParse("#ZZZZZZ", out _);

        Assert.IsFalse(parsed);
    }
}
