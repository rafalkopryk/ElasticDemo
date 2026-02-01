using Elastic.Clients.Elasticsearch;

namespace ElasticDemo.Api.Features.Products;

public record InitializeIndexResponse(bool Success, string Message);

public class InitializeIndexHandler(ElasticsearchClient client)
{
    public const string IndexName = "products";

    public async Task<IResult> Handle()
    {
        var existsResponse = await client.Indices.ExistsAsync(IndexName);

        if (existsResponse.Exists)
        {
            return Results.Ok(new InitializeIndexResponse(true, "Index already exists"));
        }

        var createResponse = await client.Indices.CreateAsync(IndexName, c => c
            .Mappings(m => m
                .Properties<Product>(p => p
                    .Keyword(k => k.Id)
                    .Text(t => t.Name, t => t.Analyzer("standard"))
                    .Text(t => t.Description, t => t.Analyzer("standard"))
                    .Keyword(k => k.Category)
                    .DoubleNumber(f => f.Price)
                    .Keyword(k => k.Tags)
                    .Boolean(b => b.InStock)
                    .Date(d => d.CreatedAt)
                    .Object(o => o.Variants, o => o
                        .Properties(vp => vp
                            .Keyword("sku")
                            .Keyword("size")
                            .Text("color")
                            .DoubleNumber("priceAdjustment")
                            .IntegerNumber("stock")
                        )
                    )
                    .DenseVector(dv => dv.Embedding, dv => dv
                        .Dims(EmbeddingService.Dimensions)
                        .Similarity(Elastic.Clients.Elasticsearch.Mapping.DenseVectorSimilarity.Cosine)
                    )
                )
            )
        );

        if (!createResponse.IsValidResponse)
        {
            return Results.BadRequest(new InitializeIndexResponse(false, $"Failed to create index: {createResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}"));
        }

        return Results.Ok(new InitializeIndexResponse(true, "Index created successfully"));
    }
}
