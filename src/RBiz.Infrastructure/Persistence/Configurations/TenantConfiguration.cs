using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RBiz.Domain.Entities;
using RBiz.Infrastructure.Persistence.Sync;

namespace RBiz.Infrastructure.Persistence.Configurations;

/// <summary>
/// Schema for <see cref="Tenant"/>. Kept in its own class rather than in OnModelCreating
/// so the mapping for each table stays easy to find as the model grows.
/// </summary>
internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        // Row version, tombstone columns, delta index and soft-delete filter.
        builder.ConfigureSyncable();

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Code)
            .HasMaxLength(50)
            .IsRequired();

        // Enforced in the database, not just in code: two tenants can never share a code
        // even under concurrent inserts.
        builder.HasIndex(t => t.Code)
            .IsUnique();

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true);
    }
}
