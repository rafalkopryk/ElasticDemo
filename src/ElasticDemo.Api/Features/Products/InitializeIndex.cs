using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;

namespace ElasticDemo.Api.Features.Products;

public record InitializeIndexResponse(bool Success, string Message);

public class InitializeIndexHandler(ElasticsearchClient client)
{
    public async Task<IResult> Handle()
    {
        var messages = new List<string>();

        // 1. Create active index if it doesn't exist
        var existsResponse = await client.Indices.ExistsAsync(ProductIndex.Active);
        if (!existsResponse.Exists)
        {
            var createResponse = await client.Indices.CreateAsync(ProductIndex.Active, c => c
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
                        .Nested(o => o.Variants, o => o
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
                return Results.BadRequest(new InitializeIndexResponse(false,
                    $"Failed to create active index: {createResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}"));
            }
            messages.Add($"Created active index '{ProductIndex.Active}'");
        }
        else
        {
            messages.Add($"Active index '{ProductIndex.Active}' already exists");
        }

        // 2. Create composable index template for archive indices
        var templateResponse = await client.Indices.PutIndexTemplateAsync(ProductIndex.ArchiveTemplateName, t => t
            .IndexPatterns((Indices)ProductIndex.ArchivePattern)
            .Template(tpl => tpl
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
                        .Nested(o => o.Variants, o => o
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
            )
        );

        if (!templateResponse.IsValidResponse)
        {
            return Results.BadRequest(new InitializeIndexResponse(false,
                $"Failed to create archive template: {templateResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}"));
        }
        messages.Add($"Archive template '{ProductIndex.ArchiveTemplateName}' configured");

        return Results.Ok(new InitializeIndexResponse(true, string.Join(". ", messages)));
    }
}
