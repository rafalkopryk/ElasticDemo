using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace ElasticDemo.Api.Features.Products;

public record ArchiveProductsResponse(bool Success, string Message, long ArchivedCount);

public class ArchiveProductsHandler(
    ElasticsearchClient client,
    TimeProvider timeProvider,
    ILogger<ArchiveProductsHandler> logger)
{
    public async Task<IResult> Handle()
    {
        var cutoff = timeProvider.GetUtcNow().AddYears(-1);
        long totalArchived = 0;

        // Find distinct years of products older than cutoff in the active index
        var aggsResponse = await client.SearchAsync<Product>(s => s
            .Indices(ProductIndex.Active)
            .Size(0)
            .Query(q => q.Range(r => r
                .Date(dr => dr.Field(f => f.CreatedAt).Lt(cutoff.ToString("o")))
            ))
            .Aggregations(aggs => aggs
                .Add("years", a => a.DateHistogram(dh => dh
                    .Field(f => f.CreatedAt)
                    .CalendarInterval(Elastic.Clients.Elasticsearch.Aggregations.CalendarInterval.Year)
                ))
            )
        );

        if (!aggsResponse.IsValidResponse)
        {
            return Results.BadRequest($"Failed to query active index: {aggsResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
        }

        var yearBuckets = aggsResponse.Aggregations?
            .GetDateHistogram("years")?.Buckets;

        if (yearBuckets == null || yearBuckets.Count == 0)
        {
            return Results.Ok(new ArchiveProductsResponse(true, "No products older than 1 year to archive", 0));
        }

        var years = yearBuckets
            .Where(b => b.DocCount > 0)
            .Select(b =>
            {
                if (b.KeyAsString != null)
                    return DateTimeOffset.Parse(b.KeyAsString).Year;
                return 0;
            })
            .Where(y => y > 0)
            .Distinct()
            .ToList();

        foreach (var year in years)
        {
            var archiveIndex = ProductIndex.ArchiveForYear(year);
            var yearStart = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var yearEnd = new DateTimeOffset(year, 12, 31, 23, 59, 59, TimeSpan.Zero);
            // Ensure we don't archive anything newer than cutoff
            var effectiveEnd = yearEnd < cutoff ? yearEnd : cutoff.AddTicks(-1);

            logger.LogInformation("Archiving year {Year} products to {ArchiveIndex}", year, archiveIndex);

            // Reindex from active to archive for this year
            var reindexResponse = await client.ReindexAsync(r => r
                .Source(src => src
                    .Indices((Indices)ProductIndex.Active)
                    .Query(q => q.Range(rng => rng
                        .Date(dr => dr
                            .Field("createdAt")
                            .Gte(yearStart.ToString("o"))
                            .Lte(effectiveEnd.ToString("o"))
                        )
                    ))
                )
                .Dest(dst => dst.Index(archiveIndex))
            );

            if (!reindexResponse.IsValidResponse)
            {
                logger.LogError("Failed to reindex year {Year}: {Error}", year,
                    reindexResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error");
                continue;
            }

            var reindexedCount = reindexResponse.Created + reindexResponse.Updated;
            logger.LogInformation("Reindexed {Count} products to {ArchiveIndex}", reindexedCount, archiveIndex);

            // Delete archived products from active index
            var deleteResponse = await client.DeleteByQueryAsync<Product>(ProductIndex.Active, d => d
                .Query(q => q.Range(rng => rng
                    .Date(dr => dr
                        .Field(f => f.CreatedAt)
                        .Gte(yearStart.ToString("o"))
                        .Lte(effectiveEnd.ToString("o"))
                    )
                ))
            );

            if (!deleteResponse.IsValidResponse)
            {
                logger.LogError("Failed to delete archived products for year {Year}: {Error}", year,
                    deleteResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error");
                continue;
            }

            totalArchived += deleteResponse.Deleted ?? 0;
            logger.LogInformation("Deleted {Count} archived products from active index for year {Year}",
                deleteResponse.Deleted, year);
        }

        return Results.Ok(new ArchiveProductsResponse(true,
            $"Archived {totalArchived} products across {years.Count} year(s)", totalArchived));
    }
}
