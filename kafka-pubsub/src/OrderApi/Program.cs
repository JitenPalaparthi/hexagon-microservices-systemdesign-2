using Confluent.Kafka;
using Npgsql;
using OrderApi.Data;
using OrderApi.Messaging;
using OrderApi.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("OrdersDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:OrdersDatabase is required.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddScoped<OrderRepository>();

var kafkaOptions = builder.Configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>()
    ?? throw new InvalidOperationException("Kafka configuration is required.");

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = kafkaOptions.BootstrapServers,
        ClientId = "order-api-outbox-publisher",
        Acks = Acks.All,
        EnableIdempotence = true,
        MessageSendMaxRetries = int.MaxValue,
        RetryBackoffMs = 500
    }).Build());

builder.Services.AddHostedService<OutboxPublisher>();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
