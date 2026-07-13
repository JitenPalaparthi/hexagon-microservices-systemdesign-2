namespace OrderConsumer.Options;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";
    public string BootstrapServers { get; init; } = "localhost:29092";
    public string Topic { get; init; } = "orders.created.v1";
    public string ConsumerGroup { get; init; } = "order-processor-v1";
}
