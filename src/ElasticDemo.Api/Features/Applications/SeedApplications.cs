using System.Text.Json;
using Elastic.Clients.Elasticsearch;

namespace ElasticDemo.Api.Features.Applications;

public record SeedApplicationsResponse(
    bool Success,
    string Message,
    int TotalProcessed,
    int SuccessCount,
    int FailedCount,
    int BatchCount,
    List<string>? Errors = null);

public class SeedApplicationsHandler(
    ElasticsearchClient client,
    ILogger<SeedApplicationsHandler> logger)
{
    private const int BatchSize = 10_000;

    private record BatchResult(int Success, int Failed, string? Error);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private async Task<BatchResult> SendBatchAsync(List<Application> batch, int batchNumber)
    {
        try
        {
            var bulkResponse = await client.BulkAsync(b => b
                .Index(ApplicationIndex.Alias)
                .IndexMany(batch)
            );

            logger.LogInformation("Batch {BatchNumber} response: IsValidResponse={IsValid}, Errors={HasErrors}",
                batchNumber, bulkResponse.IsValidResponse, bulkResponse.Errors);

            if (!bulkResponse.IsValidResponse)
            {
                logger.LogError("Batch {BatchNumber} transport failure: {DebugInfo}",
                    batchNumber, bulkResponse.DebugInformation);
                return new BatchResult(0, batch.Count, $"Batch {batchNumber} transport failure");
            }

            if (bulkResponse.Errors)
            {
                var itemsWithErrors = bulkResponse.ItemsWithErrors.ToList();
                var failedCount = itemsWithErrors.Count;
                var successCount = batch.Count - failedCount;

                if (itemsWithErrors.Count > 0)
                {
                    var firstError = itemsWithErrors.First();
                    logger.LogWarning("Batch {BatchNumber} first item error: {ErrorMessage}",
                        batchNumber, firstError.Error?.Reason ?? "Unknown reason");
                }

                var error = $"Batch {batchNumber} had {failedCount} document errors";
                logger.LogWarning("Batch {BatchNumber} partial failure: {Success} succeeded, {Failed} failed",
                    batchNumber, successCount, failedCount);
                return new BatchResult(successCount, failedCount, error);
            }

            logger.LogInformation("Batch {BatchNumber} completed: {Success} succeeded, {Failed} failed",
                batchNumber, batch.Count, 0);
            return new BatchResult(batch.Count, 0, null);
        }
        catch (Exception ex)
        {
            var error = $"Batch {batchNumber} request failed: {ex.GetBaseException().Message ?? "Unknown error"}";
            logger.LogError(ex, "Batch {BatchNumber} threw exception: {ExceptionMessage}", batchNumber, ex.GetBaseException().Message);
            return new BatchResult(0, batch.Count, error);
        }
    }

    public async Task<IResult> Handle(Stream body)
    {
        logger.LogInformation("Seeding applications with BatchSize={BatchSize}", BatchSize);

        var currentBatch = new List<Application>(BatchSize);

        int totalProcessed = 0;
        int successCount = 0;
        int failedCount = 0;
        int batchNumber = 0;
        var errors = new List<string>();

        try
        {
            await foreach (var application in JsonSerializer.DeserializeAsyncEnumerable<Application>(body, JsonOptions))
            {
                if (application is null) continue;
                currentBatch.Add(application);

                if (currentBatch.Count >= BatchSize)
                {
                    logger.LogInformation("Indexing batch {BatchNumber} with {Count} applications",
                        batchNumber + 1, currentBatch.Count);

                    var result = await SendBatchAsync(currentBatch, ++batchNumber);
                    totalProcessed += currentBatch.Count;
                    successCount += result.Success;
                    failedCount += result.Failed;
                    if (result.Error != null) errors.Add(result.Error);

                    currentBatch.Clear();
                }
            }

            if (currentBatch.Count > 0)
            {
                logger.LogInformation("Indexing final batch {BatchNumber} with {Count} applications",
                    batchNumber + 1, currentBatch.Count);

                var result = await SendBatchAsync(currentBatch, ++batchNumber);
                totalProcessed += currentBatch.Count;
                successCount += result.Success;
                failedCount += result.Failed;
                if (result.Error != null) errors.Add(result.Error);
            }
        }
        catch (JsonException ex)
        {
            return Results.Problem(
                detail: $"Failed to parse applications JSON: {ex.Message}",
                statusCode: 500,
                title: "JSON Parse Error"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to load applications: {ex.Message}",
                statusCode: 500,
                title: "Load Error"
            );
        }

        logger.LogInformation(
            "Seeding completed: {Total} total, {Success} succeeded, {Failed} failed, {Batches} batches",
            totalProcessed, successCount, failedCount, batchNumber);

        var success = successCount > 0;
        var message = success
            ? failedCount > 0
                ? $"Partially completed: {successCount} applications seeded, {failedCount} failed across {batchNumber} batches"
                : $"Successfully seeded {successCount} applications in {batchNumber} batches"
            : $"Failed to seed applications: {failedCount} failures across {batchNumber} batches";

        return Results.Ok(new SeedApplicationsResponse(
            success,
            message,
            totalProcessed,
            successCount,
            failedCount,
            batchNumber,
            errors.Count > 0 ? errors : null
        ));
    }
}
