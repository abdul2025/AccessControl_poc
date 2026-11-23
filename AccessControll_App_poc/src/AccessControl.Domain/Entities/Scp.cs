using System.Net;
using AccessControl.Domain.Catalog;
using AccessControl.Domain.Common.Entities;
using AccessControl.Domain.Common.Results;
using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Domain.Entities;

/// <summary>
/// An HID Aero controller (SCP, shown to users as "Controller") and its desired configuration. Saving changes only the
/// database; the Gateway applies a published configuration to the hardware (docs/PROJECT_SPEC.md §3.7).
/// </summary>
public sealed partial class Scp : AuditableEntity, IHasDisplayName
{
    public const string ValidationErrorCode = "Scp.Validation";
    public const string NumberLockedErrorCode = "Scp.NumberLocked";
    public const string SioNotFoundErrorCode = "Scp.SioNotFound";
    public const string PointNotFoundErrorCode = "Scp.PointNotFound";
    public const string DuplicateSioNumberErrorCode = "Scp.DuplicateSioNumber";
    public const string DuplicateSioAddressErrorCode = "Scp.DuplicateSioAddress";
    public const string DuplicateSioNameErrorCode = "Scp.DuplicateSioName";
    public const string SioLimitErrorCode = "Scp.SioLimit";
    public const string InternalSioErrorCode = "Scp.InternalSio";
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 2000;
    public const int HostMaxLength = 253;
    public const short MinScpNumber = 1;
    public const short MaxScpNumber = 16383;   // SDK IP Client SCP ID range (command 019)
    public const int DefaultPort = 3001;

    private readonly List<SioPanel> _sioPanels = [];

    private Scp()
    {
    }

    public Guid SiteId { get; private set; }
    public string Name { get; private set; } = null!;
    public short ScpNumber { get; private set; }
    public ScpModel Model { get; private set; }
    public ConnectionMode ConnectionMode { get; private set; }

    /// <summary>Controller serial number (label / ID report); how an IP Client controller is recognised.</summary>
    public int? SerialNumber { get; private set; }

    /// <summary>IP Server only: where the Gateway connects. IP Client controllers have no address here (spec §5.2.1).</summary>
    public string? IpAddress { get; private set; }

    public int? Port { get; private set; }
    public string? Description { get; private set; }
    public ScpAllocation Allocation { get; private set; } = null!;
    public ScpSyncStatus SyncStatus { get; private set; }
    public int? PublishedVersion { get; private set; }
    public int? AppliedVersion { get; private set; }

    public IReadOnlyList<SioPanel> SioPanels => _sioPanels;

    public string DisplayName => Name;

    /// <summary>The SCP number is programmed into the controller once a configuration is published (command 318).</summary>
    public bool CanChangeScpNumber => IsScpNumberChangeable(SyncStatus);

    public static bool IsScpNumberChangeable(ScpSyncStatus status) => status == ScpSyncStatus.NotPublished;

    public static Result<Scp> Create(
        Guid siteId,
        string name,
        short scpNumber,
        ScpModel model,
        ConnectionMode connectionMode,
        int? serialNumber,
        string? ipAddress,
        int? port,
        string? description,
        int gmtOffsetSeconds)
    {
        if (!HardwareCatalog.IsKnown(model))
        {
            return Result<Scp>.Failure(ValidationErrorCode, "Model is not valid.");
        }

        if (!ScpAllocation.IsValidGmtOffset(gmtOffsetSeconds))
        {
            return Result<Scp>.Failure(ValidationErrorCode, "Time zone offset must be between UTC-12 and UTC+14 in 15-minute steps.");
        }

        var scp = new Scp
        {
            Model = model,
            Allocation = ScpAllocation.Default with { GmtOffsetSeconds = gmtOffsetSeconds },
            SyncStatus = ScpSyncStatus.NotPublished,
        };

        var applied = scp.Apply(siteId, name, scpNumber, connectionMode, serialNumber, ipAddress, port, description);
        if (applied.IsFailure)
        {
            return Result<Scp>.Failure(applied.ErrorCode!, applied.ErrorMessage!);
        }

        // Every controller has a built-in SIO; creating it here keeps the aggregate complete from the start.
        scp._sioPanels.Add(SioPanel.CreateInternal(HardwareCatalog.For(model).InternalSio));
        return Result<Scp>.Success(scp);
    }

    /// <summary>Changes everything except model and allocation, which are fixed at creation (spec §3.7 rule 4a).</summary>
    public Result Update(
        Guid siteId,
        string name,
        short scpNumber,
        ConnectionMode connectionMode,
        int? serialNumber,
        string? ipAddress,
        int? port,
        string? description)
    {
        if (scpNumber != ScpNumber && !CanChangeScpNumber)
        {
            return Result.Failure(NumberLockedErrorCode, "The SCP number cannot change after the configuration has been published.");
        }

        return Apply(siteId, name, scpNumber, connectionMode, serialNumber, ipAddress, port, description);
    }

    public static string NormalizeName(string? name) => (name ?? string.Empty).Trim();

    /// <summary>The lowest free external SIO number (1-31), or <c>null</c> when the controller is full.</summary>
    public short? NextFreeSioNumber() => NextFreeSioNumber(_sioPanels.Select(panel => panel.Number));

    /// <summary>Shared with read models so the suggestion and the rule cannot differ.</summary>
    public static short? NextFreeSioNumber(IEnumerable<short> usedNumbers)
    {
        var used = usedNumbers.ToHashSet();
        for (short number = 1; number < HardwareCatalog.MaxSiosPerScp; number++)
        {
            if (!used.Contains(number))
            {
                return number;
            }
        }

        return null;
    }

    /// <summary>
    /// Adds an external X100 / X200 / X300 on an RS-485 port, with all its points (commands 108 / 109). At most 16 SIOs
    /// per port and 32 per controller; the address is unique on its port.
    /// </summary>
    public Result AddSio(short number, SioModel model, SioPort port, short address, string name)
    {
        if (!HardwareCatalog.IsKnown(model) || model == HardwareCatalog.For(Model).InternalSio)
        {
            return Result.Failure(ValidationErrorCode, "Choose an external SIO model (X100, X200 or X300).");
        }

        if (port is not (SioPort.Rs485Port1 or SioPort.Rs485Port2))
        {
            return Result.Failure(ValidationErrorCode, "External SIOs connect to RS-485 port 1 or 2.");
        }

        if (address is < 0 or > SioPanel.MaxRs485Address)
        {
            return Result.Failure(ValidationErrorCode, $"RS-485 address must be between 0 and {SioPanel.MaxRs485Address}.");
        }

        if (number is < 1 or >= HardwareCatalog.MaxSiosPerScp)
        {
            return Result.Failure(ValidationErrorCode, $"SIO number must be between 1 and {HardwareCatalog.MaxSiosPerScp - 1}.");
        }

        if (_sioPanels.Count >= HardwareCatalog.MaxSiosPerScp
            || _sioPanels.Count(panel => panel.Port == port) >= HardwareCatalog.For(Model).MaxSiosPerRs485Port)
        {
            return Result.Failure(SioLimitErrorCode, "This port or controller already has the maximum number of SIOs.");
        }

        if (_sioPanels.Any(panel => panel.Number == number))
        {
            return Result.Failure(DuplicateSioNumberErrorCode, "Another SIO on this controller already uses this number.");
        }

        if (_sioPanels.Any(panel => panel.Port == port && panel.Address == address))
        {
            return Result.Failure(DuplicateSioAddressErrorCode, "Another SIO on this port already uses this address.");
        }

        var nameCheck = CheckSioName(name, excludeSioId: null);
        if (nameCheck.IsFailure)
        {
            return nameCheck;
        }

        _sioPanels.Add(SioPanel.CreateExternal(number, model, port, address, SioPanel.NormalizeName(name)));
        return Result.Success();
    }

    public Result RenameSio(Guid sioId, string name)
    {
        if (FindSio(sioId) is not { } panel)
        {
            return Result.Failure(SioNotFoundErrorCode, "The SIO was not found on this controller.");
        }

        var nameCheck = CheckSioName(name, excludeSioId: sioId);
        return nameCheck.IsFailure ? nameCheck : panel.Rename(name);
    }

    /// <summary>Removes an external SIO and its points. The built-in SIO is part of the controller and stays.</summary>
    public Result RemoveSio(Guid sioId)
    {
        if (FindSio(sioId) is not { } panel)
        {
            return Result.Failure(SioNotFoundErrorCode, "The SIO was not found on this controller.");
        }

        if (panel.IsInternal)
        {
            return Result.Failure(InternalSioErrorCode, "The controller's built-in SIO cannot be removed.");
        }

        if (IsSioInUse(panel))
        {
            return Result.Failure(SioInUseErrorCode, "A reader or door uses this SIO. Remove or change them first.");
        }

        _sioPanels.Remove(panel);
        return Result.Success();
    }

    public Result UpdateInput(Guid inputId, string name, InputContact contact, bool supervised, bool monitored)
    {
        var input = _sioPanels.SelectMany(panel => panel.Inputs).FirstOrDefault(point => point.Id == inputId);
        return input is null
            ? Result.Failure(PointNotFoundErrorCode, "The input was not found on this controller.")
            : input.Update(name, contact, supervised, monitored);
    }

    public Result UpdateOutput(Guid outputId, string name, short pulseSeconds)
    {
        var output = _sioPanels.SelectMany(panel => panel.Outputs).FirstOrDefault(point => point.Id == outputId);
        return output is null
            ? Result.Failure(PointNotFoundErrorCode, "The output was not found on this controller.")
            : output.Update(name, pulseSeconds);
    }

    private SioPanel? FindSio(Guid sioId) => _sioPanels.FirstOrDefault(panel => panel.Id == sioId);

    private Result CheckSioName(string name, Guid? excludeSioId)
    {
        var trimmed = SioPanel.NormalizeName(name);
        if (trimmed.Length is 0 or > SioPanel.NameMaxLength)
        {
            return Result.Failure(ValidationErrorCode, $"SIO name is required and must be at most {SioPanel.NameMaxLength} characters.");
        }

        return _sioPanels.Any(panel => panel.Id != excludeSioId && string.Equals(panel.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            ? Result.Failure(DuplicateSioNameErrorCode, "Another SIO on this controller already uses this name.")
            : Result.Success();
    }

    private Result Apply(
        Guid siteId,
        string name,
        short scpNumber,
        ConnectionMode connectionMode,
        int? serialNumber,
        string? ipAddress,
        int? port,
        string? description)
    {
        var trimmedName = NormalizeName(name);
        var trimmedHost = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (siteId == Guid.Empty)
        {
            return Invalid("Site is required.");
        }

        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            return Invalid($"Name is required and must be at most {NameMaxLength} characters.");
        }

        if (scpNumber is < MinScpNumber or > MaxScpNumber)
        {
            return Invalid($"SCP number must be between {MinScpNumber} and {MaxScpNumber}.");
        }

        if (!Enum.IsDefined(connectionMode))
        {
            return Invalid("Connection mode is not valid.");
        }

        if (serialNumber is <= 0)
        {
            return Invalid("Serial number must be a positive number.");
        }

        if (trimmedDescription is { Length: > DescriptionMaxLength })
        {
            return Invalid($"Description must be at most {DescriptionMaxLength} characters.");
        }

        int? effectivePort = null;
        if (connectionMode == ConnectionMode.IpClient)
        {
            // The controller calls the Gateway; it is recognised by serial number and needs no address here.
            if (serialNumber is null)
            {
                return Invalid("Serial number is required for an IP Client controller.");
            }

            trimmedHost = null;
        }
        else
        {
            if (trimmedHost is null || trimmedHost.Length > HostMaxLength || !IsValidHost(trimmedHost))
            {
                return Invalid("A valid IP address or host name is required for an IP Server controller.");
            }

            effectivePort = port ?? DefaultPort;
            if (effectivePort is < 1 or > 65535)
            {
                return Invalid("Port must be between 1 and 65535.");
            }
        }

        SiteId = siteId;
        Name = trimmedName;
        ScpNumber = scpNumber;
        ConnectionMode = connectionMode;
        SerialNumber = serialNumber;
        IpAddress = trimmedHost;
        Port = effectivePort;
        Description = trimmedDescription;

        return Result.Success();
    }

    private static bool IsValidHost(string host) =>
        IPAddress.TryParse(host, out _) || Uri.CheckHostName(host) == UriHostNameType.Dns;

    private static Result Invalid(string message) => Result.Failure(ValidationErrorCode, message);
}
