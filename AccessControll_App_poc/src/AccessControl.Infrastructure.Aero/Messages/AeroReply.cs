namespace AccessControl.Infrastructure.Aero.Messages;

/// <summary>
/// A reply from the driver, copied out of the wrapper's <c>SCPReplyMessage</c> so no HID type leaves
/// Infrastructure.Aero. <see cref="ScpNumber"/> is the SCP ID the driver assigned to the connection.
/// </summary>
public abstract record AeroReply(int ScpNumber, DateTimeOffset ReceivedAtUtc);

/// <summary>Driver-local connection status (reply type 2).</summary>
public sealed record CommStatusReply(
    int ScpNumber,
    DateTimeOffset ReceivedAtUtc,
    int ChannelId,
    int Status,
    int ErrorCode,
    int CurrentPrimaryComm,
    int PreviousPrimaryComm) : AeroReply(ScpNumber, ReceivedAtUtc)
{
    public const int StatusOk = 2;

    public bool IsOnline => Status == StatusOk;
}

/// <summary>Controller identity report (reply type 4).</summary>
public sealed record IdReportReply(
    int ScpNumber,
    DateTimeOffset ReceivedAtUtc,
    int DeviceId,
    int DeviceVersion,
    string FirmwareVersion,
    int SerialNumber,
    string MacAddress,
    int ProgrammedScpId,
    DateTimeOffset ControllerClockUtc,
    int CardCapacity,
    int CardsStored,
    int DipSwitchesAtPowerUp,
    int DipSwitchesCurrent,
    int TlsStatus,
    int FirmwareAdvisory,
    int OemCode) : AeroReply(ScpNumber, ReceivedAtUtc);

/// <summary>Delivery status of a posted command (reply type 15); <see cref="Tag"/> matches the posted tag.</summary>
public sealed record CommandStatusReply(int ScpNumber, DateTimeOffset ReceivedAtUtc, int Tag, int Status)
    : AeroReply(ScpNumber, ReceivedAtUtc)
{
    public const int StatusOk = 1;

    public bool IsOk => Status == StatusOk;
}

/// <summary>Command rejected by the controller (reply type 3).</summary>
public sealed record NakReply(int ScpNumber, DateTimeOffset ReceivedAtUtc, int Reason, int Data, int DescriptionCode, string CommandBytes)
    : AeroReply(ScpNumber, ReceivedAtUtc);

/// <summary>Controller event (reply type 7); notation [TransactionType:TransactionCode] as in SDK §11.10.</summary>
public sealed record TransactionReply(
    int ScpNumber,
    DateTimeOffset ReceivedAtUtc,
    int SerialNumber,
    DateTimeOffset OccurredAtUtc,
    int SourceType,
    int SourceNumber,
    int TransactionType,
    int TransactionCode) : AeroReply(ScpNumber, ReceivedAtUtc);

/// <summary>Any reply type the Gateway does not interpret yet (status replies, web config …).</summary>
public sealed record OtherReply(int ScpNumber, DateTimeOffset ReceivedAtUtc, int ReplyType) : AeroReply(ScpNumber, ReceivedAtUtc);
