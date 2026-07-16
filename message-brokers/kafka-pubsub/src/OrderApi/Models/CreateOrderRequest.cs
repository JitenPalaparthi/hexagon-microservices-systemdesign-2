using System.ComponentModel.DataAnnotations;

namespace OrderApi.Models;

public sealed class CreateOrderRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string CustomerName { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 2)]
    public string Product { get; init; } = string.Empty;

    [Range(1, 10_000)]
    public int Quantity { get; init; }
}
