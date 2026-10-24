using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RBiz.Domain.Entities;
using RBiz.Infrastructure.Persistence.Sync;

namespace RBiz.Infrastructure.Persistence.Configurations;

/// <summary>Schema for <see cref="CompanyAddress"/>.</summary>
internal sealed class CompanyAddressConfiguration : IEntityTypeConfiguration<CompanyAddress>
{
    public void Configure(EntityTypeBuilder<CompanyAddress> builder)
    {
        builder.ToTable("CompanyAddresses");

        builder.HasKey(a => a.Id);

        builder.ConfigureSyncable();

        builder.Property(a => a.Line1).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Line2).HasMaxLength(200);
        builder.Property(a => a.City).HasMaxLength(100).IsRequired();
        builder.Property(a => a.State).HasMaxLength(100).IsRequired();
        builder.Property(a => a.PinCode).HasMaxLength(6).IsRequired();
        builder.Property(a => a.Country).HasMaxLength(100).IsRequired().HasDefaultValue("India");

        // One address per company for now. Additional addresses become their own rows when
        // the product supports a separate billing or registered address.
        builder.HasIndex(a => a.CompanyId)
            .IsUnique()
            .HasDatabaseName("UX_CompanyAddresses_Company");

        // Deliberately no foreign key on CopiedFromCompanyId: it is a note about where the
        // values came from, and must not create a relationship that could be followed back
        // to the source company's current address.
    }
}
