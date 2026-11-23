using AccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControl.Infrastructure.Persistence.Features.Scps;

internal sealed class InputPointConfiguration : IEntityTypeConfiguration<InputPoint>
{
    public void Configure(EntityTypeBuilder<InputPoint> builder)
    {
        builder.ToTable("InputPoints");
        builder.HasKey(point => point.Id);

        builder.Property(point => point.Name).HasMaxLength(InputPoint.NameMaxLength).IsRequired();
        builder.Property(point => point.Contact).HasConversion<int>();
        builder.Property(point => point.Dedicated).HasConversion<int>();

        // Input numbers are unique on their SIO (command 110 input).
        builder.HasIndex("SioPanelId", nameof(InputPoint.Number)).HasDatabaseName("IX_InputPoints_SioPanelId_Number").IsUnique();
    }
}

internal sealed class OutputPointConfiguration : IEntityTypeConfiguration<OutputPoint>
{
    public void Configure(EntityTypeBuilder<OutputPoint> builder)
    {
        builder.ToTable("OutputPoints");
        builder.HasKey(point => point.Id);

        builder.Property(point => point.Name).HasMaxLength(OutputPoint.NameMaxLength).IsRequired();

        // Output numbers are unique on their SIO (command 111 output).
        builder.HasIndex("SioPanelId", nameof(OutputPoint.Number)).HasDatabaseName("IX_OutputPoints_SioPanelId_Number").IsUnique();
    }
}
