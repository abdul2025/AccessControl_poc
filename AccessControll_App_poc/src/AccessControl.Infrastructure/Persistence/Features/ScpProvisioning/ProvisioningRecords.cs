using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Infrastructure.Persistence.Features.ScpProvisioning;

// Persistence-only records written by the Gateway (FEATURE-PATTERN: not every table is a domain entity). The Web only
// reads them. None of them carries a row version: the Gateway is their only writer.

/// <summary>One SDK command compiled for a published version, in send order (milestone 4b-2 fills these).</summary>
internal sealed class ScpConfigCommandRecord
{
    public const int TargetMaxLength = 300;
    public const int TextMaxLength = 1000;

    public Guid VersionId { get; set; }
    public int Order { get; set; }
    public short CommandNumber { get; set; }
    public string Target { get; set; } = null!;
    public string Text { get; set; } = null!;
}

/// <summary>One attempt by the Gateway to bring a controller to a published version.</summary>
internal sealed class ScpApplyRunRecord
{
    public const int ReasonMaxLength = 2000;

    public Guid Id { get; set; }
    public Guid ScpId { get; set; }
    public int Version { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public ScpApplyOutcome Outcome { get; set; }
    public int? FailedOrder { get; set; }
    public string? Reason { get; set; }
}

/// <summary>The result of one command sent during an apply run.</summary>
internal sealed class ScpApplyResultRecord
{
    public const int DetailMaxLength = 1000;

    public Guid RunId { get; set; }
    public int Order { get; set; }
    public short CommandNumber { get; set; }
    public string Target { get; set; } = null!;
    public int? Tag { get; set; }
    public ScpCommandResult Result { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset AtUtc { get; set; }
}

/// <summary>
/// What the Gateway knows about a controller's connection and configuration, one row per controller. Kept out of the
/// <c>Scps</c> row so Gateway updates never conflict with a user's open edit form.
/// </summary>
internal sealed class ScpConnectionStateRecord
{
    public const int FirmwareMaxLength = 20;
    public const int MacMaxLength = 17;
    public const int LastErrorMaxLength = 2000;

    public Guid ScpId { get; set; }
    public bool IsOnline { get; set; }
    public int? SessionScpNumber { get; set; }
    public DateTimeOffset? LastSeenAtUtc { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? MacAddress { get; set; }
    public int? ReportedScpId { get; set; }
    public double? ClockOffsetSeconds { get; set; }
    public DateTimeOffset? LastIdReportAtUtc { get; set; }

    /// <summary>First time the controller was matched by serial; from then on the serial is locked (spec §3.7 rule 4).</summary>
    public DateTimeOffset? MatchedAtUtc { get; set; }

    public int? AppliedVersion { get; set; }
    public ScpApplyState ApplyState { get; set; } = ScpApplyState.Idle;
    public string? LastError { get; set; }
}
