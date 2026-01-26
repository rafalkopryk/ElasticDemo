using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace ElasticDemo.Api.Features.Products;

public record SearchProductsRequest(
    string? Query = null,
    string? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int From = 0,
    int Size = 10,
    string Sort = "desc"
);

public record SearchProductsResponse(List<Product> Products, long Total);

public class SearchProductsHandler(ElasticsearchClient client)
{
    public async Task<IResult> Handle(SearchProductsRequest request)
    {
        if (request.Sort != "asc" && request.Sort != "desc")
        {
            return Results.BadRequest("Sort parameter must be either 'asc' or 'desc'");
        }

        var sortOrder = request.Sort == "asc" ? SortOrder.Asc : SortOrder.Desc;

        var searchResponse = await client.SearchAsync<Product>(s => s
            .Indices(InitializeIndexHandler.IndexName)
            .From(request.From)
            .Size(request.Size)
            .Query(q => BuildQuery(q, request.Query, request.Category, request.MinPrice, request.MaxPrice))
            .Sort(sort => sort
                .Field(f => f.CreatedAt, sortOrder)
            )
        );

        if (!searchResponse.IsValidResponse)
        {
            return Results.BadRequest($"Search failed: {searchResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
        }

        var products = searchResponse.Documents.ToList();
        return Results.Ok(new SearchProductsResponse(products, searchResponse.Total));
    }

    private static Query BuildQuery(QueryDescriptor<Product> q, string? query, string? category, decimal? minPrice, decimal? maxPrice)
    {
        var queries = new List<Action<QueryDescriptor<Product>>>();

        if (!string.IsNullOrWhiteSpace(query))
        {
            queries.Add(must => must.MultiMatch(mm => mm
                .Fields(new[] { "name^2", "description", "variants.sku^1.5", "variants.color^1.5", "variants.size" })
                .Query(query)
                .Fuzziness(new Fuzziness("AUTO"))
                .MinimumShouldMatch("100%")
            ));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            queries.Add(must => must.Term(t => t
                .Field(f => f.Category)
                .Value(category)
            ));
        }

        if (minPrice.HasValue || maxPrice.HasValue)
        {
            queries.Add(must => must.Range(r => r
                .Number(nr => nr
                    .Field(f => f.Price)
                    .Gte(minPrice.HasValue ? (double)minPrice.Value : null)
                    .Lte(maxPrice.HasValue ? (double)maxPrice.Value : null)
                )
            ));
        }

        if (queries.Count == 0)
        {
            return q.MatchAll(new MatchAllQuery());
        }

        return q.Bool(b => b.Must(queries.ToArray()));
    }
}
