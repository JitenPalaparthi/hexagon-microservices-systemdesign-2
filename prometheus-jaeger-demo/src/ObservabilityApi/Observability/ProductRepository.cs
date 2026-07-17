using System.Collections.Concurrent;
using ObservabilityApi.Models;

namespace ObservabilityApi.Observability;

public sealed class ProductRepository
{
    private readonly ConcurrentDictionary<int, Product> _products = new();
    private int _nextId = 3;

    public ProductRepository()
    {
        _products[1] = new Product(1, "Mechanical Keyboard", 89.99m, 25);
        _products[2] = new Product(2, "USB-C Dock", 129.50m, 12);
        _products[3] = new Product(3, "4K Monitor", 399.00m, 8);
    }

    public IReadOnlyCollection<Product> GetAll() =>
        _products.Values.OrderBy(product => product.Id).ToArray();

    public Product? Get(int id) => _products.GetValueOrDefault(id);

    public Product Add(CreateProductRequest request)
    {
        var id = Interlocked.Increment(ref _nextId);
        var product = new Product(id, request.Name.Trim(), request.Price, request.Stock);
        _products[id] = product;
        return product;
    }
}
