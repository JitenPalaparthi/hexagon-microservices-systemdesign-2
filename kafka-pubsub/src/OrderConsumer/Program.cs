using Npgsql;
using OrderConsumer.Data;
using OrderConsumer.Messaging;
using OrderConsumer.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("ConsumerDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:ConsumerDatabase is required.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<ProcessedOrderRepository>();
builder.Services.AddHostedService<OrderCreatedConsumer>();

await builder.Build().RunAsync();
