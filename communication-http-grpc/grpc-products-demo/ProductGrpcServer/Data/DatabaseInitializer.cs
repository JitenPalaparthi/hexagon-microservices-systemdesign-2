using Microsoft.EntityFrameworkCore;
using ProductGrpcServer.Entities;

namespace ProductGrpcServer.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        ProductDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const int maximumAttempts = 15;

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            try
            {
                logger.LogInformation(
                    "Initializing PostgreSQL. Attempt {Attempt}/{MaximumAttempts}",
                    attempt,
                    maximumAttempts);

                await dbContext.Database.EnsureCreatedAsync(cancellationToken);

                if (await dbContext.Products.AnyAsync(cancellationToken))
                {
                    logger.LogInformation("Products already exist; seed operation skipped.");
                    return;
                }

                var now = DateTimeOffset.UtcNow;
                ProductEntity[] products =
                [
                    new()
                    {
                        Name = "MacBook Pro",
                        Description = "Professional Apple Silicon laptop",
                        Category = "Computers",
                        Price = 199999.00m,
                        AvailableQuantity = 10,
                        CreatedAtUtc = now
                    },
                    new()
                    {
                        Name = "Mechanical Keyboard",
                        Description = "Mechanical keyboard with tactile switches",
                        Category = "Accessories",
                        Price = 7499.00m,
                        AvailableQuantity = 25,
                        CreatedAtUtc = now
                    },
                    new()
                    {
                        Name = "Wireless Mouse",
                        Description = "Ergonomic wireless mouse",
                        Category = "Accessories",
                        Price = 2499.00m,
                        AvailableQuantity = 40,
                        CreatedAtUtc = now
                    },
                    new()
                    {
                        Name = "27-inch 4K Monitor",
                        Description = "IPS monitor suitable for software development",
                        Category = "Displays",
                        Price = 34999.00m,
                        AvailableQuantity = 15,
                        CreatedAtUtc = now
                    },
                    new()
                    {
                        Name = "USB-C Dock",
                        Description = "Multi-port USB-C docking station",
                        Category = "Accessories",
                        Price = 8999.00m,
                        AvailableQuantity = 20,
                        CreatedAtUtc = now
                    }
                ];

                await dbContext.Products.AddRangeAsync(products, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Seeded {ProductCount} products.", products.Length);
                return;
            }
            catch (Exception exception) when (attempt < maximumAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Database initialization failed on attempt {Attempt}. Retrying in 2 seconds.",
                    attempt);

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Could not initialize PostgreSQL after {maximumAttempts} attempts.");
    }
}
