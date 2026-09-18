using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Shared.Messaging;
using eTicketing.Ticketing.Business.Integration;
using eTicketing.Ticketing.Business.ReadModels;
using eTicketing.Ticketing.Business.Subscriptions;

namespace eTicketing.Ticketing.Api.Infrastructure.Messaging;

/// <summary>
/// Sends one inbound message to its handler, through the inbox. Split out of
/// <see cref="TicketingRabbitMqConsumerService"/> so the routing — in particular what runs inside the
/// inbox transaction and what deliberately does not — can be tested without a broker.
/// </summary>
public static class TicketingMessageRouter
{
    /// <summary>
    /// Returns false when the message was a repeat and its handler was skipped.
    ///
    /// <para><b>product.deleted is split in two, on purpose.</b> Dropping the product's snapshot is
    /// what stops its sectors from selling, so it commits on its own, ahead of the inbox. The buyer
    /// notifications run inside it. Were both in the one transaction, a notification fan-out that
    /// failed would roll the removal back with it, and a deleted product would stay on sale until
    /// someone replayed the message from the dead-letter queue. Outside, the removal is simply
    /// repeated on a redelivery, which is harmless: removing a snapshot that is already gone does
    /// nothing. It is also why that event never reaches <see cref="HandleAsync"/>: its two halves are
    /// dispatched from here, off one parse of the payload.</para>
    /// </summary>
    public static async Task<bool> RouteAsync(
        IServiceProvider services, string routingKey, string? messageId, ReadOnlyMemory<byte> body,
        CancellationToken ct)
    {
        var inbox = services.GetRequiredService<IInbox>();

        if (routingKey == EventNames.ProductDeleted)
        {
            var deleted = Deserialize<ProductDeleted>(body, routingKey);
            await services.GetRequiredService<IProductSnapshotProjector>().RemoveAsync(deleted.ProductId, ct);

            // Handled here rather than in HandleAsync so the payload is parsed once, not once per half.
            return await inbox.ProcessOnceAsync(
                messageId,
                TicketingRabbitMqConsumerService.QueueName,
                handlerCt => services.GetRequiredService<IProductDeletionNotifier>().NotifyAsync(deleted, handlerCt),
                ct);
        }

        return await inbox.ProcessOnceAsync(
            messageId,
            TicketingRabbitMqConsumerService.QueueName,
            handlerCt => HandleAsync(services, routingKey, body, handlerCt),
            ct);
    }

    private static async Task HandleAsync(
        IServiceProvider services, string routingKey, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        switch (routingKey)
        {
            case EventNames.TicketPdfReady:
                var pdfReady = Deserialize<TicketPdfReady>(body, routingKey);
                await services.GetRequiredService<ITicketPdfCompletionService>().ApplyAsync(pdfReady, ct);
                break;

            case EventNames.ProductUpdated:
                var productUpdated = Deserialize<ProductUpdated>(body, routingKey);
                await services.GetRequiredService<IProductChangeNotifier>().NotifyBuyersAsync(productUpdated, ct);
                break;

            case EventNames.ProductSnapshotChanged:
                var snapshot = Deserialize<ProductSnapshotChanged>(body, routingKey);
                await services.GetRequiredService<IProductSnapshotProjector>().ApplyAsync(snapshot, ct);
                break;

            case EventNames.OrganizationSnapshotChanged:
                var organizationSnapshot = Deserialize<OrganizationSnapshotChanged>(body, routingKey);
                await services.GetRequiredService<IOrganizationSnapshotProjector>().ApplyAsync(organizationSnapshot, ct);
                break;

            case EventNames.OrganizationDeleted:
                var organizationDeleted = Deserialize<OrganizationDeleted>(body, routingKey);
                await services.GetRequiredService<IOrganizationSnapshotProjector>()
                    .RemoveAsync(organizationDeleted.OrganizationId, ct);
                break;

            case EventNames.SubscriptionRenewed:
                var renewed = Deserialize<SubscriptionRenewed>(body, routingKey);
                await services.GetRequiredService<ISubscriptionRenewalService>().RenewAsync(renewed, ct);
                break;

            case EventNames.SubscriptionPaymentFailed:
                var paymentFailed = Deserialize<SubscriptionPaymentFailed>(body, routingKey);
                await services.GetRequiredService<ISubscriptionRenewalService>().MarkPastDueAsync(paymentFailed, ct);
                break;

            case EventNames.SubscriptionCancelled:
                var cancelled = Deserialize<SubscriptionCancelled>(body, routingKey);
                await services.GetRequiredService<ISubscriptionRenewalService>().CancelAsync(cancelled, ct);
                break;

            default:
                // Only reachable if a binding is added without a matching case — that's a deployment
                // mistake worth seeing loudly, and the consumer's catch parks the message.
                throw new InvalidOperationException($"Nepoznat routing key: {routingKey}");
        }
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> body, string routingKey)
        => JsonSerializer.Deserialize<T>(body.Span)
           ?? throw new InvalidOperationException($"Prazan payload za event '{routingKey}'.");
}
