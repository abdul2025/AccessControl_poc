namespace AccessControl.Application.Features.Sites.DTOs;

public sealed record CreateSiteDto(string Code, string Name, string? Description);

public sealed record UpdateSiteDto(string Code, string Name, string? Description, byte[] RowVersion);
