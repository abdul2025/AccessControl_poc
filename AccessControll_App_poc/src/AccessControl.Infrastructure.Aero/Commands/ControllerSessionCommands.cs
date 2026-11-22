using HID.Aero.ScpdNet.Wrapper;
using static AccessControl.Infrastructure.Aero.ControllerAllocationDefaults;

namespace AccessControl.Infrastructure.Aero.Commands;

/// <summary>
/// Builds the SDK structures sent when a controller connection is accepted
/// (spec §5.3: 1107 → 1105 → 1116 → 302 → 303). Field meanings: SDK Specification §3.7, §10.3, §4.5, §4.4, §11.8.
/// </summary>
internal static class ControllerSessionCommands
{
    public static CC_SCP_SCP DeviceSpecification(short scpNumber, int gmtOffsetSeconds) => new()
    {
        lastModified = 0,
        number = scpNumber,
        nMsp1Port = Msp1Ports,
        nTransactions = Transactions,
        nSio = Sios,
        nMp = MonitorPoints,
        nCp = ControlPoints,
        nAcr = AccessControlReaders,
        nAlvl = AccessLevels,
        nTrgr = Triggers,
        nProc = Procedures,
        gmt_offset = gmtOffsetSeconds,
        nDstID = DaylightSavingId,
        nTz = TimeZones,
        nHol = Holidays,
        nMpg = MonitorPointGroups,
        nTranLimit = UnreportedTransactionLimit,
        nAuthModType = 0,
        nOperModes = OperatingModes,
        oper_type = 0,
        nLanguages = 0,
        nSrvcType = 0,
    };

    public static CC_SCP_ADBS AccessDatabaseSpecification(short scpNumber) => new()
    {
        lastModified = 0,
        nScpID = scpNumber,
        nCards = Cards,
        nAlvl = AccessLevelsPerCard,
        nPinDigits = PinDigitsField,
        bIssueCode = IssueCode,
        bApbLocation = AntiPassbackLocation,
        bActDate = ActivationDate,
        bDeactDate = DeactivationDate,
        bVacationDate = 0,
        bUpgradeDate = 0,
        bUserLevel = UserLevel,
        bUseLimit = UseLimit,
        bSupportTimedApb = TimedAntiPassback,
        nTz = 0,
        bAssetGroup = 0,
        nHostResponseTimeout = HostResponseTimeoutSeconds,
        nMxmTypeIndex = 0,
        nAlvlUse4Arq = 0,
        nFreeformBlockSize = 0,
        fldsFreeform = new flds_freeform[FreeformFieldCount],
        nEscortTimeout = EscortTimeoutSeconds,
        nMultiCardTimeout = MultiCardTimeoutSeconds,
        nAssetTimeout = AssetTimeoutSeconds,
        bAccExceptionList = AccessExceptionList,
        adbFlags = 0,
    };

    /// <summary>
    /// HID requires 1116 before the first 302 on every new IP Client connection. Saudi Arabia has no DST and
    /// 1107 sets nDstID = 0, so a single entry entirely in the past is sent. To be confirmed on the bench
    /// (spec §12); a rejection is logged and the sequence continues.
    /// </summary>
    public static CC_SCP_DAYLIGHT NoDaylightSaving(short scpNumber) => new()
    {
        nScpID = scpNumber,
        nSYear = 2000, nSMonth = 1, nSDay = 1, nSHh = 0, nSMm = 0, nSSs = 0,
        nEYear = 2000, nEMonth = 1, nEDay = 2, nEHh = 0, nEMm = 0, nESs = 0,
    };

    /// <summary>custom_time 0 = set the controller to the host's current UTC time.</summary>
    public static CC_TIME TimeSet(short scpNumber) => new() { scp_number = scpNumber, custom_time = 0 };

    /// <summary>-2 = enable transaction reporting from the last reported transaction.</summary>
    public static CC_TRANINDEX EnableTransactionReporting(short scpNumber) => new() { scp_number = scpNumber, tran_index = -2 };
}
