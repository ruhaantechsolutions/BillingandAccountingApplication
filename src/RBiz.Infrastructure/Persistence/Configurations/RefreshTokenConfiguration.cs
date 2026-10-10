using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Configurations;

/// <summary>Schema for <see cref="RefreshToken"/>.</summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(t => t.Id);

        // Hex-encoded SHA-256 is always 64 characters.
        builder.Property(t => t.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.DeviceId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.RevokedReason)
            .HasConversion<int?>();

        // Every refresh is a lookup by hash, so this index is on the hot path. Unique
        // because two tokens hashing alike would mean a SHA-256 collision.
        builder.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_RefreshTokens_TokenHash");

        // Serves the family revocation that runs when a replay is detected.
        builder.HasIndex(t => t.FamilyId);

        // Supports pruning tokens that have expired.
        builder.HasIndex(t => t.ExpiresAtUtc);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            // Tokens are meaningless without their user and hold nothing of record
            // value, so they go when the user is genuinely removed.
            .OnDelete(DeleteBehavior.Cascade);
    }
}
