using AccessControl.Domain.Common.Entities;
using AccessControl.Domain.Common.Results;
using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Domain.Entities;

/// <summary>
/// A card reader on one of an SIO's reader slots (command 112). Part of the <see cref="Scp"/> aggregate; created and
/// changed only through it. Interface settings are entered, not guessed: the VL70LF's interface is open item 1.
/// </summary>
public sealed class Reader : BaseEntity
{
    public const int NameMaxLength = 100;
    public const int ModelMaxLength = 50;
    public const string DefaultModel = "VL70LF";
    public const short MaxOsdpAddress = 3;   // osdp_flags bits 5-6 (SDK §6.7)

    private Reader()
    {
    }

    public Guid SioPanelId { get; private set; }

    /// <summary>Reader slot on its SIO (0 … reader slots - 1); reader 0 = READER 1 terminal (open item 14).</summary>
    public short Number { get; private set; }

    public string Name { get; private set; } = null!;
    public string Model { get; private set; } = null!;
    public ReaderInterface Interface { get; private set; }
    public short OsdpAddress { get; private set; }
    public OsdpBaudRate OsdpBaudRate { get; private set; }
    public bool SecureChannel { get; private set; }

    /// <summary>Wiegand only: the reader sends key presses (HID 4-bit format). OSDP readers report keys themselves.</summary>
    public bool HasKeypad { get; private set; }

    internal static string NormalizeName(string? name) => (name ?? string.Empty).Trim();

    internal static Result<Reader> Create(
        Guid sioPanelId,
        short number,
        string name,
        string? model,
        ReaderInterface readerInterface,
        short osdpAddress,
        OsdpBaudRate osdpBaudRate,
        bool secureChannel,
        bool hasKeypad)
    {
        var reader = new Reader();
        var applied = reader.Apply(sioPanelId, number, name, model, readerInterface, osdpAddress, osdpBaudRate, secureChannel, hasKeypad);
        return applied.IsSuccess
            ? Result<Reader>.Success(reader)
            : Result<Reader>.Failure(applied.ErrorCode!, applied.ErrorMessage!);
    }

    internal Result Apply(
        Guid sioPanelId,
        short number,
        string name,
        string? model,
        ReaderInterface readerInterface,
        short osdpAddress,
        OsdpBaudRate osdpBaudRate,
        bool secureChannel,
        bool hasKeypad)
    {
        var trimmedName = NormalizeName(name);
        var trimmedModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();

        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            return Invalid($"Reader name is required and must be at most {NameMaxLength} characters.");
        }

        if (trimmedModel.Length > ModelMaxLength)
        {
            return Invalid($"Reader model must be at most {ModelMaxLength} characters.");
        }

        if (!Enum.IsDefined(readerInterface))
        {
            return Invalid("Reader interface is not valid.");
        }

        if (readerInterface == ReaderInterface.Osdp)
        {
            if (osdpAddress is < 0 or > MaxOsdpAddress)
            {
                return Invalid($"OSDP address must be between 0 and {MaxOsdpAddress}.");
            }

            if (!Enum.IsDefined(osdpBaudRate))
            {
                return Invalid("OSDP baud rate is not valid.");
            }

            // OSDP readers report key presses over OSDP; the Wiegand keypad setting does not apply.
            hasKeypad = false;
        }
        else
        {
            // Wiegand has no address, speed or secure channel; keep neutral values so the record stays tidy.
            osdpAddress = 0;
            osdpBaudRate = OsdpBaudRate.Baud9600;
            secureChannel = false;
        }

        SioPanelId = sioPanelId;
        Number = number;
        Name = trimmedName;
        Model = trimmedModel;
        Interface = readerInterface;
        OsdpAddress = osdpAddress;
        OsdpBaudRate = osdpBaudRate;
        SecureChannel = secureChannel;
        HasKeypad = hasKeypad;
        return Result.Success();
    }

    private static Result Invalid(string message) => Result.Failure(Scp.ValidationErrorCode, message);
}
