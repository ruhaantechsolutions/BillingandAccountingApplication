using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RBiz.Domain.Entities;

namespace RBiz.Infrastructure.Persistence.Configurations;

/// <summary>Schema for <see cref="AuditLog"/>.</summary>
internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasMaxLength(100)
            .IsRequired();

        // Stored as the enum's numeric value rather than text: narrower rows and a
        // cheaper index than persisting the name.
        builder.Property(a => a.Action)
            .HasConversion<int>()
            .IsRequired();

        // JSON payloads of the changed columns. No length cap, as a wide row could
        // otherwise be silently truncated and break the trail.
        builder.Property(a => a.OldValues);
        builder.Property(a => a.NewValues);

        // Supports the only query pattern there is: newest-first for one tenant.
        builder.HasIndex(a => new { a.TenantId, a.TimestampUtc })
            .IsDescending(false, true);

        // No foreign keys to Tenant or User on purpose: the trail must survive even if
        // the row it refers to is later removed.
    }
}
