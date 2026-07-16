using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ProductGrpcServer.Data;
using ProductGrpcServer.Entities;
using ProductGrpcServer.Grpc;

namespace ProductGrpcServer.Services;

public sealed class ProductGrpcService(
    ProductDbContext dbContext,
    ILogger<ProductGrpcService> logger)
    : ProductService.ProductServiceBase
{
    public override async Task<ProductReply> GetProduct(
        GetProductRequest request,
        ServerCallContext context)
    {
        if (request.Id <= 0)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Product id must be greater than zero."));
        }

        var product = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.Id, context.CancellationToken);

        if (product is null)
        {
            throw new RpcException(new Status(
                StatusCode.NotFound,
                $"Product {request.Id} was not found."));
        }

        return ToReply(product);
    }

    public override async Task<ProductReply> CreateProduct(
        CreateProductRequest request,
        ServerCallContext context)
    {
        ValidateCreateRequest(request);

        var product = ToEntity(request);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Created product {ProductId}: {ProductName}", product.Id, product.Name);
        return ToReply(product);
    }

    public override async Task<ProductListReply> ListProducts(
        Empty request,
        ServerCallContext context)
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(context.CancellationToken);

        var reply = new ProductListReply();
        reply.Products.AddRange(products.Select(ToReply));
        return reply;
    }

    public override async Task StreamProducts(
        StreamProductsRequest request,
        IServerStreamWriter<ProductReply> responseStream,
        ServerCallContext context)
    {
        var delay = Math.Clamp(request.DelayMilliseconds, 0, 10_000);

        var query = dbContext.Products.AsNoTracking().OrderBy(x => x.Id).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.Trim();
            query = query.Where(x => x.Category == category);
        }

        await foreach (var product in query.AsAsyncEnumerable()
                           .WithCancellation(context.CancellationToken))
        {
            await responseStream.WriteAsync(ToReply(product));
            logger.LogInformation("Streamed product {ProductId}: {ProductName}", product.Id, product.Name);

            if (delay > 0)
            {
                await Task.Delay(delay, context.CancellationToken);
            }
        }
    }

    public override async Task<CreateProductsSummary> CreateProducts(
        IAsyncStreamReader<CreateProductRequest> requestStream,
        ServerCallContext context)
    {
        var summary = new CreateProductsSummary();

        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            summary.ReceivedCount++;

            try
            {
                ValidateCreateRequest(request);
                var product = ToEntity(request);
                dbContext.Products.Add(product);
                await dbContext.SaveChangesAsync(context.CancellationToken);

                summary.CreatedCount++;
                summary.CreatedProductIds.Add(product.Id);
                logger.LogInformation("Client stream created product {ProductId}: {ProductName}", product.Id, product.Name);
            }
            catch (RpcException exception)
            {
                summary.FailedCount++;
                summary.Errors.Add($"Message {summary.ReceivedCount}: {exception.Status.Detail}");
                dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateException exception)
            {
                summary.FailedCount++;
                summary.Errors.Add($"Message {summary.ReceivedCount}: database insertion failed.");
                logger.LogError(exception, "Client-stream product insertion failed.");
                dbContext.ChangeTracker.Clear();
            }
        }

        return summary;
    }

   public override async Task UpdateProductPrices(
    IAsyncStreamReader<PriceUpdateRequest> requestStream,
    IServerStreamWriter<PriceUpdateReply> responseStream,
    ServerCallContext context)
{
    await foreach (
        var request in requestStream.ReadAllAsync(
            context.CancellationToken))
    {
        var reply = new PriceUpdateReply
        {
            ProductId = request.ProductId,
            NewPrice = request.NewPrice,
            CorrelationId = request.CorrelationId
        };

        if (request.ProductId <= 0)
        {
            reply.Accepted = false;
            reply.Message = "Product id must be greater than zero.";

            await responseStream.WriteAsync(reply);
            continue;
        }

        if (request.NewPrice <= 0)
        {
            reply.Accepted = false;
            reply.Message = "New price must be greater than zero.";

            await responseStream.WriteAsync(reply);
            continue;
        }

        var existingProduct = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(
                product => product.Id == request.ProductId,
                context.CancellationToken);

        if (existingProduct is null)
        {
            reply.Accepted = false;
            reply.Message =
                $"Product {request.ProductId} was not found.";

            await responseStream.WriteAsync(reply);
            continue;
        }

        var previousPrice = existingProduct.Price;
        var requestedPrice = Convert.ToDecimal(request.NewPrice);

        var affectedRows = await dbContext.Products
            .Where(product => product.Id == request.ProductId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    product => product.Price,
                    requestedPrice),
                context.CancellationToken);

        if (affectedRows != 1)
        {
            reply.Accepted = false;
            reply.ProductName = existingProduct.Name;
            reply.PreviousPrice = Convert.ToDouble(previousPrice);
            reply.Message =
                $"Expected to update one product, but updated {affectedRows} rows.";

            await responseStream.WriteAsync(reply);
            continue;
        }

        var persistedProduct = await dbContext.Products
            .AsNoTracking()
            .SingleAsync(
                product => product.Id == request.ProductId,
                context.CancellationToken);

        reply.Accepted = true;
        reply.ProductName = persistedProduct.Name;
        reply.PreviousPrice = Convert.ToDouble(previousPrice);
        reply.NewPrice = Convert.ToDouble(persistedProduct.Price);

        reply.Message = string.IsNullOrWhiteSpace(request.Reason)
            ? "Product price persisted successfully."
            : $"Product price persisted successfully: {request.Reason}";

        logger.LogInformation(
            """
            Product price updated.
            ProductId: {ProductId}
            PreviousPrice: {PreviousPrice}
            RequestedPrice: {RequestedPrice}
            PersistedPrice: {PersistedPrice}
            AffectedRows: {AffectedRows}
            CorrelationId: {CorrelationId}
            """,
            request.ProductId,
            previousPrice,
            requestedPrice,
            persistedProduct.Price,
            affectedRows,
            request.CorrelationId);

        await responseStream.WriteAsync(reply);
    }
}

    private static void ValidateCreateRequest(CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Name is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Category is required."));
        }

        if (request.Price <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Price must be greater than zero."));
        }

        if (request.AvailableQuantity < 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Available quantity cannot be negative."));
        }
    }

    private static ProductEntity ToEntity(CreateProductRequest request) => new()
    {
        Name = request.Name.Trim(),
        Description = request.Description.Trim(),
        Category = request.Category.Trim(),
        Price = Convert.ToDecimal(request.Price),
        AvailableQuantity = request.AvailableQuantity,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private static ProductReply ToReply(ProductEntity product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Description = product.Description,
        Category = product.Category,
        Price = Convert.ToDouble(product.Price),
        AvailableQuantity = product.AvailableQuantity,
        CreatedAtUtc = Timestamp.FromDateTimeOffset(product.CreatedAtUtc)
    };
}
