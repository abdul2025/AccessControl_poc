using AccessControl.Infrastructure.Aero.Commands;
using AccessControl.Infrastructure.Aero.Driver;
using AccessControl.Infrastructure.Aero.Messages;
using HID.Aero.ScpdNet.Wrapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AccessControl.Infrastructure.Aero.Sessions;

/// <summary>
/// Accepts an IP Client controller on the first ID report of a connection (spec §5.3):
/// 1107 → 1105 → 1116 → 302 → 303. Without 1107 within the driver's provisioning time (15 s) the
/// driver detaches the controller. Until controllers are registered in the database (step 4), acceptance is
/// governed by <see cref="AeroOptions.AutoAcceptControllers"/>.
/// </summary>
public sealed partial class ControllerAcceptance(
    AeroCommandWriter writer,
    ControllerSessionTracker sessions,
    IOptions<AeroOptions> options,
    ILogger<ControllerAcceptance> logger)
{
    public void OnIdReport(IdReportReply report)
    {
        if (!sessions.TryBeginAcceptance(report.ScpNumber))
        {
            return;
        }

        if (!options.Value.AutoAcceptControllers)
        {
            LogPending(logger, report.ScpNumber, report.SerialNumber, report.DeviceId, report.FirmwareVersion, report.MacAddress);
            return;
        }

        var scp = (short)report.ScpNumber;
        (AeroCommand Command, IConfigCommand Payload)[] sequence =
        [
            (AeroCommand.ScpDeviceSpecification, ControllerSessionCommands.DeviceSpecification(scp, options.Value.GmtOffsetSeconds)),
            (AeroCommand.AccessDatabaseSpecification, ControllerSessionCommands.AccessDatabaseSpecification(scp)),
            (AeroCommand.DaylightSavingTimeConfiguration, ControllerSessionCommands.NoDaylightSaving(scp)),
            (AeroCommand.TimeSet, ControllerSessionCommands.TimeSet(scp)),
            (AeroCommand.SetTransactionLogIndex, ControllerSessionCommands.EnableTransactionReporting(scp)),
        ];

        LogAccepting(logger, report.ScpNumber, report.SerialNumber);
        foreach (var (command, payload) in sequence)
        {
            var posted = writer.Post(scp, command, payload);
            if (posted.IsFailure)
            {
                // Later commands depend on earlier ones (1105 must follow 1107), so stop at the first failure.
                LogPostFailed(logger, report.ScpNumber, command.Describe(), posted.ErrorMessage!);
                return;
            }

            LogPosted(logger, report.ScpNumber, command.Describe(), posted.Value);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SCP {ScpNumber} is PENDING (not accepted): serial {SerialNumber}, device {DeviceId}, firmware {Firmware}, MAC {Mac}. Set Aero:AutoAcceptControllers (development only) to accept it; the driver detaches it after its provisioning time.")]
    private static partial void LogPending(ILogger logger, int scpNumber, int serialNumber, int deviceId, string firmware, string mac);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCP {ScpNumber} (serial {SerialNumber}): accepting connection — posting 1107, 1105, 1116, 302, 303.")]
    private static partial void LogAccepting(ILogger logger, int scpNumber, int serialNumber);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCP {ScpNumber}: posted {Command} (tag {Tag}).")]
    private static partial void LogPosted(ILogger logger, int scpNumber, string command, int tag);

    [LoggerMessage(Level = LogLevel.Error, Message = "SCP {ScpNumber}: {Command} could not be posted — acceptance stopped. {Reason}")]
    private static partial void LogPostFailed(ILogger logger, int scpNumber, string command, string reason);
}
