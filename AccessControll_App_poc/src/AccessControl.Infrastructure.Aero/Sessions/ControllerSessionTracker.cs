using System.Collections.Concurrent;
using AccessControl.Infrastructure.Aero.Messages;

namespace AccessControl.Infrastructure.Aero.Sessions;

/// <summary>
/// Tracks each controller connection ("session") so the acceptance sequence runs exactly once per connection.
/// The driver reports Communication OK more than once while a controller connects; only the transition from
/// not-online to online starts a new session.
/// </summary>
public sealed class ControllerSessionTracker
{
    private readonly ConcurrentDictionary<int, SessionState> _sessions = new();

    /// <returns>True when this status starts a new session (the controller just came online).</returns>
    public bool OnCommStatus(CommStatusReply status)
    {
        var startedNewSession = false;
        _sessions.AddOrUpdate(
            status.ScpNumber,
            _ =>
            {
                startedNewSession = status.IsOnline;
                return new SessionState(status.IsOnline, AcceptanceStarted: false);
            },
            (_, current) =>
            {
                if (status.IsOnline && !current.Online)
                {
                    startedNewSession = true;
                    return new SessionState(Online: true, AcceptanceStarted: false);
                }

                return status.IsOnline ? current : new SessionState(Online: false, AcceptanceStarted: false);
            });

        return startedNewSession;
    }

    /// <summary>
    /// Claims the acceptance of the current session. True only for the first caller per session; an ID report
    /// that arrives before any comm status is treated as the start of a session.
    /// </summary>
    public bool TryBeginAcceptance(int scpNumber)
    {
        var claimed = false;
        _sessions.AddOrUpdate(
            scpNumber,
            _ =>
            {
                claimed = true;
                return new SessionState(Online: true, AcceptanceStarted: true);
            },
            (_, current) =>
            {
                if (current.AcceptanceStarted)
                {
                    return current;
                }

                claimed = true;
                return new SessionState(Online: true, AcceptanceStarted: true);
            });

        return claimed;
    }

    private sealed record SessionState(bool Online, bool AcceptanceStarted);
}
