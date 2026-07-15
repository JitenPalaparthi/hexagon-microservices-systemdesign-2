using System.Net;
using System.Net.Http.Json;

public sealed class ProductClient(HttpClient httpClient)
{
    public async Task<ProductResponse?> GetAsync(int productId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/products/{productId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken);
    }
}

public sealed record ProductResponse(int Id, string Name, decimal Price, int AvailableQuantity);
