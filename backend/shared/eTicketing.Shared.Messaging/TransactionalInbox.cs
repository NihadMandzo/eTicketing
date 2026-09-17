using eTicketing.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Shared.Messaging;

/// <summary>
/// <see cref="IInbox"/> over the consuming service's own database: the other half of the
/// transactional outbox.
///
/// <para><b>One transaction per message.</b> The <see cref="InboxMessage"/> row is inserted first,
/// then the handler runs, then both commit together. Every <c>SaveChangesAsync</c> the handler
/// makes — and every outbox row it publishes — joins that transaction, so a handler that fails
/// partway leaves nothing behind: not its writes, not its outbox rows, and not the record that would
/// make the retry skip it.</para>
///
/// <para><b>Inserted first, not last, on purpose.</b> The insert takes the key lock for the rest
/// of the transaction, so a second copy of the same message arriving meanwhile waits on it instead
/// of running the handler in parallel. When the first commits, the second's insert fails on the key
/// and it is told the message is a duplicate; when the first rolls back, the second goes ahead.
/// Checking for the row first is only a shortcut for the common case — the key is what actually
/// decides.</para>
///
/// <para><b>Anything outside the database is outside the transaction.</b> A handler that also
/// touches Redis or calls another service does that part immediately and cannot have it rolled
/// back. Such a step has to be safe to repeat on its own, as it already had to be before the inbox
/// existed.</para>
///
/// <para>Generic over the context for the same reason <see cref="OutboxEventPublisher{TContext}"/>
/// is: DI resolves the service's own scoped context, which the handler's repositories share.</para>
/// </summary>
public sealed class TransactionalInbox<TContext> : IInbox
    where TContext : DbContext
{
    private readonly TContext _context;

    public TransactionalInbox(TContext context)
    {
        _context = context;
    }

    public async Task<bool> ProcessOnceAsync(
        string? messageId, string consumer, Func<CancellationToken, Task> handler, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            await handler(ct);
            return true;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        if (await IsRecordedAsync(messageId, consumer, ct))
            return false;

        var record = new InboxMessage { MessageId = messageId, Consumer = consumer };
        _context.Set<InboxMessage>().Add(record);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            Forget(record);
            await transaction.RollbackAsync(ct);

            // Most likely a concurrent copy of this message committed its record between the check
            // above and this insert. Anything else — the database going away, say — is a real
            // failure and goes to the consumer's retry like any other.
            if (await IsRecordedAsync(messageId, consumer, ct))
                return false;

            throw;
        }

        try
        {
            await handler(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            // The transaction rolls back on dispose. The record is detached so a context that
            // outlives this call — a test's, typically, since production opens a scope per message —
            // does not go on believing it saved a row that no longer exists.
            Forget(record);
            throw;
        }

        return true;
    }

    private Task<bool> IsRecordedAsync(string messageId, string consumer, CancellationToken ct) =>
        _context.Set<InboxMessage>()
            .AsNoTracking()
            .AnyAsync(m => m.MessageId == messageId && m.Consumer == consumer, ct);

    private void Forget(InboxMessage record) => _context.Entry(record).State = EntityState.Detached;
}
