using AccessControl.Domain.Entities;

namespace AccessControl.Infrastructure.Aero;

/// <summary>
/// Memory allocation values for commands 1107 and 1105 (docs/PROJECT_SPEC.md §6.3). The per-controller values
/// (events, cards, levels per card) come from <see cref="ScpAllocation.Default"/> so the Gateway and the stored
/// controller records cannot drift; step 4 switches to each controller's own stored allocation. Changing any value on
/// a configured controller requires command 301 (reset), which erases its database (spec §3.7 rule 4a).
/// </summary>
internal static class ControllerAllocationDefaults
{
    // Command 1107 — SCP Device Specification
    public const short Msp1Ports = 3;              // internal + RS-485 port 1 + port 2
    public static readonly int Transactions = ScpAllocation.Default.Transactions;   // HID recommends >= 60000
    public const short Sios = 32;
    public const short MonitorPoints = 615;        // X1100 maximum
    public const short ControlPoints = 388;        // X1100 maximum
    public const short AccessControlReaders = 64;  // X1100 maximum
    public const short AccessLevels = 255;
    public const short Triggers = 1000;
    public const short Procedures = 1000;
    public const short DaylightSavingId = 0;       // Saudi Arabia: no DST (spec §6.2)
    public const short TimeZones = 255;
    public const short Holidays = 100;
    public const short MonitorPointGroups = 0;
    public const int UnreportedTransactionLimit = 10000;
    public const short OperatingModes = 1;

    // Command 1105 — Access Database Specification
    public static readonly int Cards = ScpAllocation.Default.Cards;
    public static readonly short AccessLevelsPerCard = ScpAllocation.Default.AccessLevelsPerCard;   // removes the IDCube 8-groups-per-card limit
    public const short PinDigitsField = 68;        // cardIDSize 4 bytes, 4 PIN digits, no duress (Getting Started 04)
    public const short IssueCode = 1;
    public const short AntiPassbackLocation = 1;
    public const short ActivationDate = 2;         // date + time
    public const short DeactivationDate = 2;       // date + time
    public const short UserLevel = 1;
    public const short UseLimit = 1;
    public const short TimedAntiPassback = 1;
    public const short HostResponseTimeoutSeconds = 5;
    public const short EscortTimeoutSeconds = 15;
    public const short MultiCardTimeoutSeconds = 15;
    public const short AssetTimeoutSeconds = 10;
    public const short AccessExceptionList = 1;
    public const int FreeformFieldCount = 8;
}
