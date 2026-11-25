using AccessControl.Domain.Common.Entities;

namespace AccessControl.Domain.Entities;

/// <summary>
/// An immutable published configuration of one controller (spec §3.7 rule 1). The snapshot is what the Gateway
/// compiles into SDK commands, so edits made after publishing never reach the hardware until the next publish.
/// The compile fields are filled in later by the Gateway (docs/plans/step-4-publish-and-provisioning.md, milestone 4b-2).
/// </summary>
public sealed class ScpConfigVersion : BaseEntity
{
    public const int SnapshotHashLength = 64;   // SHA-256, hex
    public const int PublishedByMaxLength = 450;
    public const int CompileErrorMaxLength = 2000;

    private ScpConfigVersion()
    {
    }

    public Guid ScpId { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset PublishedAtUtc { get; private set; }
    public string? PublishedBy { get; private set; }

    /// <summary>The controller's hardware configuration at publish time, as JSON.</summary>
    public string SnapshotJson { get; private set; } = null!;

    /// <summary>SHA-256 of <see cref="SnapshotJson"/>; compared with the draft to show "unpublished changes".</summary>
    public string SnapshotHash { get; private set; } = null!;

    public DateTimeOffset? CompiledAtUtc { get; private set; }

    /// <summary>SHA-256 of the compiled command list; with <see cref="Version"/> it forms the controller's config marker.</summary>
    public string? CommandHash { get; private set; }

    public string? CompileError { get; private set; }

    public static ScpConfigVersion Create(
        Guid scpId,
        int version,
        string snapshotJson,
        string snapshotHash,
        DateTimeOffset publishedAtUtc,
        string? publishedBy) => new()
    {
        ScpId = scpId,
        Version = version,
        SnapshotJson = snapshotJson,
        SnapshotHash = snapshotHash,
        PublishedAtUtc = publishedAtUtc,
        PublishedBy = publishedBy,
    };
}
