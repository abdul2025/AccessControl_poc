using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Application.Features.Scps.DTOs;

public sealed record ScpDto(
    Guid Id,
    Guid SiteId,
    string SiteCode,
    string SiteName,
    string Name,
    short ScpNumber,
    ScpModel Model,
    ConnectionMode ConnectionMode,
    int? SerialNumber,
    string? IpAddress,
    int? Port,
    string? Description,
    ScpAllocationDto Allocation,
    ScpSyncStatus SyncStatus,
    int? PublishedVersion,
    int? AppliedVersion,
    bool CanChangeScpNumber,
    IReadOnlyList<SioPanelDto> SioPanels,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    byte[] RowVersion);

/// <summary>Allocation values sent with 1107 / 1105; fixed at creation.</summary>
public sealed record ScpAllocationDto(int Transactions, int Cards, short AccessLevelsPerCard, int GmtOffsetSeconds)
{
    public double UtcOffsetHours => -GmtOffsetSeconds / 3600d;
}

/// <summary>An SIO on the controller with its hardware capacity from the model catalog.</summary>
public sealed record SioPanelDto(
    Guid Id,
    short Number,
    SioModel Model,
    SioPort Port,
    short Address,
    string Name,
    int Inputs,
    int Outputs,
    int ReaderPorts);
