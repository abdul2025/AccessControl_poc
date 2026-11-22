using System.Collections.Concurrent;
using AccessControl.Infrastructure.Aero.Commands;

namespace AccessControl.Infrastructure.Aero.Driver;

/// <summary>A command posted to a controller and still waiting for its command status reply.</summary>
public sealed record PendingCommand(int ScpNumber, int Tag, AeroCommand Command, DateTimeOffset PostedAtUtc);

/// <summary>Matches command status replies (by SCP and tag) to the commands that were posted.</summary>
public sealed class PendingCommandRegistry
{
    private readonly ConcurrentDictionary<(int ScpNumber, int Tag), PendingCommand> _pending = new();

    public void Register(PendingCommand command) => _pending[(command.ScpNumber, command.Tag)] = command;

    /// <returns>The posted command, or <c>null</c> when the tag is unknown (e.g. posted by another tool).</returns>
    public PendingCommand? Complete(int scpNumber, int tag) =>
        _pending.TryRemove((scpNumber, tag), out var command) ? command : null;

    /// <summary>Removes and returns commands that received no status within <paramref name="timeout"/>.</summary>
    public IReadOnlyList<PendingCommand> RemoveExpired(DateTimeOffset nowUtc, TimeSpan timeout)
    {
        List<PendingCommand> expired = [];
        foreach (var (key, command) in _pending)
        {
            if (nowUtc - command.PostedAtUtc >= timeout && _pending.TryRemove(key, out var removed))
            {
                expired.Add(removed);
            }
        }

        return expired;
    }
}
