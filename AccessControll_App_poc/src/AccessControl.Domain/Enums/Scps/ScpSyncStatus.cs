namespace AccessControl.Domain.Enums.Scps;

/// <summary>Whether the controller holds the published configuration (spec §3.7 rule 2).</summary>
public enum ScpSyncStatus
{
    NotPublished = 1,
    Pending = 2,
    Applying = 3,
    InSync = 4,
    Failed = 5,
}
