namespace AccessControl.Infrastructure.Aero.Messages;

/// <summary>Human-readable text for SDK codes, for logs (SDK Specification §11; AeroChat Description.cs).</summary>
public static class AeroCodes
{
    public static string CommStatus(int status) => status switch
    {
        0 => "Unknown",
        1 => "Communication failed",
        2 => "Communication OK",
        _ => $"Status {status}",
    };

    public static string CommError(int errorCode) => errorCode switch
    {
        0 => "No error",
        1 => "Timeout",
        2 => "Reply too long",
        3 => "Command too long",
        4 => "CRC error",
        5 => "Sequence number",
        6 => "Detached",
        7 => "Deleted",
        8 => "Cipher",
        20 => "Log on",
        21 => "No error (AES)",
        30 => "SCP type",
        _ => $"Error {errorCode}",
    };

    public static string CommandStatus(int status) => status switch
    {
        0 => "FAILED",
        1 => "OK",
        2 => "NAK",
        3 => "BAD COMMAND",
        _ => $"Status {status}",
    };

    public static string NakReason(int reason) => reason switch
    {
        0 => "Invalid packet header",
        4 => "Command content error",
        5 => "Requires password logon",
        6 => "Port in standby mode",
        7 => "Failed logon (password / encryption key)",
        8 => "Controller in degraded mode",
        _ => "Invalid command type",
    };
}
