using AccessControl.Infrastructure.Aero;
using AccessControl.Infrastructure.Aero.Commands;
using AccessControl.Infrastructure.Aero.Driver;
using AccessControl.Infrastructure.Aero.Messages;
using AccessControl.Infrastructure.Aero.Sessions;
using Microsoft.Extensions.Options;

namespace AccessControl.Gateway.Services;

/// <summary>
/// Consumes driver replies: tracks controller sessions, accepts connections, matches command statuses to
/// posted commands and logs every reply as structured data. Step 2 replaces the logging with persistence.
/// </summary>
internal sealed partial class ReplyProcessingService(
    AeroMessagePump pump,
    ControllerSessionTracker sessions,
    ControllerAcceptance acceptance,
    PendingCommandRegistry pendingCommands,
    IOptions<AeroOptions> options,
    TimeProvider timeProvider,
    ILogger<ReplyProcessingService> logger) : BackgroundService
{
    private static readonly TimeSpan TimeoutSweepInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sweep = SweepUnansweredCommandsAsync(stoppingToken);

        try
        {
            await foreach (var reply in pump.Replies.ReadAllAsync(stoppingToken))
            {
                Handle(reply);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is stopping.
        }

        await sweep;
    }

    private void Handle(AeroReply reply)
    {
        switch (reply)
        {
            case CommStatusReply status:
                var newSession = sessions.OnCommStatus(status);
                LogCommStatus(logger, status.ScpNumber, status.ChannelId, AeroCodes.CommStatus(status.Status),
                    AeroCodes.CommError(status.ErrorCode), status.PreviousPrimaryComm, status.CurrentPrimaryComm, newSession);
                break;

            case IdReportReply report:
                var clockOffset = report.ControllerClockUtc - report.ReceivedAtUtc;
                LogIdReport(logger, report.ScpNumber, report.SerialNumber, report.DeviceId, report.DeviceVersion,
                    report.FirmwareVersion, report.MacAddress, report.ProgrammedScpId, report.CardsStored, report.CardCapacity,
                    report.DipSwitchesCurrent, report.TlsStatus, report.FirmwareAdvisory, clockOffset.TotalSeconds);
                acceptance.OnIdReport(report);
                break;

            case CommandStatusReply commandStatus:
                var posted = pendingCommands.Complete(commandStatus.ScpNumber, commandStatus.Tag);
                var commandName = posted?.Command.Describe() ?? "(not posted by the Gateway)";
                if (commandStatus.IsOk)
                {
                    LogCommandOk(logger, commandStatus.ScpNumber, commandName, commandStatus.Tag);
                }
                else
                {
                    LogCommandNotOk(logger, commandStatus.ScpNumber, commandName, commandStatus.Tag, AeroCodes.CommandStatus(commandStatus.Status));
                }

                break;

            case NakReply nak:
                LogNak(logger, nak.ScpNumber, AeroCodes.NakReason(nak.Reason), nak.Reason, nak.DescriptionCode, nak.Data, nak.CommandBytes);
                break;

            case TransactionReply transaction:
                LogTransaction(logger, transaction.ScpNumber, transaction.SerialNumber, transaction.TransactionType,
                    transaction.TransactionCode, transaction.SourceType, transaction.SourceNumber, transaction.OccurredAtUtc);
                break;

            case OtherReply other:
                LogOtherReply(logger, other.ScpNumber, other.ReplyType);
                break;
        }
    }

    private async Task SweepUnansweredCommandsAsync(CancellationToken stoppingToken)
    {
        var timeout = TimeSpan.FromSeconds(options.Value.CommandStatusTimeoutSeconds);
        using var timer = new PeriodicTimer(TimeoutSweepInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var command in pendingCommands.RemoveExpired(timeProvider.GetUtcNow(), timeout))
                {
                    LogCommandUnanswered(logger, command.ScpNumber, command.Command.Describe(), command.Tag, timeout.TotalSeconds);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is stopping.
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SCP {ScpNumber} comm status: {Status} ({Error}), channel {ChannelId}, primary {PreviousPrimary} -> {CurrentPrimary}, new session {NewSession}.")]
    private static partial void LogCommStatus(ILogger logger, int scpNumber, int channelId, string status, string error,
        int previousPrimary, int currentPrimary, bool newSession);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SCP {ScpNumber} ID report: serial {SerialNumber}, device {DeviceId} v{DeviceVersion}, firmware {Firmware}, MAC {Mac}, programmed SCP ID {ProgrammedScpId}, cards {CardsStored}/{CardCapacity}, DIP {DipSwitches}, TLS {TlsStatus}, firmware advisory {FirmwareAdvisory}, clock offset {ClockOffsetSeconds:F0}s.")]
    private static partial void LogIdReport(ILogger logger, int scpNumber, int serialNumber, int deviceId, int deviceVersion,
        string firmware, string mac, int programmedScpId, int cardsStored, int cardCapacity, int dipSwitches, int tlsStatus,
        int firmwareAdvisory, double clockOffsetSeconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCP {ScpNumber}: {Command} (tag {Tag}) OK.")]
    private static partial void LogCommandOk(ILogger logger, int scpNumber, string command, int tag);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SCP {ScpNumber}: {Command} (tag {Tag}) {Status}.")]
    private static partial void LogCommandNotOk(ILogger logger, int scpNumber, string command, int tag, string status);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SCP {ScpNumber} NAK: {Reason} (reason {ReasonCode}, description {DescriptionCode}, data {Data}), command bytes {CommandBytes}.")]
    private static partial void LogNak(ILogger logger, int scpNumber, string reason, int reasonCode, int descriptionCode, int data, string commandBytes);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SCP {ScpNumber} transaction #{SerialNumber} [{TransactionType}:{TransactionCode}] source {SourceType}-{SourceNumber} at {OccurredAtUtc:u}.")]
    private static partial void LogTransaction(ILogger logger, int scpNumber, int serialNumber, int transactionType,
        int transactionCode, int sourceType, int sourceNumber, DateTimeOffset occurredAtUtc);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SCP {ScpNumber}: reply type {ReplyType} received (not interpreted yet).")]
    private static partial void LogOtherReply(ILogger logger, int scpNumber, int replyType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SCP {ScpNumber}: {Command} (tag {Tag}) received no command status within {TimeoutSeconds}s.")]
    private static partial void LogCommandUnanswered(ILogger logger, int scpNumber, string command, int tag, double timeoutSeconds);
}
