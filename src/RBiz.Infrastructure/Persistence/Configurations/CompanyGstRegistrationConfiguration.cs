using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RBiz.Domain.Entities;
using RBiz.Domain.Utilities;
using RBiz.Infrastructure.Persistence.Sync;

namespace RBiz.Infrastructure.Persistence.Configurations;

/// <summary>Schema for <see cref="CompanyGstRegistration"/>.</summary>
internal sealed class CompanyGstRegistrationConfiguration : IEntityTypeConfiguration<CompanyGstRegistration>
{
    public void Configure(EntityTypeBuilder<CompanyGstRegistration> builder)
    {
        builder.ToTable("CompanyGstRegistrations");

        builder.HasKey(g => g.Id);

        builder.ConfigureSyncable();

        builder.Property(g => g.Gstin)
            .HasMaxLength(GstinValidator.GstinLength)
            .IsRequired();

        builder.Property(g => g.RegistrationType).HasConversion<int>().IsRequired();
        builder.Property(g => g.VerificationStatus).HasConversion<int>().IsRequired();

        // One registration per company for now; multiple state registrations get their own
        // rows when the product supports them.
        builder.HasIndex(g => g.CompanyId)
            .IsUnique()
            .HasDatabaseName("UX_CompanyGstRegistrations_Company");

        // Answers "does any company of this user already use this GSTIN" without a scan.
        // Deliberately NOT unique: the same registration can legitimately appear under two
        // accounts, and the duplicate warning is a confirmation, not a prohibition.
        builder.HasIndex(g => g.Gstin)
            .HasDatabaseName("IX_CompanyGstRegistrations_Gstin");
    }
}
