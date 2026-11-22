// HID Aero SDK ↔ .NET 10 compatibility probe.
//
// Usage (the message pump runs until you press Q / Esc / Ctrl+C):
//   dotnet run                         offline checks + message pump
//   dotnet run -- --listen             also open the IP Client channel (44, port 3001) and wait for an X1100
//   dotnet run -- --listen --accept    additionally send 1107 when the ID report arrives (keeps the controller connected)
//   dotnet run -- --seconds 10         stop the pump automatically after 10 s instead of waiting for a key
//   dotnet run -p:AeroSdkVersion=4.8.1.3 -- ...   test another driver build from lib/HIDAero/<version>/
//
// Command values follow docs/PROJECT_SPEC.md §5.3 / §6.3.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using HID.Aero.ScpdNet.Wrapper;

var options = ProbeOptions.Parse(args);
var results = new List<(string Step, bool Passed, string Detail)>();

void Record(string step, bool passed, string detail)
{
    results.Add((step, passed, detail));
    var previousColor = Console.ForegroundColor;
    Console.ForegroundColor = passed ? ConsoleColor.Green : ConsoleColor.Red;
    Console.Write(passed ? "[PASS] " : "[FAIL] ");
    Console.ForegroundColor = previousColor;
    Console.WriteLine($"{step}: {detail}");
}

bool Run(string step, Func<string> action)
{
    try
    {
        Record(step, true, action());
        return true;
    }
    catch (Exception exception)
    {
        Record(step, false, $"{exception.GetType().Name}: {exception.Message}");
        return false;
    }
}

Console.WriteLine("=== HID Aero SDK compatibility probe ===");
Console.WriteLine();

// 1. Host environment
Run("Runtime", () =>
{
    if (!Environment.Is64BitProcess)
    {
        throw new PlatformNotSupportedException("Process is not 64-bit; HIDAeroNet64.dll requires x64.");
    }

    if (!OperatingSystem.IsWindows())
    {
        throw new PlatformNotSupportedException("The Aero driver is a Windows DLL.");
    }

    return $"{RuntimeInformation.FrameworkDescription}, {RuntimeInformation.ProcessArchitecture}, {RuntimeInformation.OSDescription}";
});

// 2. Managed wrapper
var baseDirectory = AppContext.BaseDirectory;
Run("Wrapper assembly", () =>
{
    var assembly = typeof(SCPDLL).Assembly;
    var targetFramework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? "(no TargetFramework attribute)";
    var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
    return $"{assembly.GetName().Name} {assembly.GetName().Version} (file {fileVersion}), targets {targetFramework}";
});

// 3. Native driver load
var nativePath = Path.Combine(baseDirectory, "HIDAeroNet64.dll");
var nativeLoaded = Run("Native driver load", () =>
{
    if (!File.Exists(nativePath))
    {
        throw new FileNotFoundException("HIDAeroNet64.dll is not next to the executable.", nativePath);
    }

    var handle = NativeLibrary.Load(nativePath);
    return $"HIDAeroNet64.dll {FileVersionInfo.GetVersionInfo(nativePath).FileVersion} loaded (handle 0x{handle:X})";
});

if (!nativeLoaded)
{
    return Summarize();
}

// 4. P/Invoke through the wrapper (simple scalar return)
Run("P/Invoke scpGetDllVersion", () =>
{
    // Packed as one byte per part: 0x04070129 = 4.7.1.41
    var version = SCPDLL.scpGetDllVersion();
    return $"{(version >> 24) & 0xFF}.{(version >> 16) & 0xFF}.{(version >> 8) & 0xFF}.{version & 0xFF} (0x{version:X8})";
});

// 5. Structure-based command: marshals CC_SYS through scpCfgCmndEx (command 11)
var driverInitialized = Run("Struct marshalling cmd 11 (CC_SYS)", () =>
{
    var system = new CC_SYS
    {
        nPorts = 512,
        nScps = 512,
        nTimezones = 0,
        nHolidays = 0,
        bDirectMode = 1,
    };

    if (!new SCPConfig().scpCfgCmndEx((short)enCfgCmnd.enCcSystem, system))
    {
        throw new InvalidOperationException($"scpCfgCmndEx returned false. Post error: {SCPDLL.scpGetPostError()}");
    }

    return "System level specification accepted by driver";
});

if (!driverInitialized)
{
    return Summarize();
}

// 6. String-based command: open the IP Client listening channel (command 12)
var channelOpened = false;
if (options.Listen)
{
    channelOpened = Run("String command cmd 12 (IP Client channel)", () =>
    {
        const string command = "012 44 7 3001 0 4000 10000 \"\" 0";
        if (!SCPDLL.scpConfigCommand(command))
        {
            throw new InvalidOperationException($"scpConfigCommand returned false. Post error: {SCPDLL.scpGetPostError()}");
        }

        return $"Listening on TCP 3001, channel 44 ({command})";
    });
}

// 7. Message pump: GetMessage marshals the large SCPReply union — the riskiest part for .NET (Core) compatibility.
var stopHint = options.AutoStopSeconds > 0
    ? $"stops automatically after {options.AutoStopSeconds}s (or press Q / Esc / Ctrl+C)"
    : "press Q / Esc / Ctrl+C to stop";
Console.WriteLine();
Console.WriteLine(channelOpened
    ? $"Waiting for an X1100 in IP Client mode (host IP = this PC, port 3001) — {stopHint}."
    : $"Message pump running — {stopHint}.");

var messageCount = 0;
var idReportSeen = false;
using var cancellation = options.AutoStopSeconds > 0
    ? new CancellationTokenSource(TimeSpan.FromSeconds(options.AutoStopSeconds))
    : new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var pumpOk = Run("Message pump (SCPReplyMessage.GetMessage)", () =>
{
    while (!cancellation.IsCancellationRequested)
    {
        if (StopKeyPressed())
        {
            break;
        }

        var message = new SCPReplyMessage();
        if (message.GetMessage())
        {
            messageCount++;
            Console.WriteLine($"    <- {Describe(message)}");

            if (message.ReplyType == (int)enSCPReplyType.enSCPReplyIDReport)
            {
                idReportSeen = true;
                if (options.Accept)
                {
                    AcceptController(message.SCPId);
                }
            }
        }

        Thread.Sleep(50);
    }

    return $"{messageCount} message(s) received without marshalling errors";
});

if (options.Listen)
{
    Record("Controller handshake", idReportSeen, idReportSeen
        ? "CommStatus + ID report received from controller"
        : "No ID report received (no controller connected, or controller not configured for IP Client to this host)");
}

// 8. Shutdown (command 203)
Run("Shutdown cmd 203", () =>
{
    SCPDLL.scpConfigCommand("203 0");
    return "Driver channels closed";
});

_ = pumpOk;
return Summarize();

int Summarize()
{
    Console.WriteLine();
    var failed = results.Count(result => !result.Passed);
    Console.WriteLine(failed == 0
        ? $"RESULT: COMPATIBLE — all {results.Count} checks passed."
        : $"RESULT: {failed} of {results.Count} checks failed.");
    return failed == 0 ? 0 : 1;
}

static bool StopKeyPressed()
{
    // Key polling only works with an interactive console; otherwise rely on Ctrl+C or --seconds.
    if (Console.IsInputRedirected || !Console.KeyAvailable)
    {
        return false;
    }

    return Console.ReadKey(intercept: true).Key is ConsoleKey.Q or ConsoleKey.Escape;
}

static string Describe(SCPReplyMessage message)
{
    return message.ReplyType switch
    {
        (int)enSCPReplyType.enSCPReplyCommStatus =>
            $"CommStatus SCP-{message.SCPId} channel {message.comm.nChannelId} status {message.comm.status} error {message.comm.error_code} primary {message.comm.previous_primary_comm}->{message.comm.current_primary_comm}",
        (int)enSCPReplyType.enSCPReplyIDReport =>
            $"IDReport SCP-{message.SCPId} device {message.id.device_id} fw {message.id.sft_rev_major}.{message.id.sft_rev_minor} serial {message.id.serial_number} scp_id {message.id.scp_id} tls {message.id.tls_status} dip {message.id.dip_switch_current} db {message.id.db_active}/{message.id.db_max}",
        (int)enSCPReplyType.enSCPReplyCmndStatus =>
            $"CmndStatus SCP-{message.SCPId} status {message.cmnd_sts.status} seq {message.cmnd_sts.sequence_number}",
        (int)enSCPReplyType.enSCPReplyNAK =>
            $"NAK SCP-{message.SCPId} reason {message.nak.reason} command {message.nak.command}",
        (int)enSCPReplyType.enSCPReplyTransaction =>
            $"Transaction SCP-{message.SCPId} #{message.tran.ser_num} [{message.tran.tran_type}:{message.tran.tran_code}] source {message.tran.source_type}-{message.tran.source_number}",
        _ => $"ReplyType {message.ReplyType} SCP-{message.SCPId}",
    };
}

static void AcceptController(int scpId)
{
    // Command 1107 within 15 s keeps an IP Client controller connected (docs/PROJECT_SPEC.md §5.3, values §6.3: UTC+3, no DST).
    var command = $"1107 0 {scpId} 0 0 0 0 3 60000 32 615 388 64 255 1000 1000 -10800 0 255 100 0 10000 0 1 0 0";
    var posted = SCPDLL.scpConfigCommand(command);
    Console.WriteLine($"    -> {command} ({(posted ? "posted" : "post failed")})");
}

/// <param name="AutoStopSeconds">0 = run the message pump until the user stops it.</param>
internal sealed record ProbeOptions(bool Listen, bool Accept, int AutoStopSeconds)
{
    public static ProbeOptions Parse(string[] args)
    {
        var autoStopSeconds = 0;
        var secondsIndex = Array.IndexOf(args, "--seconds");
        if (secondsIndex >= 0 && secondsIndex + 1 < args.Length && int.TryParse(args[secondsIndex + 1], out var seconds) && seconds > 0)
        {
            autoStopSeconds = seconds;
        }

        return new ProbeOptions(args.Contains("--listen"), args.Contains("--accept"), autoStopSeconds);
    }
}
