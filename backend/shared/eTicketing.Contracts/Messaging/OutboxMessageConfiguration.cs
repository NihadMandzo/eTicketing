using eTicketing.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Contracts.Messaging;

/// <summary>
/// Shared EF configuration for <see cref="OutboxMessage"/>. Each service that keeps an outbox
/// applies this from its own DbContext, so the table is identical everywhere and a future
/// dispatcher change cannot mean three different schemas.
/// </summary>
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(m => m.RoutingKey).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000);

        // The dispatcher's only query: oldest first, so events reach the broker in the order the
        // transactions that produced them committed.
        builder.HasIndex(m => m.CreatedAt);
    }
}
