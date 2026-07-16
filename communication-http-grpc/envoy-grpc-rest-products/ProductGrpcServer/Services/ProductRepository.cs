using System.Collections.Concurrent;
using ProductGrpcServer.Grpc;

namespace ProductGrpcServer.Services;

public sealed class ProductRepository
{
    private readonly ConcurrentDictionary<int, ProductReply> _products = new();
    private int _nextId = 2;

    public ProductRepository()
    {
        _products[1] = new ProductReply
        {
            Id = 1,
            Name = "Mechanical Keyboard",
            Description = "Seed product",
            Price = 7999
        };
        _products[2] = new ProductReply
        {
            Id = 2,
            Name = "USB-C Dock",
            Description = "Seed product",
            Price = 5499
        };
    }

    public IEnumerable<ProductReply> List() => _products.Values.OrderBy(p => p.Id);
    public ProductReply? Get(int id) => _products.GetValueOrDefault(id);

    public ProductReply Create(string name, string description, double price)
    {
        var id = Interlocked.Increment(ref _nextId);
        var product = new ProductReply { Id = id, Name = name, Description = description, Price = price };
        _products[id] = product;
        return product;
    }

    public ProductReply? Update(int id, string name, string description, double price)
    {
        if (!_products.ContainsKey(id)) return null;
        var product = new ProductReply { Id = id, Name = name, Description = description, Price = price };
        _products[id] = product;
        return product;
    }

    public bool Delete(int id) => _products.TryRemove(id, out _);
}
