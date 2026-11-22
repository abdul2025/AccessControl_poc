using AccessControl.Infrastructure.Aero.Commands;
using HID.Aero.ScpdNet.Wrapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AccessControl.Infrastructure.Aero.Driver;

/// <summary>
/// Owns the driver's process-wide lifetime: command 11 (allocate) and 12 (IP Client listener) on start,
/// command 203 (shutdown) on stop (spec §5.3). Exactly one instance per process.
/// </summary>
public sealed partial class AeroDriver(IOptions<AeroOptions> options, ILogger<AeroDriver> logger)
{
    private readonly Lock _gate = new();
    private bool _started;

    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            var settings = options.Value;
            var fileVersion = NativeDriverGuard.EnsureLoadable();
            var version = SCPDLL.scpGetDllVersion();
            LogDriverLoaded(logger, fileVersion, FormatPackedVersion(version));

            if (!string.IsNullOrWhiteSpace(settings.DebugFilePath))
            {
                SCPDLL.scpDebugSetFile((int)enSCPDebugLevel.enSCPDebugToFile, settings.DebugFilePath);
                LogDebugFileEnabled(logger, settings.DebugFilePath);
            }

            Post(AeroCommand.SystemLevelSpecification, new CC_SYS
            {
                nPorts = (short)settings.MaxControllers,
                nScps = (short)settings.MaxControllers,
                nTimezones = 0,
                nHolidays = 0,
                bDirectMode = 1,
                debug_rq = 0,
            });

            Post(AeroCommand.CreateChannel, new CC_CHANNEL
            {
                nChannelId = (short)settings.ChannelId,
                cType = (short)settings.ChannelType,
                cPort = (short)settings.Port,
                baud_rate = 0,
                timer1 = settings.ReplyTimeoutMilliseconds,
                timer2 = settings.RetryIntervalMilliseconds,
                cModemId = null!,   // not used for TCP/IP; the HID samples pass null as well
                cRTSMode = 0,
            });

            _started = true;
            LogListening(logger, settings.Port, settings.ChannelId, settings.ChannelType);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_started)
            {
                return;
            }

            // The wrapper's enCfgCmnd has no named entry for 203, so this is the one string command (as in the HID samples).
            var accepted = SCPDLL.scpConfigCommand($"{(short)AeroCommand.Shutdown}");
            _started = false;
            LogShutdown(logger, accepted);
        }
    }

    private static void Post(AeroCommand command, IConfigCommand payload)
    {
        if (!new SCPConfig().scpCfgCmndEx((short)command, payload))
        {
            throw new InvalidOperationException(
                $"The HID Aero driver rejected command {command.Describe()} (post error {SCPDLL.scpGetPostError()}).");
        }
    }

    /// <summary>The driver packs its version one byte per part: 0x04070129 = 4.7.1.41.</summary>
    private static string FormatPackedVersion(int version) =>
        $"{(version >> 24) & 0xFF}.{(version >> 16) & 0xFF}.{(version >> 8) & 0xFF}.{version & 0xFF}";

    [LoggerMessage(Level = LogLevel.Information, Message = "HID Aero driver loaded: file {FileVersion}, API {ApiVersion}.")]
    private static partial void LogDriverLoaded(ILogger logger, string fileVersion, string apiVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "HID Aero driver debug file enabled: {Path}.")]
    private static partial void LogDebugFileEnabled(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Commands 11 and 12 accepted. Listening for IP Client controllers on TCP {Port}, channel {ChannelId}, cType {ChannelType}.")]
    private static partial void LogListening(ILogger logger, int port, int channelId, int channelType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Command 203 Shutdown sent (accepted: {Accepted}). Driver channels closed.")]
    private static partial void LogShutdown(ILogger logger, bool accepted);
}
