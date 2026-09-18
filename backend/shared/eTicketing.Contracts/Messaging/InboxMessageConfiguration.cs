using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eTicketing.Contracts.Messaging;

/// <summary>
/// Shared EF configuration for <see cref="InboxMessage"/>, applied by every service that consumes
/// through the inbox — same arrangement as <see cref="OutboxMessageConfiguration"/>.
/// </summary>
public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        // The key is the deduplication. Two copies of one message racing each other cannot both
        // insert this row, so the loser learns it is a duplicate from the database itself rather
        // than from a check that a concurrent writer could slip past.
        builder.HasKey(m => new { m.MessageId, m.Consumer });

        builder.Property(m => m.MessageId).HasMaxLength(128);
        builder.Property(m => m.Consumer).HasMaxLength(100);

        // The cleanup job's only query: everything processed before the retention cut-off.
        builder.HasIndex(m => m.CreatedAt);
    }
}
