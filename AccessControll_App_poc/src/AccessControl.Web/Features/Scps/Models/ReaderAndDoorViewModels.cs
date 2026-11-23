using System.ComponentModel.DataAnnotations;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Enums.Scps;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccessControl.Web.Features.Scps.Models;

/// <summary>A choice in a reader / point dropdown.</summary>
public sealed record OptionItem(Guid Id, string Text);

public sealed class ReaderFormViewModel
{
    [Required]
    [Display(Name = "SIO")]
    public Guid? SioPanelId { get; set; }

    [Required]
    [Range(0, 3)]
    [Display(Name = "Reader number")]
    public short? Number { get; set; } = 0;

    [Required]
    [StringLength(Reader.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(Reader.ModelMaxLength)]
    public string? Model { get; set; } = Reader.DefaultModel;

    [Required]
    [Display(Name = "Interface")]
    public ReaderInterface? Interface { get; set; } = ReaderInterface.Osdp;

    [Range(0, Reader.MaxOsdpAddress)]
    [Display(Name = "OSDP address")]
    public short? OsdpAddress { get; set; } = 0;

    [Display(Name = "Baud rate")]
    public OsdpBaudRate? OsdpBaudRate { get; set; } = Domain.Enums.Scps.OsdpBaudRate.Baud9600;

    [Display(Name = "Secure channel")]
    public bool SecureChannel { get; set; }

    [Display(Name = "Reader has a keypad")]
    public bool HasKeypad { get; set; }

    public string? RowVersion { get; set; }

    [BindNever]
    public string ScpDisplayName { get; set; } = string.Empty;

    [BindNever]
    public string Title { get; set; } = "Add reader";

    /// <summary>SIOs that have reader ports.</summary>
    [BindNever]
    public IReadOnlyList<OptionItem> SioOptions { get; set; } = [];

    public IEnumerable<SelectListItem> SioItems() => SioOptions.Select(option => new SelectListItem(option.Text, option.Id.ToString()));
}

public sealed class DoorFormViewModel : IValidatableObject
{
    [Required]
    [Range(0, 63)]
    [Display(Name = "Door number")]
    public short? Number { get; set; }

    [Required]
    [StringLength(Door.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Entry reader")]
    public Guid? EntryReaderId { get; set; }

    [Display(Name = "Exit reader (optional)")]
    public Guid? ExitReaderId { get; set; }

    [Required]
    [Display(Name = "Lock relay")]
    public Guid? LockOutputId { get; set; }

    [Display(Name = "Door contact (optional)")]
    public Guid? DoorContactInputId { get; set; }

    [Display(Name = "Exit button (optional)")]
    public Guid? ExitButtonInputId { get; set; }

    [Required]
    [Range(1, Door.MaxStrikeSeconds)]
    [Display(Name = "Unlock time minimum (s)")]
    public short? StrikeMinSeconds { get; set; } = Door.DefaultStrikeMinSeconds;

    [Required]
    [Range(1, Door.MaxStrikeSeconds)]
    [Display(Name = "Unlock time maximum (s)")]
    public short? StrikeMaxSeconds { get; set; } = Door.DefaultStrikeMaxSeconds;

    [Required]
    [Range(1, Door.MaxHeldOpenSeconds)]
    [Display(Name = "Held-open alarm after (s)")]
    public short? HeldOpenSeconds { get; set; } = Door.DefaultHeldOpenSeconds;

    [Required]
    [Display(Name = "Access mode")]
    public DoorAccessMode? AccessMode { get; set; } = DoorAccessMode.CardOnly;

    public string? RowVersion { get; set; }

    [BindNever]
    public string ScpDisplayName { get; set; } = string.Empty;

    [BindNever]
    public string Title { get; set; } = "Add door";

    [BindNever]
    public IReadOnlyList<OptionItem> ReaderOptions { get; set; } = [];

    [BindNever]
    public IReadOnlyList<OptionItem> RelayOptions { get; set; } = [];

    [BindNever]
    public IReadOnlyList<OptionItem> InputOptions { get; set; } = [];

    public static IEnumerable<SelectListItem> Items(IEnumerable<OptionItem> options) =>
        options.Select(option => new SelectListItem(option.Text, option.Id.ToString()));

    /// <summary>Quick field-level feedback; the domain enforces the same rules.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExitReaderId is not null && ExitReaderId == EntryReaderId)
        {
            yield return new ValidationResult("Entry and exit readers must be different.", [nameof(ExitReaderId)]);
        }

        if (DoorContactInputId is not null && DoorContactInputId == ExitButtonInputId)
        {
            yield return new ValidationResult("The door contact and the exit button must be different inputs.", [nameof(ExitButtonInputId)]);
        }

        if (StrikeMinSeconds is { } min && StrikeMaxSeconds is { } max && max < min)
        {
            yield return new ValidationResult("Maximum cannot be shorter than minimum.", [nameof(StrikeMaxSeconds)]);
        }
    }
}

/// <summary>Confirmation for removing a reader or a door.</summary>
public sealed record RemoveItemViewModel(
    Guid ScpId,
    string ScpDisplayName,
    string Title,
    string Summary,
    string? BlockedReason,
    string RowVersion);
