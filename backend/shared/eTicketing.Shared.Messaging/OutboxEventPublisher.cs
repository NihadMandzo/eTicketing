using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Shared.Messaging;

/// <summary>
/// The write-path <see cref="IEventPublisher"/>: it does not publish anything. It adds an
/// <see cref="OutboxMessage"/> to the caller's own <see cref="DbContext"/> and returns.
///
/// <para>That is the whole trick. The row is not saved here — it joins whatever
/// <c>SaveChangesAsync</c> the calling service method was already going to make, so the event and
/// the data that caused it commit together or not at all. Which means <b>every publish call must
/// now happen before that save</b>, not after it as they all used to; a call made afterwards
/// leaves a row that nothing commits, and the event silently never happens. Nothing enforces the
/// ordering at compile time — the tests in each service assert it instead.</para>
///
/// <para>Generic over the context rather than taking a <c>DbContext</c> directly so DI resolves the
/// service's own registered context; there is one per service and no shared base.</para>
/// </summary>
public sealed class OutboxEventPublisher<TContext> : IEventPublisher
    where TContext : DbContext
{
    private readonly TContext _context;

    public OutboxEventPublisher(TContext context)
    {
        _context = context;
    }

    public Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default)
    {
        _context.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            RoutingKey = routingKey,
            Payload = JsonSerializer.Serialize(message),
        });

        // Deliberately synchronous work behind an async signature: the interface is shared with the
        // direct publisher, which genuinely awaits a broker. Adding to the change tracker does not.
        return Task.CompletedTask;
    }
}
