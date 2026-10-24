using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RBiz.Domain.Entities;
using RBiz.Infrastructure.Persistence.Sync;

namespace RBiz.Infrastructure.Persistence.Configurations;

/// <summary>Schema for <see cref="Location"/>.</summary>
internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("Locations");

        builder.HasKey(l => l.Id);

        builder.ConfigureSyncable();

        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.IsActive).HasDefaultValue(true);

        // At most one primary location per company, enforced by a filtered unique index so
        // the rule holds under concurrent writes rather than only in application code.
        builder.HasIndex(l => l.CompanyId)
            .IsUnique()
            .HasFilter("[IsPrimary] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Locations_Company_Primary");

        builder.HasOne(l => l.Company)
            .WithMany(c => c.Locations)
            .HasForeignKey(l => l.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
