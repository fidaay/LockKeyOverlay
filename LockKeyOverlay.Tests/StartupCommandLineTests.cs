namespace LockKeyOverlay.Tests;

[TestClass]
public sealed class StartupCommandLineTests
{
    [TestMethod]
    public void Build_QuotesExecutablePath()
    {
        string commandLine = StartupCommandLine.Build(@"C:\Program Files\LockKeyOverlay\LockKeyOverlay.exe");

        Assert.AreEqual(@"""C:\Program Files\LockKeyOverlay\LockKeyOverlay.exe""", commandLine);
    }

    [TestMethod]
    public void TryParseExecutablePath_ParsesQuotedPathWithArguments()
    {
        bool parsed = StartupCommandLine.TryParseExecutablePath(
            @"""C:\Program Files\LockKeyOverlay\LockKeyOverlay.exe"" --minimized",
            out string executablePath);

        Assert.IsTrue(parsed);
        Assert.AreEqual(@"C:\Program Files\LockKeyOverlay\LockKeyOverlay.exe", executablePath);
    }

    [TestMethod]
    public void TryParseExecutablePath_ParsesUnquotedPathWithArguments()
    {
        bool parsed = StartupCommandLine.TryParseExecutablePath(
            @"C:\Program Files\LockKeyOverlay\LockKeyOverlay.exe --minimized",
            out string executablePath);

        Assert.IsTrue(parsed);
        Assert.AreEqual(@"C:\Program Files\LockKeyOverlay\LockKeyOverlay.exe", executablePath);
    }

    [TestMethod]
    public void TryParseExecutablePath_ReturnsFalseForEmptyValue()
    {
        bool parsed = StartupCommandLine.TryParseExecutablePath("   ", out string executablePath);

        Assert.IsFalse(parsed);
        Assert.AreEqual(string.Empty, executablePath);
    }

    [TestMethod]
    public void PathsEqual_NormalizesEquivalentPaths()
    {
        bool equal = StartupCommandLine.PathsEqual(
            @"C:\Program Files\LockKeyOverlay\..\LockKeyOverlay\LockKeyOverlay.exe",
            @"c:\program files\lockkeyoverlay\lockkeyoverlay.exe");

        Assert.IsTrue(equal);
    }
}
