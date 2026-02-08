using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace ElasticDemo.Api.Features.Applications;

public enum ClientRole { MainClient, Spouse, CoApplicant }

public record SearchApplicationsRequest(
    string? Product = null,
    string? Transaction = null,
    string? Channel = null,
    string? Status = null,
    DateTimeOffset? CreatedAtFrom = null,
    DateTimeOffset? CreatedAtTo = null,
    string? FirstName = null,
    string? LastName = null,
    string? NationalId = null,
    string? ClientId = null,
    string? Email = null,
    List<ClientRole>? Roles = null,
    int Size = 100,
    string Sort = "desc"
);

public record SearchApplicationsResponse(List<Application> Applications, long Total);

public class SearchApplicationsHandler(ElasticsearchClient client)
{
    public async Task<IResult> Handle(SearchApplicationsRequest request)
    {
        if (request.Sort != "asc" && request.Sort != "desc")
        {
            return Results.BadRequest("Sort parameter must be either 'asc' or 'desc'");
        }


        var sortOrder = request.Sort == "asc" ? SortOrder.Asc : SortOrder.Desc;

        var searchResponse = await client.SearchAsync<Application>(s => s
            .Indices(ApplicationIndex.Active)
            .Size(request.Size)
            .Query(q => BuildQuery(q, request))
            .Sort(sort => sort
                .Field(f => f.CreatedAt, sortOrder)
            )
        );

        if (!searchResponse.IsValidResponse)
        {
            return Results.BadRequest($"Search failed: {searchResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
        }

        var applications = searchResponse.Documents.ToList();
        return Results.Ok(new SearchApplicationsResponse(applications, searchResponse.Total));
    }

    private static Query BuildQuery(QueryDescriptor<Application> q, SearchApplicationsRequest request)
    {
        var queries = new List<Action<QueryDescriptor<Application>>>();

        if (!string.IsNullOrWhiteSpace(request.Product))
        {
            queries.Add(must => must.Term(t => t.Field(f => f.Product).Value(request.Product)));
        }

        if (!string.IsNullOrWhiteSpace(request.Transaction))
        {
            queries.Add(must => must.Term(t => t.Field(f => f.Transaction).Value(request.Transaction)));
        }

        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            queries.Add(must => must.Term(t => t.Field(f => f.Channel).Value(request.Channel)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            queries.Add(must => must.Term(t => t.Field(f => f.Status).Value(request.Status)));
        }

        if (request.CreatedAtFrom.HasValue || request.CreatedAtTo.HasValue)
        {
            queries.Add(must => must.Range(r => r
                .Date(dr =>
                {
                    dr.Field(f => f.CreatedAt);
                    if (request.CreatedAtFrom.HasValue) dr.Gte(request.CreatedAtFrom.Value.ToString("o"));
                    if (request.CreatedAtTo.HasValue) dr.Lte(request.CreatedAtTo.Value.ToString("o"));
                })
            ));
        }

        AddClientQuery(queries, request);

        if (queries.Count == 0)
        {
            return q.MatchAll(new MatchAllQuery());
        }

        return q.Bool(b => b.Must(queries.ToArray()));
    }

    private static void AddClientQuery(List<Action<QueryDescriptor<Application>>> queries, SearchApplicationsRequest request)
    {
        var clientTerms = new List<(string field, string value)>();
        if (!string.IsNullOrWhiteSpace(request.FirstName)) clientTerms.Add(("firstName", request.FirstName.ToLowerInvariant()));
        if (!string.IsNullOrWhiteSpace(request.LastName)) clientTerms.Add(("lastName", request.LastName.ToLowerInvariant()));
        if (!string.IsNullOrWhiteSpace(request.NationalId)) clientTerms.Add(("nationalId", request.NationalId));
        if (!string.IsNullOrWhiteSpace(request.ClientId)) clientTerms.Add(("clientId", request.ClientId));
        if (!string.IsNullOrWhiteSpace(request.Email)) clientTerms.Add(("email", request.Email.ToLowerInvariant()));

        if (clientTerms.Count == 0)
            return;

        var roles = request.Roles is { Count: > 0 } ? request.Roles : [ClientRole.MainClient];

        var roleQueries = roles.Select(role => (Action<QueryDescriptor<Application>>)(role switch
            {
                ClientRole.CoApplicant => s => s.Nested(n => n.Path("coApplicants")
                    .Query(nq => nq.Bool(bb => bb.Must(clientTerms.Select<(string field, string value), Action<QueryDescriptor<Application>>>(t => m => m.Term(term => term.Field($"coApplicants.{t.field}").Value(t.value))).ToArray())))),
                ClientRole.MainClient => s => s.Bool(bb => bb.Must(clientTerms.Select<(string field, string value), Action<QueryDescriptor<Application>>>(t => m => m.Term(term => term.Field($"mainClient.{t.field}").Value(t.value))).ToArray())),
                ClientRole.Spouse => s => s.Bool(bb => bb.Must(clientTerms.Select<(string field, string value), Action<QueryDescriptor<Application>>>(t => m => m.Term(term => term.Field($"spouse.{t.field}").Value(t.value))).ToArray())),
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
            }))
            .ToList();

        if (roleQueries.Count == 1)
        {
            queries.Add(roleQueries[0]);
        }
        else
        {
            queries.Add(must => must.Bool(b => b
                .Should(roleQueries.ToArray())
                .MinimumShouldMatch(1)
            ));
        }
    }
}
