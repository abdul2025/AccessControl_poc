using System.ComponentModel.DataAnnotations;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Enums.Scps;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AccessControl.Web.Features.Scps.Models;

/// <summary>Add an external SIO. Every hardware-layout form carries the controller's row version.</summary>
public sealed class AddSioFormViewModel
{
    [Required]
    [Range(1, 31)]
    [Display(Name = "SIO number")]
    public short? Number { get; set; }

    [Required]
    [Display(Name = "Model")]
    public SioModel? Model { get; set; } = SioModel.X100;

    [Required]
    [Display(Name = "RS-485 port")]
    public SioPort? Port { get; set; } = SioPort.Rs485Port1;

    [Required]
    [Range(0, SioPanel.MaxRs485Address)]
    [Display(Name = "RS-485 address")]
    public short? Address { get; set; } = 0;

    [Required]
    [StringLength(SioPanel.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    public string? RowVersion { get; set; }

    [BindNever]
    public string ScpDisplayName { get; set; } = string.Empty;
}

/// <summary>Rename an SIO; model, port and address are fixed once added (remove and re-add to change them).</summary>
public sealed class EditSioFormViewModel
{
    [Required]
    [StringLength(SioPanel.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    public string? RowVersion { get; set; }

    [BindNever]
    public string ScpDisplayName { get; set; } = string.Empty;

    [BindNever]
    public string HardwareSummary { get; set; } = string.Empty;
}

/// <summary>Confirmation for removing an external SIO together with its points.</summary>
public sealed record RemoveSioViewModel(Guid ScpId, Guid SioId, string ScpDisplayName, string SioDisplayName, string HardwareSummary, string RowVersion);

public sealed class InputFormViewModel
{
    [Required]
    [StringLength(InputPoint.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Contact")]
    public InputContact? Contact { get; set; }

    [Display(Name = "Supervised (end-of-line resistors fitted)")]
    public bool Supervised { get; set; }

    [Display(Name = "Monitored (report changes as events)")]
    public bool Monitored { get; set; }

    public string? RowVersion { get; set; }

    [BindNever]
    public string ScpDisplayName { get; set; } = string.Empty;

    [BindNever]
    public string PointDisplayName { get; set; } = string.Empty;

    [BindNever]
    public DedicatedInput Dedicated { get; set; } = DedicatedInput.None;
}

public sealed class OutputFormViewModel
{
    [Required]
    [StringLength(OutputPoint.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(1, OutputPoint.MaxPulseSeconds)]
    [Display(Name = "Pulse time (seconds)")]
    public short? PulseSeconds { get; set; } = OutputPoint.DefaultPulseSeconds;

    public string? RowVersion { get; set; }

    [BindNever]
    public string ScpDisplayName { get; set; } = string.Empty;

    [BindNever]
    public string PointDisplayName { get; set; } = string.Empty;
}
