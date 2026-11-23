using AccessControl.Domain.Common.Entities;
using AccessControl.Domain.Common.Results;
using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Domain.Entities;

/// <summary>
/// A physical input on an SIO (command 110). Part of the <see cref="Scp"/> aggregate; changed only through it.
/// Monitored inputs become monitor points that report changes of state (command 113).
/// </summary>
public sealed class InputPoint : BaseEntity
{
    public const int NameMaxLength = 100;

    private InputPoint()
    {
    }

    public short Number { get; private set; }
    public string Name { get; private set; } = null!;
    public InputContact Contact { get; private set; }

    /// <summary>End-of-line resistors fitted (1k/1k): the controller can also detect cut or shorted wiring.</summary>
    public bool Supervised { get; private set; }

    public bool Monitored { get; private set; }

    /// <summary>Fixed by the board (Tamper / AC fail / Battery fail); such inputs cannot serve a door.</summary>
    public DedicatedInput Dedicated { get; private set; }

    internal static InputPoint Create(short number, string name, DedicatedInput dedicated) => new()
    {
        Number = number,
        Name = name,
        Dedicated = dedicated,

        // Enclosure and power-supply contacts are normally closed; general inputs default to normally open.
        // Nothing is monitored until the installer confirms the wiring: an unwired normally-closed input reads
        // "open" and would raise a permanent alarm (decision: docs/plans/tamper-ac-battery-inputs.md, option A).
        Contact = dedicated == DedicatedInput.None ? InputContact.NormallyOpen : InputContact.NormallyClosed,
        Supervised = false,
        Monitored = false,
    };

    internal Result Update(string name, InputContact contact, bool supervised, bool monitored)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            return Result.Failure(Scp.ValidationErrorCode, $"Input name is required and must be at most {NameMaxLength} characters.");
        }

        if (!Enum.IsDefined(contact))
        {
            return Result.Failure(Scp.ValidationErrorCode, "Contact type is not valid.");
        }

        Name = trimmedName;
        Contact = contact;
        Supervised = supervised;
        Monitored = monitored;
        return Result.Success();
    }
}
