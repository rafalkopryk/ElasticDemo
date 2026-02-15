using Elastic.Clients.Elasticsearch;
using ElasticDemo.Api.Features.Applications;

namespace ElasticDemo.Api.Features.ApplicationsV2;

public record MigrateApplicationsV2Response(bool Success, string Message, long? Total = null);

public class MigrateApplicationsV2Handler(ElasticsearchClient client)
{
    private const string PainlessScript = """
        def clients = [];
        if (ctx._source.mainApplicant != null) {
          def parentId = null;
          if (ctx._source.mainApplicant.client != null) {
            def c = ctx._source.mainApplicant.client;
            c.role = 'MainClient';
            c.parentClientId = null;
            parentId = c.clientId;
            clients.add(c);
          }
          if (ctx._source.mainApplicant.spouse != null) {
            def s = ctx._source.mainApplicant.spouse;
            s.role = 'Spouse';
            s.parentClientId = parentId;
            clients.add(s);
          }
        }
        if (ctx._source.coApplicants != null) {
          for (def co : ctx._source.coApplicants) {
            def parentId = null;
            if (co.client != null) {
              def c = co.client;
              c.role = 'CoApplicant';
              c.parentClientId = null;
              parentId = c.clientId;
              clients.add(c);
            }
            if (co.spouse != null) {
              def s = co.spouse;
              s.role = 'Spouse';
              s.parentClientId = parentId;
              clients.add(s);
            }
          }
        }
        ctx._source.clients = clients;
        ctx._source.remove('mainApplicant');
        ctx._source.remove('coApplicants');
        """;

    public async Task<IResult> Handle()
    {
        var aliasResponse = await client.Indices.ExistsAliasAsync(ApplicationV2Index.Alias);
        if (!aliasResponse.Exists)
        {
            return Results.BadRequest(new MigrateApplicationsV2Response(false,
                $"Target index alias '{ApplicationV2Index.Alias}' does not exist. Initialize V2 index first."));
        }

        var sourceExists = await client.Indices.ExistsAliasAsync(ApplicationIndex.Alias);
        if (!sourceExists.Exists)
        {
            return Results.BadRequest(new MigrateApplicationsV2Response(false,
                $"Source index alias '{ApplicationIndex.Alias}' does not exist. No data to migrate."));
        }

        var reindexResponse = await client.ReindexAsync(r => r
            .Source(s => s.Indices((Indices)ApplicationIndex.Alias))
            .Dest(d => d.Index(ApplicationV2Index.Alias))
            .Script(s => s.Source(PainlessScript).Lang("painless"))
        );

        if (!reindexResponse.IsValidResponse)
        {
            return Results.BadRequest(new MigrateApplicationsV2Response(false,
                $"Migration failed: {reindexResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}"));
        }

        return Results.Ok(new MigrateApplicationsV2Response(true,
            $"Migrated {reindexResponse.Total} documents from '{ApplicationIndex.Alias}' to '{ApplicationV2Index.Alias}'",
            reindexResponse.Total));
    }
}
