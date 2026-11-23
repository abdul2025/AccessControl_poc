using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Application.Features.Scps.DTOs;

public sealed record ScpListItemDto(
    Guid Id,
    string Name,
    short ScpNumber,
    string SiteCode,
    ConnectionMode ConnectionMode,
    int? SerialNumber,
    string? IpAddress,
    ScpSyncStatus SyncStatus);
