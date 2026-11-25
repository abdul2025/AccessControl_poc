using AccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControl.Infrastructure.Persistence.Features.ScpProvisioning;

internal sealed class ScpConfigVersionConfiguration : IEntityTypeConfiguration<ScpConfigVersion>
{
    public const string ScpVersionIndexName = "IX_ScpConfigVersions_ScpId_Version";

    public void Configure(EntityTypeBuilder<ScpConfigVersion> builder)
    {
        builder.ToTable("ScpConfigVersions");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Id).ValueGeneratedOnAdd();

        builder.Property(version => version.PublishedBy).HasMaxLength(ScpConfigVersion.PublishedByMaxLength);
        builder.Property(version => version.SnapshotJson).IsRequired();
        builder.Property(version => version.SnapshotHash).HasMaxLength(ScpConfigVersion.SnapshotHashLength).IsUnicode(false).IsRequired();
        builder.Property(version => version.CommandHash).HasMaxLength(ScpConfigVersion.SnapshotHashLength).IsUnicode(false);
        builder.Property(version => version.CompileError).HasMaxLength(ScpConfigVersion.CompileErrorMaxLength);

        // Versions are history: controllers are only soft-deleted, so nothing cascades.
        builder.HasOne<Scp>().WithMany().HasForeignKey(version => version.ScpId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(version => new { version.ScpId, version.Version }).HasDatabaseName(ScpVersionIndexName).IsUnique();
    }
}

internal sealed class ScpConfigCommandConfiguration : IEntityTypeConfiguration<ScpConfigCommandRecord>
{
    public void Configure(EntityTypeBuilder<ScpConfigCommandRecord> builder)
    {
        builder.ToTable("ScpConfigCommands");
        builder.HasKey(command => new { command.VersionId, command.Order });

        builder.Property(command => command.Target).HasMaxLength(ScpConfigCommandRecord.TargetMaxLength).IsRequired();
        builder.Property(command => command.Text).HasMaxLength(ScpConfigCommandRecord.TextMaxLength).IsUnicode(false).IsRequired();

        // The compiled list belongs to its version.
        builder.HasOne<ScpConfigVersion>().WithMany().HasForeignKey(command => command.VersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ScpApplyRunConfiguration : IEntityTypeConfiguration<ScpApplyRunRecord>
{
    public void Configure(EntityTypeBuilder<ScpApplyRunRecord> builder)
    {
        builder.ToTable("ScpApplyRuns");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id).ValueGeneratedOnAdd();
        builder.Property(run => run.Outcome).HasConversion<int>();
        builder.Property(run => run.Reason).HasMaxLength(ScpApplyRunRecord.ReasonMaxLength);

        builder.HasOne<Scp>().WithMany().HasForeignKey(run => run.ScpId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(run => new { run.ScpId, run.StartedAtUtc }).HasDatabaseName("IX_ScpApplyRuns_ScpId_StartedAtUtc");
    }
}

internal sealed class ScpApplyResultConfiguration : IEntityTypeConfiguration<ScpApplyResultRecord>
{
    public void Configure(EntityTypeBuilder<ScpApplyResultRecord> builder)
    {
        builder.ToTable("ScpApplyResults");
        builder.HasKey(result => new { result.RunId, result.Order });
        builder.Property(result => result.Target).HasMaxLength(ScpConfigCommandRecord.TargetMaxLength).IsRequired();
        builder.Property(result => result.Result).HasConversion<int>();
        builder.Property(result => result.Detail).HasMaxLength(ScpApplyResultRecord.DetailMaxLength);

        builder.HasOne<ScpApplyRunRecord>().WithMany().HasForeignKey(result => result.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ScpConnectionStateConfiguration : IEntityTypeConfiguration<ScpConnectionStateRecord>
{
    public void Configure(EntityTypeBuilder<ScpConnectionStateRecord> builder)
    {
        builder.ToTable("ScpConnectionStates");
        builder.HasKey(state => state.ScpId);
        builder.Property(state => state.FirmwareVersion).HasMaxLength(ScpConnectionStateRecord.FirmwareMaxLength);
        builder.Property(state => state.MacAddress).HasMaxLength(ScpConnectionStateRecord.MacMaxLength).IsUnicode(false);
        builder.Property(state => state.ApplyState).HasConversion<int>();
        builder.Property(state => state.LastError).HasMaxLength(ScpConnectionStateRecord.LastErrorMaxLength);

        builder.HasOne<Scp>().WithOne().HasForeignKey<ScpConnectionStateRecord>(state => state.ScpId).OnDelete(DeleteBehavior.Restrict);
    }
}
