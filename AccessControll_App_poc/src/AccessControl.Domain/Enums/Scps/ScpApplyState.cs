namespace AccessControl.Domain.Enums.Scps;

/// <summary>What the Gateway is doing with a controller's configuration right now.</summary>
public enum ScpApplyState
{
    Idle = 1,
    Applying = 2,
    Failed = 3,
}

/// <summary>How one apply run (connect → verify → download) ended.</summary>
public enum ScpApplyOutcome
{
    Running = 1,
    Succeeded = 2,

    /// <summary>The controller already held the published version (config marker matched); nothing was sent.</summary>
    UpToDate = 3,

    Failed = 4,

    /// <summary>The connection dropped before the run finished; it starts again on the next connection.</summary>
    Interrupted = 5,
}

/// <summary>Result of one command sent during an apply run.</summary>
public enum ScpCommandResult
{
    Ok = 1,
    Nak = 2,
    NoReply = 3,
    PostRejected = 4,
}
