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

        builder.Property(u => u.Mobile)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(256);

        builder.Property(u => u.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);

        // The login identity, so uniqueness is global rather than per company: sign-in
        // asks only for a number and a password and has no company to scope it by.
        builder.HasIndex(u => u.Mobile)
            .IsUnique()
            .HasDatabaseName("UX_Users_Mobile");

        // Email is optional, so the index ignores rows without one. Without the filter a
        // second user with no email would collide with the first.
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("[Email] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Users_Email");

    }
}
