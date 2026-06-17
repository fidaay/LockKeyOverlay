using System;
using System.Runtime.InteropServices;

namespace LockKeyOverlay.AsusAuraHelper;

internal static class Program
{
    private const uint DeviceControl2Arg = 0x100056;
    private const uint StaticEffect = 0x00;
    private const uint NoTempo = 0x00;

    private static int Main(string[] args)
    {
        if (args.Length == 4 &&
            string.Equals(args[0], "set-static", StringComparison.OrdinalIgnoreCase) &&
            TryParseColor(args[1], out uint red) &&
            TryParseColor(args[2], out uint green) &&
            TryParseColor(args[3], out uint blue))
        {
            return ExecuteWithWmi(() =>
            {
                int setResult = SetLaptopRgbBacklight(red, green, blue);
                Console.Out.WriteLine($"set={setResult}");
            });
        }

        if (args.Length == 2 &&
            string.Equals(args[0], "get-status", StringComparison.OrdinalIgnoreCase) &&
            TryParseUInt32(args[1], out uint deviceId))
        {
            return ExecuteWithWmi(() =>
            {
                AsWMI_NB_GetDeviceStatus(deviceId, out uint status);
                Console.Out.WriteLine($"device=0x{deviceId:X8}; status=0x{status:X8}; statusDec={status}");
            });
        }

        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  LockKeyOverlay.AsusAuraHelper.exe set-static <red> <green> <blue>");
        Console.Error.WriteLine("  LockKeyOverlay.AsusAuraHelper.exe get-status <device-id>");
        return 2;
    }

    private static int ExecuteWithWmi(Action action)
    {
        try
        {
            int openResult = AsWMI_Open();
            Console.Out.WriteLine($"open={openResult}");

            try
            {
                action();
            }
            finally
            {
                int closeResult = AsWMI_Close();
                Console.Out.WriteLine($"close={closeResult}");
            }

            return 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Console.Error.WriteLine(ex.Message);
            return 3;
        }
    }

    private static bool TryParseColor(string value, out uint color)
    {
        return uint.TryParse(value, out color) && color <= 255;
    }

    private static bool TryParseUInt32(string value, out uint number)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out number);

        return uint.TryParse(value, out number);
    }

    private static int SetLaptopRgbBacklight(uint red, uint green, uint blue)
    {
        uint redGreenEffect =
            (0x00010000u * red) +
            (0x01000000u * green) +
            (0x00000100u * StaticEffect) +
            0xb3u;
        uint blueTempo = blue + (0x0100u * NoTempo);

        return AsWMI_NB_DeviceControl_2arg(DeviceControl2Arg, redGreenEffect, blueTempo);
    }

    [DllImport("ACPIWMI.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int AsWMI_Open();

    [DllImport("ACPIWMI.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int AsWMI_Close();

    [DllImport("ACPIWMI.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int AsWMI_NB_DeviceControl_2arg(uint one, uint two, uint three);

    [DllImport("ACPIWMI.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void AsWMI_NB_GetDeviceStatus(uint deviceId, out uint status);
}
