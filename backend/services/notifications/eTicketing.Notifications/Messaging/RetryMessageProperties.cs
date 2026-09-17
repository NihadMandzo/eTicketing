using RabbitMQ.Client;

namespace eTicketing.Notifications.Messaging;

/// <summary>
/// The AMQP properties a retry or dead-letter copy is republished with.
///
/// <para>Two things have to survive the republish, and neither does by default, because the copy is
/// a brand-new message rather than the original moved:</para>
/// <list type="bullet">
/// <item><b>The routing key</b>, in <c>Type</c> — see <see cref="RoutingKeyResolver"/>.</item>
/// <item><b>The message id.</b> <see cref="DeduplicatingDeliveryHandler"/> recognises a repeat by
/// it; a retry copy without it would be a stranger, so an email that failed once and then got
/// redelivered from the outbox would go out twice.</item>
/// </list>
/// </summary>
public static class RetryMessageProperties
{
    public const string RetryCountHeader = "x-retry-count";

    public static BasicProperties Create(string routingKey, string? messageId, int? retryCount)
    {
        var properties = new BasicProperties { Persistent = true, Type = routingKey, MessageId = messageId };

        if (retryCount is not null)
            properties.Headers = new Dictionary<string, object?> { [RetryCountHeader] = retryCount.Value };

        return properties;
    }
}
