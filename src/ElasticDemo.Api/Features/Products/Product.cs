namespace ElasticDemo.Api.Features.Products;

public record ProductVariant
{
    public required string Sku { get; init; }
    public string? Size { get; init; }
    public string? Color { get; init; }
    public decimal PriceAdjustment { get; init; }
    public int Stock { get; init; }
}

public class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public decimal Price { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool InStock { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ProductVariant> Variants { get; set; } = [];
}
