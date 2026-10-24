using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RBiz.Domain.Sync;

namespace RBiz.Infrastructure.Persistence.Configurations;

/// <summary>Schema for the idempotency ledger.</summary>
internal sealed class SyncOperationConfiguration : IEntityTypeConfiguration<SyncOperation>
{
    public void Configure(EntityTypeBuilder<SyncOperation> builder)
    {
        builder.ToTable("SyncOperations");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.DeviceId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(o => o.EntityName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.Outcome)
            .HasConversion<int>()
            .IsRequired();

        // The guarantee that makes a push idempotent. Unique in the database rather than
        // checked in code, so two retries arriving at the same moment on different
        // servers cannot both get through.
        builder.HasIndex(o => new { o.CompanyId, o.OperationId })
            .IsUnique()
            .HasDatabaseName("UX_SyncOperations_Company_Operation");

        // Supports pruning old ledger rows once their retry window has passed.
        builder.HasIndex(o => o.AppliedAtUtc);
    }
}
