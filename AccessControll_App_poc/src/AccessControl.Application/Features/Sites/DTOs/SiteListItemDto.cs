namespace AccessControl.Application.Features.Sites.DTOs;

public sealed record SiteListItemDto(Guid Id, string Code, string Name, int ScpCount);
