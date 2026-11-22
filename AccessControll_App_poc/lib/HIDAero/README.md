# HID Aero SDK binaries (not in Git)

The HID driver DLLs are HID Global binaries and are **not committed**. After cloning, copy them here:

```
lib/HIDAero/<sdk-version>/HIDAeroNet64.dll     native driver
lib/HIDAero/<sdk-version>/HIDAeroWrap64.dll    .NET wrapper
```

Current version: **4.7.1.41** (driver 4.7.1.41 + wrapper 4.7.1.32, from the AeroChat 1.4.15 sample in the HID SDK package).
Both files must come from the same SDK package. The build stops with a clear error if they are missing.
Select another version with `dotnet build -p:AeroSdkVersion=<version>` (see `Directory.Build.props`).
