using ProductGrpcServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001, listen =>
    {
        listen.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddSingleton<ProductRepository>();

var app = builder.Build();

app.MapGrpcService<ProductsService>();
app.MapGrpcReflectionService();
app.MapGet("/", () => "Native gRPC server. Send REST/JSON requests through Envoy on port 8080.");

app.Run();
