using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Testcontainers.Redpanda;

namespace BunkerFlow.Broker.Tests;

/// <summary>
/// A real broker in a container, shared by every test in the collection.
/// Starting one per test would be slower than the tests themselves.
/// </summary>
public sealed class RedpandaFixture : IAsyncLifetime
{
    // Pinned to the same image the compose stack runs, so a broker quirk shows
    // up in tests rather than only at runtime.
    private readonly RedpandaContainer _container =
        new RedpandaBuilder("redpandadata/redpanda:v24.3.6").Build();

    public string BootstrapServers => _container.GetBootstrapAddress();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public async Task CreateTopicAsync(string topic)
    {
        using var admin = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = BootstrapServers }).Build();

        await admin.CreateTopicsAsync([
            new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 },
        ]);
    }

    public void Produce(string topic, string key, string value)
    {
        using var producer = new ProducerBuilder<string, string>(
            new ProducerConfig { BootstrapServers = BootstrapServers }).Build();

        producer.Produce(topic, new Message<string, string> { Key = key, Value = value });
        producer.Flush(TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// The committed offset for a consumer group, or null when the group has
    /// never committed. This is what proves the worker did or did not advance.
    /// </summary>
    public long? CommittedOffset(string topic, string consumerGroup)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = BootstrapServers,
            GroupId = consumerGroup,
        }).Build();

        var committed = consumer.Committed(
            [new TopicPartition(topic, new Partition(0))],
            TimeSpan.FromSeconds(15));

        var offset = committed.SingleOrDefault()?.Offset;

        return offset is null || offset == Offset.Unset ? null : offset.Value.Value;
    }
}

[CollectionDefinition(nameof(RedpandaCollection))]
public sealed class RedpandaCollection : ICollectionFixture<RedpandaFixture>;
