namespace eTicketing.PdfGeneration.Messaging;

/// <summary>Mirrors every other service's own inline IEventPublisher — duplicated per-service by
/// design, not shared/promoted to Contracts.</summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default);
}
