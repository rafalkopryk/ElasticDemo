using Elastic.Clients.Elasticsearch;

namespace ElasticDemo.Api.Features.Products;

public record DeleteProductResponse(bool Success, string Message);

public class DeleteProductHandler(ElasticsearchClient client)
{
    public async Task<IResult> Handle(string id)
    {
        var response = await client.DeleteAsync(ProductIndex.Active, id);

        if (!response.IsValidResponse)
        {
            return Results.NotFound(new DeleteProductResponse(false, $"Product with id '{id}' not found"));
        }

        return Results.Ok(new DeleteProductResponse(true, "Product deleted successfully"));
    }
}
