using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Application.Features.Scps.DTOs;

/// <summary>The latest published version of a controller; compile fields are filled in by the Gateway.</summary>
public sealed record ScpVersionSummaryDto(
    int Version,
    DateTimeOffset PublishedAtUtc,
    string? PublishedBy,
    string SnapshotHash,
    DateTimeOffset? CompiledAtUtc,
    string? CompileError,
    int CommandCount);

/// <summary>What the Gateway last reported about the controller's connection and configuration.</summary>
public sealed record ScpConnectionDto(
    bool IsOnline,
    DateTimeOffset? LastSeenAtUtc,
    string? FirmwareVersion,
    string? MacAddress,
    int? ReportedScpId,
    int? AppliedVersion,
    ScpApplyState ApplyState,
    string? LastError);

/// <summary>One attempt by the Gateway to bring the controller to a published version, with every command's result.</summary>
public sealed record ScpApplyRunDto(
    int Version,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    ScpApplyOutcome Outcome,
    string? Reason,
    IReadOnlyList<ScpApplyResultDto> Results);

public sealed record ScpApplyResultDto(
    int Order,
    short CommandNumber,
    string Target,
    ScpCommandResult Result,
    string? Detail,
    DateTimeOffset AtUtc);

/// <summary>The SDK commands the Gateway compiled for one published version (preview, spec §3.7).</summary>
public sealed record ScpCommandListDto(
    Guid ScpId,
    short ScpNumber,
    string ScpName,
    int Version,
    DateTimeOffset PublishedAtUtc,
    string? PublishedBy,
    DateTimeOffset? CompiledAtUtc,
    string? CompileError,
    IReadOnlyList<int> Versions,
    IReadOnlyList<ScpCompiledCommandDto> Commands);

/// <summary>One compiled command in HID string form, with what it configures.</summary>
public sealed record ScpCompiledCommandDto(int Order, short CommandNumber, string Target, string Text);
