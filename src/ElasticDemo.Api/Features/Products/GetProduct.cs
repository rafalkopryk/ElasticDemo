using Elastic.Clients.Elasticsearch;

namespace ElasticDemo.Api.Features.Products;

public class GetProductHandler(ElasticsearchClient client)
{
    public async Task<IResult> Handle(string id)
    {
        var response = await client.GetAsync<Product>(id, g => g.Index(ProductIndex.Active));

        if (!response.IsValidResponse || !response.Found)
        {
            return Results.NotFound($"Product with id '{id}' not found");
        }

        return Results.Ok(response.Source);
    }
}
