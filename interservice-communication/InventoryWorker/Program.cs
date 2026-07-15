var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient("warehouse", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:WarehouseService"] ?? "http://localhost:5004");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHostedService<Worker>();
await builder.Build().RunAsync();
