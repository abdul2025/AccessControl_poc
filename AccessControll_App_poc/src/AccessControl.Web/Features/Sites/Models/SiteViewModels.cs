using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AccessControl.Application.Common.Paging;
using AccessControl.Application.Features.Sites.DTOs;
using SiteEntity = AccessControl.Domain.Entities.Site;

namespace AccessControl.Web.Features.Sites.Models;

/// <summary>Create and edit share this form.</summary>
public sealed class SiteFormViewModel
{
    [Required]
    [StringLength(SiteEntity.CodeMaxLength)]
    [Display(Name = "Code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(SiteEntity.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(SiteEntity.DescriptionMaxLength)]
    [DataType(DataType.MultilineText)]
    public string? Description { get; set; }

    /// <summary>Base64 row version captured when the edit form was loaded.</summary>
    public string? RowVersion { get; set; }
}

public sealed record SiteDetailsViewModel(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<SiteScpItemDto> Scps,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    string RowVersion)
{
    public bool HasScps => Scps.Count > 0;

    public static SiteDetailsViewModel From(SiteDto dto) => new(
        dto.Id, dto.Code, dto.Name, dto.Description, dto.Scps,
        dto.CreatedAtUtc, dto.CreatedBy, dto.UpdatedAtUtc, dto.UpdatedBy,
        Convert.ToBase64String(dto.RowVersion));
}

/// <summary>The list page; RouteValues keeps the current filters on pager links.</summary>
public sealed record SiteListViewModel(PagedResponse<SiteListItemDto> Page, SiteListFilter Filter)
{
    public Dictionary<string, string> RouteValues(int? page = null)
    {
        var values = new Dictionary<string, string>();
        if (Filter.Search is { } search)
        {
            values["search"] = search;
        }

        if (page is > 1)
        {
            values["page"] = page.Value.ToString(CultureInfo.InvariantCulture);
        }

        return values;
    }
}
