using AccessControl.Domain.Common.Entities;
using AccessControl.Domain.Common.Results;
using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Domain.Entities;

/// <summary>
/// A door controlled by the controller (an access control reader, ACR — command 115): its reader(s), lock relay,
/// door contact and exit button, lock times and access mode. Part of the <see cref="Scp"/> aggregate; the rules that
/// involve other readers and points (same controller, one door per point) are enforced by <see cref="Scp"/>.
/// </summary>
public sealed class Door : BaseEntity
{
    public const int NameMaxLength = 100;
    public const short DefaultStrikeMinSeconds = 1;
    public const short DefaultStrikeMaxSeconds = 5;
    public const short MaxStrikeSeconds = 255;
    public const short DefaultHeldOpenSeconds = 300;   // 5 minutes — MoM asks 5-10 min; unit / range: open item 7
    public const short MaxHeldOpenSeconds = 3600;

    private Door()
    {
    }

    /// <summary>ACR number of the entry side on the controller (0-63) — the primary ACR of a paired door.</summary>
    public short Number { get; private set; }

    public string Name { get; private set; } = null!;
    public Guid EntryReaderId { get; private set; }

    /// <summary>
    /// Optional reader on the other side. It becomes a second, paired ACR (command 115 secondary reader, SDK §8.1.3)
    /// so entry and exit events carry different ACR numbers (docs/plans/step-4-publish-and-provisioning.md §4.1).
    /// </summary>
    public Guid? ExitReaderId { get; private set; }

    /// <summary>
    /// ACR number of the exit reader's paired ACR; set whenever <see cref="ExitReaderId"/> is set. Assigned by the
    /// controller and kept stable while the exit reader stays, because events and access levels refer to it.
    /// </summary>
    public short? ExitAcrNumber { get; private set; }

    public Guid LockOutputId { get; private set; }
    public Guid? DoorContactInputId { get; private set; }
    public Guid? ExitButtonInputId { get; private set; }
    public short StrikeMinSeconds { get; private set; }
    public short StrikeMaxSeconds { get; private set; }
    public short HeldOpenSeconds { get; private set; }
    public DoorAccessMode AccessMode { get; private set; }

    internal static string NormalizeName(string? name) => (name ?? string.Empty).Trim();

    internal static Door CreateEmpty() => new();

    /// <summary>Validates the door's own values; references are validated by the controller before calling.</summary>
    internal Result Apply(
        short number,
        string name,
        Guid entryReaderId,
        Guid? exitReaderId,
        short? exitAcrNumber,
        Guid lockOutputId,
        Guid? doorContactInputId,
        Guid? exitButtonInputId,
        short strikeMinSeconds,
        short strikeMaxSeconds,
        short heldOpenSeconds,
        DoorAccessMode accessMode)
    {
        var trimmedName = NormalizeName(name);
        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            return Invalid($"Door name is required and must be at most {NameMaxLength} characters.");
        }

        if (strikeMinSeconds is < 1 or > MaxStrikeSeconds || strikeMaxSeconds is < 1 or > MaxStrikeSeconds)
        {
            return Invalid($"Lock times must be between 1 and {MaxStrikeSeconds} seconds.");
        }

        if (strikeMaxSeconds < strikeMinSeconds)
        {
            return Invalid("Maximum lock time cannot be shorter than the minimum lock time.");
        }

        if (heldOpenSeconds is < 1 or > MaxHeldOpenSeconds)
        {
            return Invalid($"Held-open alarm time must be between 1 and {MaxHeldOpenSeconds} seconds.");
        }

        if (!Enum.IsDefined(accessMode))
        {
            return Invalid("Access mode is not valid.");
        }

        Number = number;
        Name = trimmedName;
        EntryReaderId = entryReaderId;
        ExitReaderId = exitReaderId;
        ExitAcrNumber = exitReaderId is null ? null : exitAcrNumber;
        LockOutputId = lockOutputId;
        DoorContactInputId = doorContactInputId;
        ExitButtonInputId = exitButtonInputId;
        StrikeMinSeconds = strikeMinSeconds;
        StrikeMaxSeconds = strikeMaxSeconds;
        HeldOpenSeconds = heldOpenSeconds;
        AccessMode = accessMode;
        return Result.Success();
    }

    /// <summary>The ACR numbers this door occupies on the controller: its own number and, if paired, the exit ACR.</summary>
    internal IEnumerable<short> UsedAcrNumbers() => ExitAcrNumber is { } exitAcr ? [Number, exitAcr] : [Number];

    /// <summary>Every reader and point this door uses (for "one door per reader / point" checks).</summary>
    internal IEnumerable<Guid> UsedReaderIds() => ExitReaderId is { } exit ? [EntryReaderId, exit] : [EntryReaderId];

    internal IEnumerable<Guid> UsedInputIds()
    {
        if (DoorContactInputId is { } contact)
        {
            yield return contact;
        }

        if (ExitButtonInputId is { } exitButton)
        {
            yield return exitButton;
        }
    }

    private static Result Invalid(string message) => Result.Failure(Scp.ValidationErrorCode, message);
}
