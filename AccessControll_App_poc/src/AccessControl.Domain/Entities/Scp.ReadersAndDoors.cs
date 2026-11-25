using AccessControl.Domain.Catalog;
using AccessControl.Domain.Common.Results;
using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Domain.Entities;

/// <summary>Readers and doors of the controller (commands 112 / 115) and the rules that span the whole aggregate.</summary>
public sealed partial class Scp
{
    public const string ReaderNotFoundErrorCode = "Scp.ReaderNotFound";
    public const string DoorNotFoundErrorCode = "Scp.DoorNotFound";
    public const string DuplicateReaderNumberErrorCode = "Scp.DuplicateReaderNumber";
    public const string DuplicateReaderNameErrorCode = "Scp.DuplicateReaderName";
    public const string DuplicateDoorNumberErrorCode = "Scp.DuplicateDoorNumber";
    public const string DuplicateDoorNameErrorCode = "Scp.DuplicateDoorName";
    public const string ReaderInUseErrorCode = "Scp.ReaderInUse";
    public const string PointInUseErrorCode = "Scp.PointInUse";
    public const string SioInUseErrorCode = "Scp.SioInUse";
    public const string DoorLimitErrorCode = "Scp.DoorLimit";

    private readonly List<Reader> _readers = [];
    private readonly List<Door> _doors = [];

    public IReadOnlyList<Reader> Readers => _readers;
    public IReadOnlyList<Door> Doors => _doors;

    /// <summary>
    /// The lowest free ACR number, or <c>null</c> when all 64 are used. Door numbers and the exit ACRs of paired doors
    /// share the same range (1107 <c>nAcr</c>).
    /// </summary>
    public short? NextFreeDoorNumber() => NextFreeDoorNumber(_doors.SelectMany(door => door.UsedAcrNumbers()));

    /// <summary>Shared with read models so the suggestion and the rule cannot differ. Pass every used ACR number.</summary>
    public static short? NextFreeDoorNumber(IEnumerable<short> usedNumbers)
    {
        var used = usedNumbers.ToHashSet();
        for (short number = 0; number < HardwareCatalog.MaxDoorsPerScp; number++)
        {
            if (!used.Contains(number))
            {
                return number;
            }
        }

        return null;
    }

    // ---- Readers --------------------------------------------------------------------------------------------

    public Result AddReader(
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
        var placement = CheckReaderPlacement(sioPanelId, number, name, excludeReaderId: null);
        if (placement.IsFailure)
        {
            return placement;
        }

        var created = Reader.Create(sioPanelId, number, name, model, readerInterface, osdpAddress, osdpBaudRate, secureChannel, hasKeypad);
        if (created.IsFailure)
        {
            return Result.Failure(created.ErrorCode!, created.ErrorMessage!);
        }

        _readers.Add(created.Value!);
        return Result.Success();
    }

    public Result UpdateReader(
        Guid readerId,
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
        if (_readers.FirstOrDefault(reader => reader.Id == readerId) is not { } existing)
        {
            return Result.Failure(ReaderNotFoundErrorCode, "The reader was not found on this controller.");
        }

        var placement = CheckReaderPlacement(sioPanelId, number, name, excludeReaderId: readerId);
        return placement.IsFailure
            ? placement
            : existing.Apply(sioPanelId, number, name, model, readerInterface, osdpAddress, osdpBaudRate, secureChannel, hasKeypad);
    }

    public Result RemoveReader(Guid readerId)
    {
        if (_readers.FirstOrDefault(reader => reader.Id == readerId) is not { } reader)
        {
            return Result.Failure(ReaderNotFoundErrorCode, "The reader was not found on this controller.");
        }

        if (_doors.Any(door => door.UsedReaderIds().Contains(readerId)))
        {
            return Result.Failure(ReaderInUseErrorCode, "A door uses this reader. Remove the door or choose another reader first.");
        }

        _readers.Remove(reader);
        return Result.Success();
    }

    // ---- Doors ----------------------------------------------------------------------------------------------

    public Result AddDoor(
        short number,
        string name,
        Guid entryReaderId,
        Guid? exitReaderId,
        Guid lockOutputId,
        Guid? doorContactInputId,
        Guid? exitButtonInputId,
        short strikeMinSeconds,
        short strikeMaxSeconds,
        short heldOpenSeconds,
        DoorAccessMode accessMode)
    {
        if (_doors.Sum(door => door.UsedAcrNumbers().Count()) >= HardwareCatalog.MaxDoorsPerScp)
        {
            return Result.Failure(DoorLimitErrorCode,
                $"A controller has {HardwareCatalog.MaxDoorsPerScp} ACRs; doors and exit readers use them all.");
        }

        var door = Door.CreateEmpty();
        var applied = ApplyDoor(door, isNew: true, number, name, entryReaderId, exitReaderId, lockOutputId,
            doorContactInputId, exitButtonInputId, strikeMinSeconds, strikeMaxSeconds, heldOpenSeconds, accessMode);

        if (applied.IsSuccess)
        {
            _doors.Add(door);
        }

        return applied;
    }

    public Result UpdateDoor(
        Guid doorId,
        short number,
        string name,
        Guid entryReaderId,
        Guid? exitReaderId,
        Guid lockOutputId,
        Guid? doorContactInputId,
        Guid? exitButtonInputId,
        short strikeMinSeconds,
        short strikeMaxSeconds,
        short heldOpenSeconds,
        DoorAccessMode accessMode)
    {
        if (_doors.FirstOrDefault(door => door.Id == doorId) is not { } existing)
        {
            return Result.Failure(DoorNotFoundErrorCode, "The door was not found on this controller.");
        }

        return ApplyDoor(existing, isNew: false, number, name, entryReaderId, exitReaderId, lockOutputId,
            doorContactInputId, exitButtonInputId, strikeMinSeconds, strikeMaxSeconds, heldOpenSeconds, accessMode);
    }

    public Result RemoveDoor(Guid doorId)
    {
        if (_doors.FirstOrDefault(door => door.Id == doorId) is not { } door)
        {
            return Result.Failure(DoorNotFoundErrorCode, "The door was not found on this controller.");
        }

        _doors.Remove(door);
        return Result.Success();
    }

    // ---- Rules ----------------------------------------------------------------------------------------------

    /// <summary>A reader or a door point sits on this SIO.</summary>
    private bool IsSioInUse(SioPanel panel)
    {
        if (_readers.Any(reader => reader.SioPanelId == panel.Id))
        {
            return true;
        }

        var inputIds = panel.Inputs.Select(point => point.Id).ToHashSet();
        var outputIds = panel.Outputs.Select(point => point.Id).ToHashSet();
        return _doors.Any(door => outputIds.Contains(door.LockOutputId) || door.UsedInputIds().Any(inputIds.Contains));
    }

    private Result CheckReaderPlacement(Guid sioPanelId, short number, string name, Guid? excludeReaderId)
    {
        if (FindSio(sioPanelId) is not { } panel)
        {
            return Result.Failure(SioNotFoundErrorCode, "The SIO was not found on this controller.");
        }

        var slots = HardwareCatalog.For(panel.Model).ReaderSlots;
        if (slots == 0)
        {
            return Result.Failure(ValidationErrorCode, $"{HardwareCatalog.For(panel.Model).DisplayName} boards have no reader ports.");
        }

        if (number < 0 || number >= slots)
        {
            return Result.Failure(ValidationErrorCode, $"Reader number must be between 0 and {slots - 1} on this SIO.");
        }

        if (_readers.Any(reader => reader.Id != excludeReaderId && reader.SioPanelId == sioPanelId && reader.Number == number))
        {
            return Result.Failure(DuplicateReaderNumberErrorCode, "Another reader on this SIO already uses this number.");
        }

        var trimmed = Reader.NormalizeName(name);
        return _readers.Any(reader => reader.Id != excludeReaderId && string.Equals(reader.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            ? Result.Failure(DuplicateReaderNameErrorCode, "Another reader on this controller already uses this name.")
            : Result.Success();
    }

    private Result ApplyDoor(
        Door door,
        bool isNew,
        short number,
        string name,
        Guid entryReaderId,
        Guid? exitReaderId,
        Guid lockOutputId,
        Guid? doorContactInputId,
        Guid? exitButtonInputId,
        short strikeMinSeconds,
        short strikeMaxSeconds,
        short heldOpenSeconds,
        DoorAccessMode accessMode)
    {
        if (number < 0 || number >= HardwareCatalog.MaxDoorsPerScp)
        {
            return Result.Failure(ValidationErrorCode, $"Door number must be between 0 and {HardwareCatalog.MaxDoorsPerScp - 1}.");
        }

        var otherDoors = _doors.Where(other => isNew || other.Id != door.Id).ToList();

        if (otherDoors.Any(other => other.Number == number))
        {
            return Result.Failure(DuplicateDoorNumberErrorCode, "Another door on this controller already uses this number.");
        }

        // Exit readers occupy ACR numbers too (paired ACRs), so a door number must not collide with one.
        var otherAcrNumbers = otherDoors.SelectMany(other => other.UsedAcrNumbers()).ToHashSet();
        if (otherDoors.FirstOrDefault(other => other.ExitAcrNumber == number) is { } pairedDoor)
        {
            return Result.Failure(DuplicateDoorNumberErrorCode,
                $"ACR {number} is used by the exit reader of door {pairedDoor.Number} \"{pairedDoor.Name}\". Choose another number.");
        }

        var trimmed = Door.NormalizeName(name);
        if (otherDoors.Any(other => string.Equals(other.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(DuplicateDoorNameErrorCode, "Another door on this controller already uses this name.");
        }

        // Readers: on this controller, entry and exit different, not used by another door.
        if (_readers.All(reader => reader.Id != entryReaderId))
        {
            return Result.Failure(ReaderNotFoundErrorCode, "The entry reader was not found on this controller.");
        }

        if (exitReaderId is { } exitReader)
        {
            if (_readers.All(reader => reader.Id != exitReader))
            {
                return Result.Failure(ReaderNotFoundErrorCode, "The exit reader was not found on this controller.");
            }

            if (exitReader == entryReaderId)
            {
                return Result.Failure(ValidationErrorCode, "Entry and exit readers must be different readers.");
            }
        }

        var readersOfOtherDoors = otherDoors.SelectMany(other => other.UsedReaderIds()).ToHashSet();
        if (readersOfOtherDoors.Contains(entryReaderId) || (exitReaderId is { } exitId && readersOfOtherDoors.Contains(exitId)))
        {
            return Result.Failure(ReaderInUseErrorCode, "Another door already uses this reader.");
        }

        // Lock relay: on this controller and not another door's lock.
        if (_sioPanels.SelectMany(panel => panel.Outputs).All(point => point.Id != lockOutputId))
        {
            return Result.Failure(PointNotFoundErrorCode, "The lock relay was not found on this controller.");
        }

        if (otherDoors.Any(other => other.LockOutputId == lockOutputId))
        {
            return Result.Failure(PointInUseErrorCode, "Another door already uses this relay as its lock.");
        }

        // Inputs: on this controller, general purpose (not Tamper / AC / Battery), different, not used by another door.
        var inputs = _sioPanels.SelectMany(panel => panel.Inputs).ToDictionary(point => point.Id);
        var inputsOfOtherDoors = otherDoors.SelectMany(other => other.UsedInputIds()).ToHashSet();
        foreach (var (inputId, role) in new[] { (doorContactInputId, "door contact"), (exitButtonInputId, "exit button") })
        {
            if (inputId is not { } id)
            {
                continue;
            }

            if (!inputs.TryGetValue(id, out var input))
            {
                return Result.Failure(PointNotFoundErrorCode, $"The {role} input was not found on this controller.");
            }

            if (input.Dedicated != DedicatedInput.None)
            {
                return Result.Failure(ValidationErrorCode, $"{input.Name} is reserved for the enclosure or power supply and cannot be the {role}.");
            }

            if (inputsOfOtherDoors.Contains(id))
            {
                return Result.Failure(PointInUseErrorCode, $"Another door already uses {input.Name}.");
            }
        }

        if (doorContactInputId is { } contact && contact == exitButtonInputId)
        {
            return Result.Failure(ValidationErrorCode, "The door contact and the exit button must be different inputs.");
        }

        // The exit reader's paired ACR keeps its number while it stays valid; otherwise it takes the lowest free one.
        short? exitAcrNumber = null;
        if (exitReaderId is not null)
        {
            exitAcrNumber = door.ExitAcrNumber is { } current && current != number && !otherAcrNumbers.Contains(current)
                ? current
                : NextFreeDoorNumber(otherAcrNumbers.Append(number));

            if (exitAcrNumber is null)
            {
                return Result.Failure(DoorLimitErrorCode,
                    $"No free ACR is left for the exit reader; a controller has {HardwareCatalog.MaxDoorsPerScp} ACRs.");
            }
        }

        return door.Apply(number, name, entryReaderId, exitReaderId, exitAcrNumber, lockOutputId, doorContactInputId,
            exitButtonInputId, strikeMinSeconds, strikeMaxSeconds, heldOpenSeconds, accessMode);
    }
}
