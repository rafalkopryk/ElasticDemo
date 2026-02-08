using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;

namespace ElasticDemo.Api.Features.Applications;

public record InitializeApplicationIndexResponse(bool Success, string Message);

public class InitializeApplicationIndexHandler(ElasticsearchClient client)
{
    public async Task<IResult> Handle()
    {
        var existsResponse = await client.Indices.ExistsAsync(ApplicationIndex.Active);
        if (existsResponse.Exists)
        {
            return Results.Ok(new InitializeApplicationIndexResponse(true,
                $"Index '{ApplicationIndex.Active}' already exists"));
        }

        var createResponse = await client.Indices.CreateAsync(ApplicationIndex.Active, c => c
            .Settings(s => s
                .Analysis(a => a
                    .Normalizers(n => n
                        .Custom("lowercase", cn => cn.Filter(["lowercase"]))
                    )
                )
            )
            .Mappings(m => m
                .Properties<Application>(p => p
                    .Keyword(k => k.Id)
                    .Keyword(k => k.Product)
                    .Keyword(k => k.Transaction)
                    .Keyword(k => k.Channel)
                    .Keyword(k => k.Branch)
                    .Keyword(k => k.Status)
                    .Keyword(k => k.User)
                    .Date(d => d.CreatedAt)
                    .Date(d => d.UpdatedAt)
                    .Object(o => o.MainClient, o => o
                        .Properties(cp => cp
                            .Keyword("email", k => k.Normalizer("lowercase"))
                            .Keyword("firstName", k => k.Normalizer("lowercase"))
                            .Keyword("lastName", k => k.Normalizer("lowercase"))
                            .Keyword("nationalId")
                            .Keyword("clientId")
                        )
                    )
                    .Object(o => o.Spouse, o => o
                        .Properties(cp => cp
                            .Keyword("email", k => k.Normalizer("lowercase"))
                            .Keyword("firstName", k => k.Normalizer("lowercase"))
                            .Keyword("lastName", k => k.Normalizer("lowercase"))
                            .Keyword("nationalId")
                            .Keyword("clientId")
                        )
                    )
                    .Nested(n => n.CoApplicants, n => n
                        .Properties(cp => cp
                            .Keyword("email", k => k.Normalizer("lowercase"))
                            .Keyword("firstName", k => k.Normalizer("lowercase"))
                            .Keyword("lastName", k => k.Normalizer("lowercase"))
                            .Keyword("nationalId")
                            .Keyword("clientId")
                        )
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
            $"Created index '{ApplicationIndex.Active}'"));
    }
}
