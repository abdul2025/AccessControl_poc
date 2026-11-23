using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Application.Features.Sites.DTOs;

public sealed record SiteDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<SiteScpItemDto> Scps,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    byte[] RowVersion);

/// <summary>A controller listed on its site details page.</summary>
public sealed record SiteScpItemDto(Guid Id, string Name, short ScpNumber, ConnectionMode ConnectionMode, ScpSyncStatus SyncStatus);
