using System.ComponentModel.DataAnnotations;

namespace AccessControl.Infrastructure.Aero;

/// <summary>
/// Host-side driver settings (configuration section <c>Aero</c>). Defaults are the IP Client values in
/// docs/PROJECT_SPEC.md §5.3: one listening channel for every controller, TLS when the controller accepts it.
/// </summary>
public sealed class AeroOptions
{
    public const string SectionName = "Aero";

    /// <summary>Command 11 <c>nPorts</c>/<c>nScps</c>: maximum channels and controllers the driver allocates.</summary>
    [Range(1, 1024)]
    public int MaxControllers { get; set; } = 512;

    /// <summary>Command 12 <c>nChannelId</c> of the IP Client listening channel.</summary>
    [Range(1, short.MaxValue)]
    public int ChannelId { get; set; } = 44;

    /// <summary>
    /// Command 12 <c>cType</c> for an IP Client listener (SDK §5.3): 7 = TLS if the controller accepts it (POC),
    /// 263 = TLS required, 519 = certificate verification, 775 = TLS required + certificate verification,
    /// 1287 = mutual TLS. The IP Server values (4/260/516/772/1284) are not valid here.
    /// </summary>
    [AllowedValues(7, 263, 519, 775, 1287)]
    public int ChannelType { get; set; } = 7;

    /// <summary>TCP port the driver listens on for controllers.</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 3001;

    /// <summary>Command 12 <c>timer1</c>: milliseconds to wait for a controller reply.</summary>
    [Range(500, 60000)]
    public int ReplyTimeoutMilliseconds { get; set; } = 3000;

    /// <summary>Command 12 <c>timer2</c>: milliseconds between TCP reconnect attempts.</summary>
    [Range(1000, 300000)]
    public int RetryIntervalMilliseconds { get; set; } = 10000;

    /// <summary>
    /// Command 1107 <c>gmt_offset</c> in seconds, sign as the SDK expects: -(UTC offset).
    /// Saudi Arabia (UTC+03:00, no DST) = -10800 (spec §6.2).
    /// </summary>
    [Range(-50400, 43200)]
    public int GmtOffsetSeconds { get; set; } = -10800;

    /// <summary>Optional driver debug log file; empty disables it.</summary>
    public string? DebugFilePath { get; set; }

    /// <summary>
    /// Temporary until controllers are registered in the database (spec §3.6 step 4): when true, every
    /// IP Client controller that connects is accepted (1107 …). When false, connections are only logged as
    /// pending and the driver detaches them after its provisioning time (spec §3.7 rule 4).
    /// Enable only on a bench / development machine.
    /// </summary>
    public bool AutoAcceptControllers { get; set; }

    /// <summary>Seconds to wait for a command status reply before a posted command is reported as unanswered.</summary>
    [Range(5, 300)]
    public int CommandStatusTimeoutSeconds { get; set; } = 30;
}
