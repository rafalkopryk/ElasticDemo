using Elastic.Clients.Elasticsearch;

namespace ElasticDemo.Api.Features.Products;

public record UpdateProductRequest(
    string? Description = null,
    decimal? Price = null,
    bool? InStock = null
);

public class UpdateProductHandler(ElasticsearchClient client)
{
    public async Task<IResult> Handle(string id, UpdateProductRequest request)
    {
        var getResponse = await client.GetAsync<Product>(id, g => g.Index(InitializeIndexHandler.IndexName));

        if (!getResponse.IsValidResponse || !getResponse.Found)
        {
            return Results.NotFound($"Product with ID '{id}' not found.");
        }

        var existing = getResponse.Source!;
        var product = existing with
        {
            Description = request.Description ?? existing.Description,
            Price = request.Price ?? existing.Price,
            InStock = request.InStock ?? existing.InStock
        };

        var indexResponse = await client.IndexAsync(product, i => i
            .Index(InitializeIndexHandler.IndexName)
            .Id(product.Id)
        );

        if (!indexResponse.IsValidResponse)
        {
            return Results.BadRequest($"Failed to update product: {indexResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
        }

        return Results.Ok(product);
    }
}
