using System.Collections.Concurrent;
using AccessControl.Domain.Common.Results;
using AccessControl.Infrastructure.Aero.Commands;
using HID.Aero.ScpdNet.Wrapper;

namespace AccessControl.Infrastructure.Aero.Driver;

/// <summary>
/// Posts structure-based commands to a controller and records their tag so the command status reply can be
/// matched. Every controller-bound command must go through here: the wrapper has no "post with tag" call
/// for structures, so the tag is read with scpGetTagLastPosted while a per-SCP lock is held.
/// </summary>
public sealed class AeroCommandWriter(PendingCommandRegistry registry, TimeProvider timeProvider)
{
    public const string PostRejectedErrorCode = "Aero.PostRejected";

    private readonly ConcurrentDictionary<int, Lock> _scpLocks = new();

    /// <returns>The driver tag of the posted command, or a failure when the driver refused to queue it.</returns>
    public Result<int> Post(short scpNumber, AeroCommand command, IConfigCommand payload)
    {
        lock (_scpLocks.GetOrAdd(scpNumber, _ => new Lock()))
        {
            if (!new SCPConfig().scpCfgCmndEx((short)command, payload))
            {
                return Result<int>.Failure(
                    PostRejectedErrorCode,
                    $"The driver did not queue {command.Describe()} for SCP {scpNumber} (post error {SCPDLL.scpGetPostError()}).");
            }

            var tag = SCPDLL.scpGetTagLastPosted(scpNumber);
            registry.Register(new PendingCommand(scpNumber, tag, command, timeProvider.GetUtcNow()));
            return Result<int>.Success(tag);
        }
    }
}
