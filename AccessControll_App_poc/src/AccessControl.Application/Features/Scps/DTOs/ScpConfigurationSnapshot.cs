using AccessControl.Domain.Enums.Scps;

namespace AccessControl.Application.Features.Scps.DTOs;

/// <summary>
/// The controller's hardware configuration as published (spec §3.7 rule 1): everything the Gateway needs to build the
/// SDK command list, with references expressed as hardware positions (SIO + number) instead of database ids, so the
/// snapshot stands on its own. Stored as JSON on the version row; never changed after publishing.
/// </summary>
public sealed record ScpConfigurationSnapshot(
    int Version,
    short ScpNumber,
    ScpModel Model,
    ConnectionMode ConnectionMode,
    SnapshotAllocation Allocation,
    IReadOnlyList<SnapshotSio> Sios,
    IReadOnlyList<SnapshotReader> Readers,
    IReadOnlyList<SnapshotDoor> Doors);

public sealed record SnapshotAllocation(int Transactions, int Cards, short AccessLevelsPerCard, int GmtOffsetSeconds);

public sealed record SnapshotSio(
    short Number,
    SioModel Model,
    SioPort Port,
    short Address,
    string Name,
    IReadOnlyList<SnapshotInput> Inputs,
    IReadOnlyList<SnapshotOutput> Outputs);

public sealed record SnapshotInput(short Number, string Name, InputContact Contact, bool Supervised, bool Monitored, DedicatedInput Dedicated);

public sealed record SnapshotOutput(short Number, string Name, short PulseSeconds);

/// <summary>A point or reader on a given SIO.</summary>
public sealed record SnapshotPointRef(short Sio, short Number);

public sealed record SnapshotReader(
    short Sio,
    short Number,
    string Name,
    string Model,
    ReaderInterface Interface,
    short OsdpAddress,
    OsdpBaudRate OsdpBaudRate,
    bool SecureChannel,
    bool HasKeypad);

/// <summary>A door; <see cref="ExitAcrNumber"/> is set exactly when <see cref="ExitReader"/> is (paired ACRs).</summary>
public sealed record SnapshotDoor(
    short Number,
    string Name,
    SnapshotPointRef EntryReader,
    SnapshotPointRef? ExitReader,
    short? ExitAcrNumber,
    SnapshotPointRef Lock,
    SnapshotPointRef? DoorContact,
    SnapshotPointRef? ExitButton,
    short StrikeMinSeconds,
    short StrikeMaxSeconds,
    short HeldOpenSeconds,
    DoorAccessMode AccessMode);
