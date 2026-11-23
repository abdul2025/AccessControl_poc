using AccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControl.Infrastructure.Persistence.Features.Scps;

internal sealed class ScpConfiguration : IEntityTypeConfiguration<Scp>
{
    public const string ScpNumberIndexName = "IX_Scps_ScpNumber";
    public const string SerialNumberIndexName = "IX_Scps_SerialNumber";
    public const string SiteNameIndexName = "IX_Scps_SiteId_Name";
    private const string NotDeletedFilter = "[IsDeleted] = 0";

    public void Configure(EntityTypeBuilder<Scp> builder)
    {
        builder.ToTable("Scps");
        builder.HasKey(scp => scp.Id);

        builder.Property(scp => scp.Name).HasMaxLength(Scp.NameMaxLength).IsRequired();
        builder.Property(scp => scp.Description).HasMaxLength(Scp.DescriptionMaxLength);
        builder.Property(scp => scp.IpAddress).HasMaxLength(Scp.HostMaxLength);
        builder.Property(scp => scp.Model).HasConversion<int>();
        builder.Property(scp => scp.ConnectionMode).HasConversion<int>();
        builder.Property(scp => scp.SyncStatus).HasConversion<int>();

        // Allocation is a value object stored as columns on the controller row (Allocation_Transactions, …).
        builder.ComplexProperty(scp => scp.Allocation, allocation =>
        {
            allocation.Property(value => value.Transactions).HasColumnName("Allocation_Transactions");
            allocation.Property(value => value.Cards).HasColumnName("Allocation_Cards");
            allocation.Property(value => value.AccessLevelsPerCard).HasColumnName("Allocation_AccessLevelsPerCard");
            allocation.Property(value => value.GmtOffsetSeconds).HasColumnName("Allocation_GmtOffsetSeconds");
        });

        // Reference to another aggregate: never cascade — rows are only soft-deleted.
        builder.HasOne<Site>().WithMany().HasForeignKey(scp => scp.SiteId).IsRequired().OnDelete(DeleteBehavior.Restrict);

        // SIO panels belong to the controller aggregate.
        builder.HasMany(scp => scp.SioPanels).WithOne().HasForeignKey("ScpId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(scp => scp.SioPanels).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Live rows only: a deleted controller's SCP number and serial can be reused.
        builder.HasIndex(scp => scp.ScpNumber).HasDatabaseName(ScpNumberIndexName).IsUnique().HasFilter(NotDeletedFilter);
        builder.HasIndex(scp => scp.SerialNumber)
            .HasDatabaseName(SerialNumberIndexName)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [SerialNumber] IS NOT NULL");
        builder.HasIndex(scp => new { scp.SiteId, scp.Name }).HasDatabaseName(SiteNameIndexName).IsUnique().HasFilter(NotDeletedFilter);
    }
}
