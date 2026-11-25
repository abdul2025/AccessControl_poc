using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Application.Features.Scps.DTOs;

public sealed record ReaderDto(
    Guid Id,
    Guid SioPanelId,
    short SioNumber,
    short Number,
    string Name,
    string Model,
    ReaderInterface Interface,
    short OsdpAddress,
    OsdpBaudRate OsdpBaudRate,
    bool SecureChannel,
    bool HasKeypad);

/// <summary>A door with the names of the reader(s) and points it uses, for display.</summary>
public sealed record DoorDto(
    Guid Id,
    short Number,
    string Name,
    Guid EntryReaderId,
    string EntryReaderName,
    Guid? ExitReaderId,
    string? ExitReaderName,
    short? ExitAcrNumber,
    Guid LockOutputId,
    string LockOutputName,
    Guid? DoorContactInputId,
    string? DoorContactInputName,
    Guid? ExitButtonInputId,
    string? ExitButtonInputName,
    short StrikeMinSeconds,
    short StrikeMaxSeconds,
    short HeldOpenSeconds,
    DoorAccessMode AccessMode);

/// <summary>Add or change a reader; carries the controller's row version (code-style pattern 1).</summary>
public sealed record ReaderWriteDto(
    Guid SioPanelId,
    short Number,
    string Name,
    string? Model,
    ReaderInterface Interface,
    short OsdpAddress,
    OsdpBaudRate OsdpBaudRate,
    bool SecureChannel,
    bool HasKeypad,
    byte[] RowVersion);

/// <summary>Add or change a door; carries the controller's row version (code-style pattern 1).</summary>
public sealed record DoorWriteDto(
    short Number,
    string Name,
    Guid EntryReaderId,
    Guid? ExitReaderId,
    Guid LockOutputId,
    Guid? DoorContactInputId,
    Guid? ExitButtonInputId,
    short StrikeMinSeconds,
    short StrikeMaxSeconds,
    short HeldOpenSeconds,
    DoorAccessMode AccessMode,
    byte[] RowVersion);
