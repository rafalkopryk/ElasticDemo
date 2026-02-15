using System.Linq.Expressions;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using ElasticDemo.Api.Features.Applications;

namespace ElasticDemo.Api.Features.ApplicationsV2;

public record SearchApplicationsV2Request(
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

public record SearchApplicationsV2Response(List<ApplicationV2> Applications, long Total);

public class SearchApplicationsV2Handler(ElasticsearchClient client)
{
    public async Task<IResult> Handle(SearchApplicationsV2Request request)
    {
        if (request.Sort != "asc" && request.Sort != "desc")
        {
            return Results.BadRequest("Sort parameter must be either 'asc' or 'desc'");
        }

        var sortOrder = request.Sort == "asc" ? SortOrder.Asc : SortOrder.Desc;

        var searchResponse = await client.SearchAsync<ApplicationV2>(s => s
            .Indices(ApplicationV2Index.Alias)
            .Size(request.Size)
            .Query(q => new ApplicationV2QueryBuilder()
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
        return Results.Ok(new SearchApplicationsV2Response(applications, searchResponse.Total));
    }
}

public class ApplicationV2QueryBuilder
{
    private readonly List<Action<QueryDescriptor<ApplicationV2>>> _queries = [];
    private readonly List<(string field, string value)> _clientTerms = [];
    private List<ClientRole>? _clientRoles;

    public ApplicationV2QueryBuilder Term(string? value, Expression<Func<ApplicationV2, object?>> field)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _queries.Add(must => must.Term(t => t.Field(field).Value(value)));
        return this;
    }

    public ApplicationV2QueryBuilder DateRange(
        Expression<Func<ApplicationV2, DateTimeOffset>> field,
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

    public ApplicationV2QueryBuilder Client(
        SearchApplicationsV2Request request,
        Expression<Func<ApplicationV2Client, object>> field,
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

    public Query Build(QueryDescriptor<ApplicationV2> q)
    {
        AddClientQuery();

        return _queries.Count switch
        {
            0 => q.MatchAll(new MatchAllQuery()),
            1 => ApplySingle(q, _queries[0]),
            _ => q.Bool(b => b.Must(_queries.ToArray()))
        };
    }

    private static Query ApplySingle(QueryDescriptor<ApplicationV2> q, Action<QueryDescriptor<ApplicationV2>> action)
    {
        action(q);
        return q;
    }

    private void AddClientQuery()
    {
        if (_clientTerms.Count == 0)
            return;

        var roles = _clientRoles ?? [ClientRole.MainClient];

        // Build nested query: all client term filters + role filter within a single nested query on "clients"
        _queries.Add(must => must.Nested(n => n
            .Path(_ => _.Clients)
            .Query(nq => nq.Bool(b => b.Must(BuildNestedClientTerms(roles))))
        ));
    }

    private Action<QueryDescriptor<ApplicationV2>>[] BuildNestedClientTerms(List<ClientRole> roles)
    {
        var terms = _clientTerms.Select<(string field, string value), Action<QueryDescriptor<ApplicationV2>>>(
            t => m => m.Term(term => term.Field($"clients.{t.field}").Value(t.value))
        ).ToList();

        // Add role filter
        if (roles.Count == 1)
        {
            terms.Add(m => m.Term(term => term.Field("clients.role").Value(roles[0].ToString())));
        }
        else
        {
            terms.Add(m => m.Terms(term => term
                .Field("clients.role")
                .Terms(new TermsQueryField(roles.Select(r => FieldValue.String(r.ToString())).ToArray()))
            ));
        }

        return terms.ToArray();
    }
}
