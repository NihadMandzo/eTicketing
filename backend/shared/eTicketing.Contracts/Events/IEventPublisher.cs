namespace eTicketing.Contracts.Events;

/// <summary>
/// Publishes a domain event onto the platform's topic exchange.
///
/// One declaration, shared. This reverses an earlier, explicitly documented decision to duplicate
/// it per service — there were five byte-identical copies, two of them tucked inside unrelated
/// files (AuthService.cs and IStripeWebhookService.cs). Independent copies were defensible while
/// events only triggered side effects: a service could change its own contract without dragging
/// the others along. That stopped being true once events became the data-sync channel between
/// services, because the shape now has to agree by construction rather than by inspection.
///
/// <para>Contracts deliberately does <b>not</b> reference RabbitMQ.Client. This is the seam, not
/// the transport: the implementations live in eTicketing.Shared.Messaging, and which one a service
/// gets — the direct publisher, or the outbox writer that defers to it — is a composition decision
/// made in that service's Api/Infrastructure.</para>
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default);
}
