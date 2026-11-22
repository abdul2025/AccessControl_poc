using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AccessControl.Infrastructure.Aero.Driver;

/// <summary>
/// Turns the usual native-driver failures (wrong bitness, missing DLL) into one clear startup error
/// instead of a BadImageFormatException / DllNotFoundException on the first SDK call (spec §3.5 rule 8).
/// </summary>
internal static class NativeDriverGuard
{
    public const string NativeDriverFileName = "HIDAeroNet64.dll";

    /// <returns>The native driver's file version.</returns>
    public static string EnsureLoadable()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The HID Aero driver is a Windows DLL; the Gateway must run on Windows.");
        }

        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException($"{NativeDriverFileName} requires a 64-bit (x64) process.");
        }

        var path = Path.Combine(AppContext.BaseDirectory, NativeDriverFileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"{NativeDriverFileName} was not found next to the Gateway executable. It is copied from lib/HIDAero/<AeroSdkVersion>/ at build time.",
                path);
        }

        if (!NativeLibrary.TryLoad(path, out _))
        {
            throw new InvalidOperationException($"{NativeDriverFileName} exists but could not be loaded: {path}");
        }

        return FileVersionInfo.GetVersionInfo(path).FileVersion ?? "unknown";
    }
}
