namespace AccessControl.Infrastructure.Aero.Commands;

/// <summary>SDK commands used by the Gateway; the value is the SDK command number (SDK Specification).</summary>
public enum AeroCommand : short
{
    SystemLevelSpecification = 11,
    CreateChannel = 12,
    Shutdown = 203,
    TimeSet = 302,
    SetTransactionLogIndex = 303,
    AccessDatabaseSpecification = 1105,
    ScpDeviceSpecification = 1107,
    DaylightSavingTimeConfiguration = 1116,
}

public static class AeroCommandExtensions
{
    /// <summary>"1107 ScpDeviceSpecification" — how commands appear in logs.</summary>
    public static string Describe(this AeroCommand command) => $"{(short)command} {command}";
}
