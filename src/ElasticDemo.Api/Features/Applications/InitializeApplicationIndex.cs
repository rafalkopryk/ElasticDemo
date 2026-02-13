using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

namespace ElasticDemo.Api.Features.Applications;

public record InitializeApplicationIndexResponse(bool Success, string Message);

public class InitializeApplicationIndexHandler(ElasticsearchClient client)
{
    public async Task<IResult> Handle()
    {
        var aliasResponse = await client.Indices.ExistsAliasAsync(ApplicationIndex.Alias);
        if (aliasResponse.Exists)
        {
            return Results.Ok(new InitializeApplicationIndexResponse(true,
                $"Index '{ApplicationIndex.CurrentVersion}' with alias '{ApplicationIndex.Alias}' already exists"));
        }

        var createResponse = await client.Indices.CreateAsync(ApplicationIndex.CurrentVersion, c => c
            .Aliases(a => a
                .Add(ApplicationIndex.Alias, alias => { })
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
                .Properties<Application>(p => p
                    .Keyword(k => k.Id)
                    .Keyword(k => k.Product)
                    .Keyword(k => k.Transaction)
                    .Keyword(k => k.Channel, k => k.Normalizer("lowercase"))
                    .Keyword(k => k.Branch)
                    .Keyword(k => k.Status, k => k.Normalizer("lowercase"))
                    .Keyword(k => k.User, k => k.Normalizer("lowercase"))
                    .Date(d => d.CreatedAt)
                    .Date(d => d.UpdatedAt)
                    .Object(o => o.MainApplicant, o => o
                        .Properties(ApplicantProperties)
                    )
                    .Nested(n => n.CoApplicants, n => n
                        .Properties(ApplicantProperties)
                    )
                )
            )
        );

        if (!createResponse.IsValidResponse)
        {
            return Results.BadRequest(new InitializeApplicationIndexResponse(false,
                $"Failed to create index: {createResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}"));
        }

        return Results.Ok(new InitializeApplicationIndexResponse(true,
            $"Created index '{ApplicationIndex.CurrentVersion}' with alias '{ApplicationIndex.Alias}'"));
    }

    private static void ClientProperties<T>(PropertiesDescriptor<T> cp) => cp
        .Keyword("email", k => k.Normalizer("lowercase"))
        .Keyword("firstName", k => k.Normalizer("lowercase"))
        .Keyword("lastName", k => k.Normalizer("lowercase"))
        .Keyword("nationalId")
        .Keyword("clientId");

    private static void ApplicantProperties<T>(PropertiesDescriptor<T> ap) => ap
        .Object("client", o => o.Properties(ClientProperties))
        .Object("spouse", o => o.Properties(ClientProperties));
}
