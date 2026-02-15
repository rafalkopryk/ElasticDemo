using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

namespace ElasticDemo.Api.Features.ApplicationsV2;

public record InitializeApplicationV2IndexResponse(bool Success, string Message);

public class InitializeApplicationV2IndexHandler(ElasticsearchClient client)
{
    public async Task<IResult> Handle()
    {
        var aliasResponse = await client.Indices.ExistsAliasAsync(ApplicationV2Index.Alias);
        if (aliasResponse.Exists)
        {
            return Results.Ok(new InitializeApplicationV2IndexResponse(true,
                $"Index '{ApplicationV2Index.CurrentVersion}' with alias '{ApplicationV2Index.Alias}' already exists"));
        }

        var createResponse = await client.Indices.CreateAsync(ApplicationV2Index.CurrentVersion, c => c
            .Aliases(a => a
                .Add(ApplicationV2Index.Alias, alias => { })
            )
            .Settings(s => s
                .Analysis(a => a
                    .Normalizers(n => n
                        .Custom("lowercase", cn => cn.Filter(["lowercase"]))
                    )
                )
            )
            .Mappings(m => m
                .Dynamic(DynamicMapping.Strict)
                .Properties<ApplicationV2>(p => p
                    .Keyword(k => k.Id)
                    .Keyword(k => k.Product)
                    .Keyword(k => k.Transaction)
                    .Keyword(k => k.Channel, k => k.Normalizer("lowercase"))
                    .Keyword(k => k.Branch)
                    .Keyword(k => k.Status, k => k.Normalizer("lowercase"))
                    .Keyword(k => k.User, k => k.Normalizer("lowercase"))
                    .Date(d => d.CreatedAt)
                    .Date(d => d.UpdatedAt)
                    .Nested(n => n.Clients, n => n
                        .Properties(ClientProperties)
                    )
                )
            )
        );

        if (!createResponse.IsValidResponse)
        {
            return Results.BadRequest(new InitializeApplicationV2IndexResponse(false,
                $"Failed to create index: {createResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}"));
        }

        return Results.Ok(new InitializeApplicationV2IndexResponse(true,
            $"Created index '{ApplicationV2Index.CurrentVersion}' with alias '{ApplicationV2Index.Alias}'"));
    }

    private static void ClientProperties<T>(PropertiesDescriptor<T> cp) => cp
        .Keyword("email", k => k.Normalizer("lowercase"))
        .Keyword("firstName", k => k.Normalizer("lowercase"))
        .Keyword("lastName", k => k.Normalizer("lowercase"))
        .Keyword("nationalId")
        .Keyword("clientId")
        .Keyword("role")
        .Keyword("parentClientId");
}
