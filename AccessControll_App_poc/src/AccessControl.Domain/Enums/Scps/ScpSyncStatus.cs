namespace AccessControl.Domain.Enums.Scps;

/// <summary>Whether the controller holds the published configuration (spec §3.7 rule 2). Derived, never stored.</summary>
public enum ScpSyncStatus
{
    NotPublished = 1,
    Pending = 2,
    Applying = 3,
    InSync = 4,
    Failed = 5,
}

/// <summary>
/// Derives the sync status from what the user published and what the Gateway reports. The two halves live in
/// different rows on purpose: users write the controller, the Gateway writes its connection state.
/// </summary>
public static class ScpSyncStatuses
{
    public static ScpSyncStatus Derive(int? publishedVersion, int? appliedVersion, ScpApplyState? applyState) =>
        publishedVersion is null ? ScpSyncStatus.NotPublished
        : applyState == ScpApplyState.Applying ? ScpSyncStatus.Applying
        : applyState == ScpApplyState.Failed ? ScpSyncStatus.Failed
        : appliedVersion == publishedVersion ? ScpSyncStatus.InSync
        : ScpSyncStatus.Pending;
}
