namespace eTicketing.Catalog.Business.Products;

/// <summary>Mirrors eTicketing.Identity.Business.Auth.AuthService's and
/// eTicketing.Ticketing.Business.Purchases' own inline IEventPublisher — duplicated per-service by
/// design (see each service's RabbitMqEventPublisher doc comment), not shared/promoted to
/// Contracts.</summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default);
}
