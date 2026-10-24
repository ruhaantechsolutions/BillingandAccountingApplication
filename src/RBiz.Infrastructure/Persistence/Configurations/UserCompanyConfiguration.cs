using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Configurations;

/// <summary>Schema for <see cref="UserCompany"/>.</summary>
internal sealed class UserCompanyConfiguration : IEntityTypeConfiguration<UserCompany>
{
    public void Configure(EntityTypeBuilder<UserCompany> builder)
    {
        builder.ToTable("UserCompanies");

        builder.HasKey(uc => uc.Id);

        builder.Property(uc => uc.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(uc => uc.IsActive)
            .HasDefaultValue(true);

        // One membership row per person per company, enforced in the database so two
        // concurrent invitations cannot both succeed.
        builder.HasIndex(uc => new { uc.UserId, uc.CompanyId })
            .IsUnique()
            .HasDatabaseName("UX_UserCompanies_User_Company");

        // Serves the hot path: "which companies can this person reach", asked on sign-in
        // and on every company switch.
        builder.HasIndex(uc => new { uc.UserId, uc.IsActive })
            .HasDatabaseName("IX_UserCompanies_User_Active");

        // Serves the reverse: "who belongs to this company".
        builder.HasIndex(uc => new { uc.CompanyId, uc.IsActive })
            .HasDatabaseName("IX_UserCompanies_Company_Active");

        builder.HasOne(uc => uc.User)
            .WithMany(u => u.Companies)
            .HasForeignKey(uc => uc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(uc => uc.Company)
            .WithMany(c => c.Members)
            .HasForeignKey(uc => uc.CompanyId)
            // Restrict rather than cascade: removing a company that still has members
            // should be a deliberate act, not a side effect.
            .OnDelete(DeleteBehavior.Restrict);
    }
}
