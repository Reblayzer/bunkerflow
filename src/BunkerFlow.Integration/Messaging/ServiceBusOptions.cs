namespace BunkerFlow.Integration.Messaging;

/// <summary>
/// Connection settings for the Azure Service Bus entities that Terraform
/// provisions in infra/terraform.
/// </summary>
public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    /// <summary>
    /// Left empty when running without a broker, which makes the host fall back
    /// to the in-memory publisher instead of failing to start.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Topic every normalized event is published to.</summary>
    public string TopicName { get; set; } = "bunkerflow-events";

    /// <summary>Subscription the landing writer reads from.</summary>
    public string SubscriptionName { get; set; } = "landing";

    /// <summary>
    /// Queue for records the pipeline refused before they ever reached the
    /// topic. Service Bus has its own dead-letter queue per entity for messages
    /// that fail after delivery; this one catches the earlier stage.
    /// </summary>
    public string DeadLetterQueueName { get; set; } = "bunkerflow-deadletter";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}
