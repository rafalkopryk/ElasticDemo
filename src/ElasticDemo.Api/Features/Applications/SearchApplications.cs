using System.Linq.Expressions;
using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace ElasticDemo.Api.Features.Applications;

[JsonConverter(typeof(JsonStringEnumConverter))]
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
            .Indices(ApplicationIndex.Alias)
            .Size(request.Size)
            .Query(q => new ApplicationQueryBuilder()
                .Term(request.Product, f => f.Product)
                .Term(request.Transaction, f => f.Transaction)
                .Term(request.Channel, f => f.Channel)
                .Term(request.Status, f => f.Status)
                .DateRange(f => f.CreatedAt, request.CreatedAtFrom, request.CreatedAtTo)
                .Client(request, c => c.FirstName, request.FirstName)
                .Client(request, c => c.LastName, request.LastName)
                .Client(request, c => c.NationalId, request.NationalId)
                .Client(request, c => c.ClientId, request.ClientId)
                .Client(request, c => c.Email, request.Email)
                .Build(q))
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
}

public class ApplicationQueryBuilder
{
    private readonly List<Action<QueryDescriptor<Application>>> _queries = [];
    private readonly List<(string field, string value)> _clientTerms = [];
    private List<ClientRole>? _clientRoles;

    public ApplicationQueryBuilder Term(string? value, Expression<Func<Application, object?>> field)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _queries.Add(must => must.Term(t => t.Field(field).Value(value)));
        return this;
    }

    public ApplicationQueryBuilder DateRange(
        Expression<Func<Application, DateTimeOffset>> field,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (from.HasValue || to.HasValue)
        {
            _queries.Add(must => must.Range(r => r
                .Date(dr =>
                {
                    dr.Field(field);
                    if (from.HasValue) dr.Gte(from.Value.ToString("o"));
                    if (to.HasValue) dr.Lte(to.Value.ToString("o"));
                })
            ));
        }
        return this;
    }

    public ApplicationQueryBuilder Client(
        SearchApplicationsRequest request,
        Expression<Func<Client, object>> field,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return this;

        _clientRoles ??= request.Roles is { Count: > 0 } ? request.Roles : [ClientRole.MainClient];

        var fieldName = ((MemberExpression)field.Body).Member.Name;
        var camelCase = char.ToLowerInvariant(fieldName[0]) + fieldName[1..];
        _clientTerms.Add((camelCase, value));

        return this;
    }

    public Query Build(QueryDescriptor<Application> q)
    {
        AddClientQuery();

        if (_queries.Count == 0)
            return q.MatchAll(new MatchAllQuery());

        return q.Bool(b => b.Must(_queries.ToArray()));
    }

    private void AddClientQuery()
    {
        if (_clientTerms.Count == 0)
            return;

        var roles = _clientRoles ?? [ClientRole.MainClient];

        var roleQueries = roles.Select(role => (Action<QueryDescriptor<Application>>)(role switch
            {
                ClientRole.CoApplicant => s => s.Nested(n => n.Path(_ => _.CoApplicants)
                    .Query(nq => nq.Bool(bb => bb.Must(BuildClientTerms("coApplicants"))))),
                ClientRole.MainClient => s => s.Bool(bb => bb.Must( BuildClientTerms("mainClient"))),
                ClientRole.Spouse => s => s.Bool(bb => bb.Must(BuildClientTerms("spouse"))),
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
            }))
            .ToList();

        if (roleQueries.Count == 1)
        {
            _queries.Add(roleQueries[0]);
        }
        else
        {
            _queries.Add(must => must.Bool(b => b
                .Should(roleQueries.ToArray())
                .MinimumShouldMatch(1)
            ));
        }
    }

    private Action<QueryDescriptor<Application>>[] BuildClientTerms(string prefix) =>
        _clientTerms.Select<(string field, string value), Action<QueryDescriptor<Application>>>(
            t => m => m.Term(term => term.Field($"{prefix}.{t.field}").Value(t.value))
        ).ToArray();
}
