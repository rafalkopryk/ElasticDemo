using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace ElasticDemo.Api.Features.Products;

public record SearchProductsRequest(
    string? Query = null,
    string? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    DateTimeOffset? CreatedAtFrom = null,
    DateTimeOffset? CreatedAtTo = null,
    int From = 0,
    int Size = 10,
    string Sort = "desc"
);

public record SearchProductsResponse(List<Product> Products, long Total);

public class SearchProductsHandler(ElasticsearchClient client, TimeProvider timeProvider)
{
    public async Task<IResult> Handle(SearchProductsRequest request)
    {
        if (request.Sort != "asc" && request.Sort != "desc")
        {
            return Results.BadRequest("Sort parameter must be either 'asc' or 'desc'");
        }

        var sortOrder = request.Sort == "asc" ? SortOrder.Asc : SortOrder.Desc;
        var targetIndices = ProductIndex.IndicesForSearch(request.CreatedAtFrom, request.CreatedAtTo, timeProvider);

        var searchResponse = await client.SearchAsync<Product>(s => s
            .Indices(targetIndices)
            .From(request.From)
            .Size(request.Size)
            .Query(q => BuildQuery(q, request.Query, request.Category, request.MinPrice, request.MaxPrice, request.CreatedAtFrom, request.CreatedAtTo))
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

    private static Query BuildQuery(QueryDescriptor<Product> q, string? query, string? category, decimal? minPrice, decimal? maxPrice, DateTimeOffset? createdAtFrom, DateTimeOffset? createdAtTo)
    {
        var queries = new List<Action<QueryDescriptor<Product>>>();

        if (!string.IsNullOrWhiteSpace(query))
        {
            // Split query into terms and require each term to match in at least one field
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var term in terms)
            {
                queries.Add(must => must.MultiMatch(mm => mm
                    .Fields(new[] { "name", "description", "variants.color" })
                    .Query(term)
                ));
            }
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

        if (createdAtFrom.HasValue || createdAtTo.HasValue)
        {
            queries.Add(must => must.Range(r => r
                .Date(dr =>
                {
                    dr.Field(f => f.CreatedAt);
                    if (createdAtFrom.HasValue) dr.Gte(createdAtFrom.Value.ToString("o"));
                    if (createdAtTo.HasValue) dr.Lte(createdAtTo.Value.ToString("o"));
                })
            ));
        }

        if (queries.Count == 0)
        {
            return q.MatchAll(new MatchAllQuery());
        }

        return q.Bool(b => b.Must(queries.ToArray()));
    }
}
