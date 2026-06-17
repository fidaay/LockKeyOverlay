namespace LockKeyOverlay;

internal readonly record struct AppStartupOptions(bool ShutdownExisting)
{
    public const string ShutdownExistingArgument = "--shutdown-existing";

    public static AppStartupOptions Parse(IEnumerable<string> arguments)
    {
        foreach (string argument in arguments)
        {
            if (string.Equals(argument, ShutdownExistingArgument, StringComparison.OrdinalIgnoreCase))
                return new AppStartupOptions(ShutdownExisting: true);
        }

        return new AppStartupOptions(ShutdownExisting: false);
    }
}
