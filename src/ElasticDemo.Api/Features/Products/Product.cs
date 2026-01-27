namespace ElasticDemo.Api.Features.Products;

public record ProductVariant
{
    public required string Sku { get; init; }
    public string? Size { get; init; }
    public string? Color { get; init; }
    public decimal PriceAdjustment { get; init; }
    public int Stock { get; init; }
}

public record Product
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public decimal Price { get; init; }
    public List<string> Tags { get; init; } = [];
    public bool InStock { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<ProductVariant> Variants { get; init; } = [];
}
