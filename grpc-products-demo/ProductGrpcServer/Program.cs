using Microsoft.EntityFrameworkCore;
using ProductGrpcServer.Data;
using ProductGrpcServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddGrpcReflection();

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");

builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseInitializer");

    await DatabaseInitializer.InitializeAsync(dbContext, logger);
}

app.MapGrpcService<ProductGrpcService>();
app.MapGrpcReflectionService();

app.MapGet("/", () => Results.Ok(new
{
    service = "product-grpc-server",
    grpcEndpoint = "http://localhost:5001",
    message = "Use Postman gRPC or grpcurl to call the ProductService."
}));

app.MapGet("/health", async (ProductDbContext dbContext, CancellationToken cancellationToken) =>
{
    var databaseAvailable = await dbContext.Database.CanConnectAsync(cancellationToken);
    return databaseAvailable
        ? Results.Ok(new { status = "ok", database = "postgresql" })
        : Results.Problem("PostgreSQL is unavailable.", statusCode: 503);
});

app.Run();
