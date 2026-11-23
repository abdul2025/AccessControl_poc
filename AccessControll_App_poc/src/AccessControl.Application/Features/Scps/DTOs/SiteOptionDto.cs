namespace AccessControl.Application.Features.Scps.DTOs;

/// <summary>A site a controller form can select.</summary>
public sealed record SiteOptionDto(Guid Id, string Code, string Name)
{
    public string DisplayName => $"{Code} — {Name}";
}
