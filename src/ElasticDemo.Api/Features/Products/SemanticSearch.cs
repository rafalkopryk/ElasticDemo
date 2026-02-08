using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace ElasticDemo.Api.Features.Products;

public record SemanticSearchRequest(
    string Query,
    string? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    DateTimeOffset? CreatedAtFrom = null,
    DateTimeOffset? CreatedAtTo = null,
    int K = 10,
    int NumCandidates = 100,
    float? Similarity = null
);

public record SemanticSearchResponse(List<Product> Products, long Total);

public class SemanticSearchHandler(ElasticsearchClient client, EmbeddingService embeddingService, TimeProvider timeProvider)
{
    public async Task<IResult> Handle(SemanticSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Results.BadRequest("Query is required for semantic search");
        }

        // Generate embedding for the search query
        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(request.Query);

        // Build post-filter for category, price, and date range
        var filters = BuildFilters(request.Category, request.MinPrice, request.MaxPrice, request.CreatedAtFrom, request.CreatedAtTo);

        var targetIndices = ProductIndex.IndicesForSearch(request.CreatedAtFrom, request.CreatedAtTo, timeProvider);

        var searchResponse = await client.SearchAsync<Product>(s =>
        {
            s.Indices(targetIndices)
                .Knn(knn =>
                {
                    knn.Field(f => f.Embedding)
                        .QueryVector(queryEmbedding)
                        .K(request.K)
                        .NumCandidates(request.NumCandidates);

                    if (request.Similarity.HasValue)
                    {
                        knn.Similarity(request.Similarity.Value);
                    }
                });

            if (filters.Count > 0)
            {
                s.PostFilter(pf => pf.Bool(b => b.Must(filters.ToArray())));
            }
        });

        if (!searchResponse.IsValidResponse)
        {
            return Results.BadRequest($"Semantic search failed: {searchResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
        }

        var products = searchResponse.Documents.ToList();
        return Results.Ok(new SemanticSearchResponse(products, searchResponse.Total));
    }

    private static List<Action<QueryDescriptor<Product>>> BuildFilters(
        string? category,
        decimal? minPrice,
        decimal? maxPrice,
        DateTimeOffset? createdAtFrom,
        DateTimeOffset? createdAtTo)
    {
        var filters = new List<Action<QueryDescriptor<Product>>>();

        if (!string.IsNullOrWhiteSpace(category))
        {
            filters.Add(must => must.Term(t => t
                .Field(f => f.Category)
                .Value(category)
            ));
        }

        if (minPrice.HasValue || maxPrice.HasValue)
        {
            filters.Add(must => must.Range(r => r
                .Number(nr => nr
                    .Field(f => f.Price)
                    .Gte(minPrice.HasValue ? (double)minPrice.Value : null)
                    .Lte(maxPrice.HasValue ? (double)maxPrice.Value : null)
                )
            ));
        }

        if (createdAtFrom.HasValue || createdAtTo.HasValue)
        {
            filters.Add(must => must.Range(r => r
                .Date(dr =>
                {
                    dr.Field(f => f.CreatedAt);
                    if (createdAtFrom.HasValue) dr.Gte(createdAtFrom.Value.ToString("o"));
                    if (createdAtTo.HasValue) dr.Lte(createdAtTo.Value.ToString("o"));
                })
            ));
        }

        return filters;
    }
}
