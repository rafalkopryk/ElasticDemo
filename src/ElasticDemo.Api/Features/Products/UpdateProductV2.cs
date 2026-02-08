using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;

namespace ElasticDemo.Api.Features.Products;

[JsonSerializable(typeof(UpdateProductV2Request))]
public record UpdateProductV2Request(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Description = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    decimal? Price = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? InStock = null
);

public class UpdateProductV2Handler(ElasticsearchClient client)
{
    public async Task<IResult> Handle(string id, UpdateProductV2Request request)
    {
        var response = await client.UpdateAsync<Product, UpdateProductV2Request>(ProductIndex.Active, id, u => u
            .Doc(request)
        );

        if (!response.IsValidResponse)
        {
            var reason = response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error";
            if (response.ElasticsearchServerError?.Status == 404)
            {
                return Results.NotFound($"Product with ID '{id}' not found.");
            }
            return Results.BadRequest($"Failed to update product: {reason}");
        }

        return Results.Ok();
    }
}
