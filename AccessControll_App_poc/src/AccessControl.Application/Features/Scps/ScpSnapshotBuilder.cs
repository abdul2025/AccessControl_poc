using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AccessControl.Application.Features.Scps.DTOs;
using AccessControl.Domain.Entities;

namespace AccessControl.Application.Features.Scps;

/// <summary>
/// Turns the controller aggregate into its <see cref="ScpConfigurationSnapshot"/> and canonical JSON. Everything is
/// ordered by hardware position, so the same configuration always gives the same JSON and hash — which is how
/// "unpublished changes" is detected.
/// </summary>
public static class ScpSnapshotBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static ScpConfigurationSnapshot Build(Scp scp, int version)
    {
        var panelsById = scp.SioPanels.ToDictionary(panel => panel.Id);
        var inputsById = scp.SioPanels
            .SelectMany(panel => panel.Inputs.Select(point => (point.Id, Ref: new SnapshotPointRef(panel.Number, point.Number))))
            .ToDictionary(pair => pair.Id, pair => pair.Ref);
        var outputsById = scp.SioPanels
            .SelectMany(panel => panel.Outputs.Select(point => (point.Id, Ref: new SnapshotPointRef(panel.Number, point.Number))))
            .ToDictionary(pair => pair.Id, pair => pair.Ref);
        var readersById = scp.Readers.ToDictionary(
            reader => reader.Id,
            reader => new SnapshotPointRef(panelsById[reader.SioPanelId].Number, reader.Number));

        SnapshotPointRef? Optional(Dictionary<Guid, SnapshotPointRef> refs, Guid? id) => id is { } value ? refs[value] : null;

        var sios = scp.SioPanels
            .OrderBy(panel => panel.Number)
            .Select(panel => new SnapshotSio(
                panel.Number,
                panel.Model,
                panel.Port,
                panel.Address,
                panel.Name,
                panel.Inputs.OrderBy(point => point.Number)
                    .Select(point => new SnapshotInput(point.Number, point.Name, point.Contact, point.Supervised, point.Monitored, point.Dedicated))
                    .ToList(),
                panel.Outputs.OrderBy(point => point.Number)
                    .Select(point => new SnapshotOutput(point.Number, point.Name, point.PulseSeconds))
                    .ToList()))
            .ToList();

        var readers = scp.Readers
            .Select(reader => new SnapshotReader(
                panelsById[reader.SioPanelId].Number,
                reader.Number,
                reader.Name,
                reader.Model,
                reader.Interface,
                reader.OsdpAddress,
                reader.OsdpBaudRate,
                reader.SecureChannel,
                reader.HasKeypad))
            .OrderBy(reader => reader.Sio)
            .ThenBy(reader => reader.Number)
            .ToList();

        var doors = scp.Doors
            .OrderBy(door => door.Number)
            .Select(door => new SnapshotDoor(
                door.Number,
                door.Name,
                readersById[door.EntryReaderId],
                Optional(readersById, door.ExitReaderId),
                door.ExitAcrNumber,
                outputsById[door.LockOutputId],
                Optional(inputsById, door.DoorContactInputId),
                Optional(inputsById, door.ExitButtonInputId),
                door.StrikeMinSeconds,
                door.StrikeMaxSeconds,
                door.HeldOpenSeconds,
                door.AccessMode))
            .ToList();

        return new ScpConfigurationSnapshot(
            version,
            scp.ScpNumber,
            scp.Model,
            scp.ConnectionMode,
            new SnapshotAllocation(
                scp.Allocation.Transactions,
                scp.Allocation.Cards,
                scp.Allocation.AccessLevelsPerCard,
                scp.Allocation.GmtOffsetSeconds),
            sios,
            readers,
            doors);
    }

    public static string ToJson(ScpConfigurationSnapshot snapshot) => JsonSerializer.Serialize(snapshot, JsonOptions);

    public static ScpConfigurationSnapshot FromJson(string json) =>
        JsonSerializer.Deserialize<ScpConfigurationSnapshot>(json, JsonOptions)
        ?? throw new InvalidOperationException("The stored configuration snapshot is empty.");

    /// <summary>
    /// Hash of the configuration content. The version number is left out so an unchanged draft hashes the same as the
    /// version it was published as.
    /// </summary>
    public static string Hash(ScpConfigurationSnapshot snapshot) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ToJson(snapshot with { Version = 0 }))));
}
