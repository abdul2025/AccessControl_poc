using AccessControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControl.Infrastructure.Persistence.Features.Sites;

internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public const string CodeIndexName = "IX_Sites_Code";
    public const string NameIndexName = "IX_Sites_Name";
    private const string NotDeletedFilter = "[IsDeleted] = 0";

    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("Sites");
        builder.HasKey(site => site.Id);

        builder.Property(site => site.Code).HasMaxLength(Site.CodeMaxLength).IsRequired();
        builder.Property(site => site.Name).HasMaxLength(Site.NameMaxLength).IsRequired();
        builder.Property(site => site.Description).HasMaxLength(Site.DescriptionMaxLength);

        // Uniqueness applies to live rows only, so the code / name of a soft-deleted site can be reused.
        builder.HasIndex(site => site.Code).HasDatabaseName(CodeIndexName).IsUnique().HasFilter(NotDeletedFilter);
        builder.HasIndex(site => site.Name).HasDatabaseName(NameIndexName).IsUnique().HasFilter(NotDeletedFilter);
    }
}
