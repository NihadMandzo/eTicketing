namespace eTicketing.Shared.Messaging;

/// <summary>
/// Publishes an event that is already serialized, under an id the caller chose.
///
/// Separate from <see cref="eTicketing.Contracts.Events.IEventPublisher"/> because it is the
/// opposite end of the same pipe: that one takes a domain object from a service that does not care
/// how it travels, this one takes bytes and a message id from something replaying a stored row.
/// Keeping them apart is what stops a business service from reaching for the raw form, and what
/// lets <see cref="OutboxDispatcher{TContext}"/> depend on an abstraction it can be tested against
/// rather than on the RabbitMQ client.
/// </summary>
public interface IRawEventPublisher
{
    /// <param name="messageId">Carried onto the AMQP message, so a row published twice arrives with
    /// the same id both times and a future inbox table can recognise the repeat.</param>
    Task PublishRawAsync(string routingKey, string payloadJson, Guid messageId, CancellationToken ct = default);
}
