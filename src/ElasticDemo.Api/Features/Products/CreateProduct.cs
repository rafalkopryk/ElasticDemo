using Elastic.Clients.Elasticsearch;

namespace ElasticDemo.Api.Features.Products;

public record CreateProductVariantRequest(
    string Sku,
    string? Size,
    string? Color,
    decimal PriceAdjustment,
    int Stock
);

public record CreateProductRequest(
    string Name,
    string Description,
    string Category,
    decimal Price,
    List<string>? Tags = null,
    bool InStock = true,
    List<CreateProductVariantRequest>? Variants = null
);

public class CreateProductHandler(ElasticsearchClient client, TimeProvider timeProvider)
{
    public async Task<IResult> Handle(CreateProductRequest request)
    {
        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            Price = request.Price,
            Tags = request.Tags ?? [],
            InStock = request.InStock,
            CreatedAt = timeProvider.GetUtcNow(),
            Variants = request.Variants?.Select(v => new ProductVariant
            {
                Sku = v.Sku,
                Size = v.Size,
                Color = v.Color,
                PriceAdjustment = v.PriceAdjustment,
                Stock = v.Stock
            }).ToList() ?? []
        };

        var response = await client.IndexAsync(product, i => i
            .Index(InitializeIndexHandler.IndexName)
            .Id(product.Id)
        );

        if (!response.IsValidResponse)
        {
            return Results.BadRequest($"Failed to create product: {response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
        }

        return Results.Created($"/api/products/{product.Id}", product);
    }
}
