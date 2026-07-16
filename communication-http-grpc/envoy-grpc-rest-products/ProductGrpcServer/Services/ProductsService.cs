using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ProductGrpcServer.Grpc;

namespace ProductGrpcServer.Services;

public sealed class ProductsService(ProductRepository repository) : ProductService.ProductServiceBase
{
    public override Task<ListProductsReply> ListProducts(Empty request, ServerCallContext context)
    {
        var reply = new ListProductsReply();
        reply.Products.AddRange(repository.List());
        return Task.FromResult(reply);
    }

    public override Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        var product = repository.Get(request.Id)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"Product {request.Id} was not found."));
        return Task.FromResult(product);
    }

    public override Task<ProductReply> CreateProduct(CreateProductRequest request, ServerCallContext context)
    {
        Validate(request.Name, request.Price);
        return Task.FromResult(repository.Create(request.Name, request.Description, request.Price));
    }

    public override Task<ProductReply> UpdateProduct(UpdateProductRequest request, ServerCallContext context)
    {
        Validate(request.Name, request.Price);
        var product = repository.Update(request.Id, request.Name, request.Description, request.Price)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"Product {request.Id} was not found."));
        return Task.FromResult(product);
    }

    public override Task<DeleteProductReply> DeleteProduct(DeleteProductRequest request, ServerCallContext context)
    {
        return Task.FromResult(new DeleteProductReply { Id = request.Id, Deleted = repository.Delete(request.Id) });
    }

    private static void Validate(string name, double price)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Name is required."));
        if (price < 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Price cannot be negative."));
    }
}
