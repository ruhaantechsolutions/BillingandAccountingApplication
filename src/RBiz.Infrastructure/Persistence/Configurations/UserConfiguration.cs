using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RBiz.Domain.Entities;
using RBiz.Infrastructure.Persistence.Sync;

namespace RBiz.Infrastructure.Persistence.Configurations;

/// <summary>Schema for <see cref="User"/>.</summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        // Row version, tombstone columns, delta index and soft-delete filter.
        builder.ConfigureSyncable();

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);

        // An email is unique within a tenant, not globally: the same person may exist in
        // two different businesses. This index also serves the login lookup.
        builder.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique();

        builder.HasOne(u => u.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(u => u.TenantId)
            // Deleting a tenant must not silently delete its users and orphan their audit
            // history; the delete is blocked until the users are dealt with explicitly.
            .OnDelete(DeleteBehavior.Restrict);
    }
}
