using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AccessControl.Application.Common.Paging;
using AccessControl.Application.Features.Scps.DTOs;
using AccessControl.Domain.Enums.Scps;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using ScpEntity = AccessControl.Domain.Entities.Scp;

namespace AccessControl.Web.Features.Scps.Models;

/// <summary>
/// Create and edit share this form. Model and time zone are set only on create (allocation is fixed at creation,
/// spec §3.7 rule 4a). Display-only values are [BindNever] and reloaded by the controller on every render.
/// </summary>
public sealed class ScpFormViewModel : IValidatableObject
{
    public const double DefaultUtcOffsetHours = 3;   // Saudi Arabia, UTC+03:00 (spec §6.2)

    [Required]
    [Display(Name = "Site")]
    public Guid? SiteId { get; set; }

    [Required]
    [StringLength(ScpEntity.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(ScpEntity.MinScpNumber, ScpEntity.MaxScpNumber)]
    [Display(Name = "SCP Host assigning number")]
    public short? ScpNumber { get; set; }

    [Display(Name = "Model")]
    public ScpModel? Model { get; set; } = ScpModel.X1100;

    [Required]
    [Display(Name = "Connection mode")]
    public ConnectionMode? ConnectionMode { get; set; } = Domain.Enums.Scps.ConnectionMode.IpClient;

    [Range(1, int.MaxValue)]
    [Display(Name = "Serial number")]
    public int? SerialNumber { get; set; }

    [StringLength(ScpEntity.HostMaxLength)]
    [Display(Name = "IP address or host name")]
    public string? IpAddress { get; set; }

    [Range(1, 65535)]
    public int? Port { get; set; }

    [StringLength(ScpEntity.DescriptionMaxLength)]
    [DataType(DataType.MultilineText)]
    public string? Description { get; set; }

    [Range(-12, 14)]
    [Display(Name = "Time zone (UTC offset, hours)")]
    public double? UtcOffsetHours { get; set; } = DefaultUtcOffsetHours;

    /// <summary>Base64 row version captured when the edit form was loaded.</summary>
    public string? RowVersion { get; set; }

    [BindNever]
    public IReadOnlyList<SiteOptionDto> SiteOptions { get; set; } = [];

    [BindNever]
    public bool IsEdit { get; set; }

    [BindNever]
    public bool CanChangeScpNumber { get; set; } = true;

    /// <summary>Edit only: the allocation fixed at creation, shown read-only.</summary>
    [BindNever]
    public ScpAllocationDto? Allocation { get; set; }

    public IEnumerable<SelectListItem> SiteItems() =>
        SiteOptions.Select(site => new SelectListItem(site.DisplayName, site.Id.ToString()));

    /// <summary>Quick field-level feedback for the mode-dependent fields; the domain enforces the same rules.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ConnectionMode == Domain.Enums.Scps.ConnectionMode.IpServer && string.IsNullOrWhiteSpace(IpAddress))
        {
            yield return new ValidationResult("IP address or host name is required for an IP Server controller.", [nameof(IpAddress)]);
        }
    }
}

public sealed record ScpDetailsViewModel(ScpDto Scp, string RowVersion)
{
    public Guid Id => Scp.Id;

    public string DisplayName => $"SCP {Scp.ScpNumber} — {Scp.Name}";

    public static ScpDetailsViewModel From(ScpDto dto) => new(dto, Convert.ToBase64String(dto.RowVersion));
}

/// <summary>The list page; RouteValues keeps the current filters on pager links.</summary>
public sealed record ScpListViewModel(
    PagedResponse<ScpListItemDto> Page,
    ScpListFilter Filter,
    IReadOnlyList<SiteOptionDto> SiteOptions)
{
    public Dictionary<string, string> RouteValues(int? page = null)
    {
        var values = new Dictionary<string, string>();
        if (Filter.Search is { } search)
        {
            values["search"] = search;
        }

        if (Filter.SiteId is { } siteId)
        {
            values["siteId"] = siteId.ToString();
        }

        if (page is > 1)
        {
            values["page"] = page.Value.ToString(CultureInfo.InvariantCulture);
        }

        return values;
    }
}
