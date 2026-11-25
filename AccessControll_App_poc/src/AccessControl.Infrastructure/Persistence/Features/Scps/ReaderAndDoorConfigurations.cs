using AccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControl.Infrastructure.Persistence.Features.Scps;

internal sealed class ReaderConfiguration : IEntityTypeConfiguration<Reader>
{
    public void Configure(EntityTypeBuilder<Reader> builder)
    {
        builder.ToTable("Readers");
        builder.HasKey(reader => reader.Id);

        builder.Property(reader => reader.Name).HasMaxLength(Reader.NameMaxLength).IsRequired();
        builder.Property(reader => reader.Model).HasMaxLength(Reader.ModelMaxLength).IsRequired();
        builder.Property(reader => reader.Interface).HasConversion<int>();
        builder.Property(reader => reader.OsdpBaudRate).HasConversion<int>();

        // Same aggregate: the controller cascade is the only delete path; the SIO link must not cascade too
        // (SQL Server rejects multiple cascade paths, and the domain refuses to remove an SIO that has readers).
        builder.HasOne<SioPanel>().WithMany().HasForeignKey(reader => reader.SioPanelId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(nameof(Reader.SioPanelId), nameof(Reader.Number)).HasDatabaseName("IX_Readers_SioPanelId_Number").IsUnique();
        builder.HasIndex("ScpId", nameof(Reader.Name)).HasDatabaseName("IX_Readers_ScpId_Name").IsUnique();
    }
}

internal sealed class DoorConfiguration : IEntityTypeConfiguration<Door>
{
    public void Configure(EntityTypeBuilder<Door> builder)
    {
        builder.ToTable("Doors");
        builder.HasKey(door => door.Id);

        builder.Property(door => door.Name).HasMaxLength(Door.NameMaxLength).IsRequired();
        builder.Property(door => door.AccessMode).HasConversion<int>();

        // References inside the controller aggregate never cascade; the domain keeps them consistent.
        builder.HasOne<Reader>().WithMany().HasForeignKey(door => door.EntryReaderId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Reader>().WithMany().HasForeignKey(door => door.ExitReaderId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<OutputPoint>().WithMany().HasForeignKey(door => door.LockOutputId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<InputPoint>().WithMany().HasForeignKey(door => door.DoorContactInputId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<InputPoint>().WithMany().HasForeignKey(door => door.ExitButtonInputId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex("ScpId", nameof(Door.Number)).HasDatabaseName("IX_Doors_ScpId_Number").IsUnique();
        builder.HasIndex("ScpId", nameof(Door.Name)).HasDatabaseName("IX_Doors_ScpId_Name").IsUnique();

        // Exit readers' paired ACRs are unique per controller; that they never equal a door number is a domain rule.
        builder.HasIndex("ScpId", nameof(Door.ExitAcrNumber)).HasDatabaseName("IX_Doors_ScpId_ExitAcrNumber").IsUnique()
            .HasFilter("[ExitAcrNumber] IS NOT NULL");

        // One door per reader / lock / input — the database backs up the domain rule.
        builder.HasIndex(door => door.EntryReaderId).HasDatabaseName("IX_Doors_EntryReaderId").IsUnique();
        builder.HasIndex(door => door.ExitReaderId).HasDatabaseName("IX_Doors_ExitReaderId").IsUnique().HasFilter("[ExitReaderId] IS NOT NULL");
        builder.HasIndex(door => door.LockOutputId).HasDatabaseName("IX_Doors_LockOutputId").IsUnique();
        builder.HasIndex(door => door.DoorContactInputId).HasDatabaseName("IX_Doors_DoorContactInputId").IsUnique().HasFilter("[DoorContactInputId] IS NOT NULL");
        builder.HasIndex(door => door.ExitButtonInputId).HasDatabaseName("IX_Doors_ExitButtonInputId").IsUnique().HasFilter("[ExitButtonInputId] IS NOT NULL");
    }
}
