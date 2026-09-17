namespace eTicketing.Shared.Messaging;

/// <summary>Bound from the <c>RabbitMq</c> configuration section.</summary>
public class RabbitMqPublisherOptions
{
    /// <summary>Either a bare host name or a full <c>amqp://</c> URI — both shapes appear in this
    /// repo's configuration, so the publisher accepts either rather than forcing one.</summary>
    public string Host { get; set; } = "localhost";
}
