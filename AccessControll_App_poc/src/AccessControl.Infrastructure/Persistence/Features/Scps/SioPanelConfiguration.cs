using AccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControl.Infrastructure.Persistence.Features.Scps;

internal sealed class SioPanelConfiguration : IEntityTypeConfiguration<SioPanel>
{
    public const string ScpNumberIndexName = "IX_SioPanels_ScpId_Number";

    public void Configure(EntityTypeBuilder<SioPanel> builder)
    {
        builder.ToTable("SioPanels");
        builder.HasKey(panel => panel.Id);

        builder.Property(panel => panel.Name).HasMaxLength(SioPanel.NameMaxLength).IsRequired();
        builder.Property(panel => panel.Model).HasConversion<int>();
        builder.Property(panel => panel.Port).HasConversion<int>();

        // SIO numbers are unique within their controller (command 109 sio_number).
        builder.HasIndex("ScpId", nameof(SioPanel.Number)).HasDatabaseName(ScpNumberIndexName).IsUnique();

        // Points belong to their SIO: removing an SIO from the controller removes its points.
        builder.HasMany(panel => panel.Inputs).WithOne().HasForeignKey("SioPanelId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(panel => panel.Inputs).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(panel => panel.Outputs).WithOne().HasForeignKey("SioPanelId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(panel => panel.Outputs).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
