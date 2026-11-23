using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Application.Features.Scps.DTOs;

/// <param name="UtcOffsetHours">Controller local time zone, e.g. 3 for UTC+03:00 (converted to the SDK GMT offset).</param>
public sealed record CreateScpDto(
    Guid SiteId,
    string Name,
    short ScpNumber,
    ScpModel Model,
    ConnectionMode ConnectionMode,
    int? SerialNumber,
    string? IpAddress,
    int? Port,
    string? Description,
    double UtcOffsetHours);

/// <summary>Model and allocation are not here: they are fixed at creation (spec §3.7 rule 4a).</summary>
public sealed record UpdateScpDto(
    Guid SiteId,
    string Name,
    short ScpNumber,
    ConnectionMode ConnectionMode,
    int? SerialNumber,
    string? IpAddress,
    int? Port,
    string? Description,
    byte[] RowVersion);
