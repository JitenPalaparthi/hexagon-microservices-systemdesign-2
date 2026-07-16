using System.Net.Http.Json;
using Azure.Messaging.ServiceBus;
using Contracts;

public sealed class Worker : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Worker> _logger;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusProcessor _processor;

    public Worker(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<Worker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var connectionString = configuration.GetConnectionString("ServiceBus")
            ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
        var topicName = configuration["ServiceBus:OrdersTopic"] ?? "orders-topic";
        var subscriptionName = configuration["ServiceBus:Subscription"] ?? "inventory-subscription";

        _client = new ServiceBusClient(connectionString);
        _processor = _client.CreateProcessor(
            topicName,
            subscriptionName,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 8,
                PrefetchCount = 20,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var integrationEvent = args.Message.Body.ToObjectFromJson<OrderCreatedEvent>()
                ?? throw new InvalidOperationException("Invalid OrderCreatedEvent body.");

            _logger.LogInformation(
                "Inventory received event {EventId} for order {OrderId}; delivery count {DeliveryCount}",
                integrationEvent.EventId,
                integrationEvent.OrderId,
                args.Message.DeliveryCount);

            var request = new WarehouseAllocationRequest(
                integrationEvent.OrderId,
                integrationEvent.Items
                    .Select(item => new WarehouseItem(item.ProductId, item.Quantity))
                    .ToArray());

            var httpClient = _httpClientFactory.CreateClient("warehouse");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/warehouse/allocations")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("Idempotency-Key", $"inventory-{integrationEvent.OrderId}");

            using var response = await httpClient.SendAsync(httpRequest, args.CancellationToken);
            response.EnsureSuccessStatusCode();

            var allocation = await response.Content
                .ReadFromJsonAsync<WarehouseAllocationResponse>(args.CancellationToken);

            _logger.LogInformation(
                "Warehouse allocation {AllocationId}; replay={IsReplay}",
                allocation?.AllocationId,
                allocation?.IsReplay);

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (System.Text.Json.JsonException exception)
        {
            _logger.LogError(exception, "Invalid event JSON; dead-lettering message {MessageId}", args.Message.MessageId);
            await args.DeadLetterMessageAsync(
                args.Message,
                "InvalidJson",
                exception.Message,
                args.CancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Inventory processing failed; abandoning message {MessageId} for retry",
                args.Message.MessageId);
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Service Bus error. Entity={EntityPath}, Source={ErrorSource}, Namespace={Namespace}",
            args.EntityPath,
            args.ErrorSource,
            args.FullyQualifiedNamespace);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processor.StopProcessingAsync(cancellationToken);
        await _processor.DisposeAsync();
        await _client.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
