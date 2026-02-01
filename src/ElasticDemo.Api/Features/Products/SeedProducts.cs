using System.Text.Json;
using Elastic.Clients.Elasticsearch;

namespace ElasticDemo.Api.Features.Products;

public record SeedProductsResponse(
    bool Success,
    string Message,
    int TotalProcessed,
    int SuccessCount,
    int FailedCount,
    int BatchCount,
    List<string>? Errors = null);

public class SeedProductsHandler(
    ElasticsearchClient client,
    IWebHostEnvironment env,
    ILogger<SeedProductsHandler> logger,
    EmbeddingService embeddingService)
{
    private record BatchResult(int Success, int Failed, string? Error);

    private async IAsyncEnumerable<Product> StreamProductsAsync()
    {
        var basePath = env.IsDevelopment()
            ? env.ContentRootPath
            : AppContext.BaseDirectory;

        var filePath = Path.Combine(basePath, "Features", "Products", "sample-products.json");

        using var fileStream = File.OpenRead(filePath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        await foreach (var product in JsonSerializer.DeserializeAsyncEnumerable<Product>(fileStream, options))
        {
            if (product != null)
            {
                yield return product;
            }
        }
    }

    private async Task<BatchResult> SendBatchAsync(List<Product> batch, int batchNumber)
    {
        try
        {
            var bulkResponse = await client.BulkAsync(b => b
                .Index(InitializeIndexHandler.IndexName)
                .IndexMany(batch)
            );

            if (!bulkResponse.IsValidResponse)
            {
                var error = $"Batch {batchNumber} request failed: {bulkResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}";
                logger.LogError("Batch {BatchNumber} failed: {Error}", batchNumber, error);
                return new BatchResult(0, batch.Count, error);
            }

            if (bulkResponse.Errors)
            {
                var failedCount = bulkResponse.ItemsWithErrors.Count();
                var successCount = batch.Count - failedCount;
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
            var error = $"Batch {batchNumber} exception: {ex.Message}";
            logger.LogError(ex, "Batch {BatchNumber} threw exception", batchNumber);
            return new BatchResult(0, batch.Count, error);
        }
    }

    public async Task<IResult> Handle()
    {
        // Smaller batch size since embedding generation is slower
        const int BatchSize = 50;
        var currentBatch = new List<Product>(BatchSize);

        int totalProcessed = 0;
        int successCount = 0;
        int failedCount = 0;
        int batchNumber = 0;
        var errors = new List<string>();

        try
        {
            await foreach (var product in StreamProductsAsync())
            {
                currentBatch.Add(product);

                if (currentBatch.Count >= BatchSize)
                {
                    // Generate embeddings for entire batch at once
                    var embeddings = await embeddingService.GenerateEmbeddingsAsync(currentBatch);
                    var productsWithEmbeddings = currentBatch
                        .Select((p, i) => p with { Embedding = embeddings[i] })
                        .ToList();

                    var result = await SendBatchAsync(productsWithEmbeddings, ++batchNumber);
                    totalProcessed += currentBatch.Count;
                    successCount += result.Success;
                    failedCount += result.Failed;
                    if (result.Error != null) errors.Add(result.Error);

                    currentBatch.Clear();
                }
            }

            // Send final partial batch
            if (currentBatch.Count > 0)
            {
                var embeddings = await embeddingService.GenerateEmbeddingsAsync(currentBatch);
                var productsWithEmbeddings = currentBatch
                    .Select((p, i) => p with { Embedding = embeddings[i] })
                    .ToList();

                var result = await SendBatchAsync(productsWithEmbeddings, ++batchNumber);
                totalProcessed += currentBatch.Count;
                successCount += result.Success;
                failedCount += result.Failed;
                if (result.Error != null) errors.Add(result.Error);
            }
        }
        catch (FileNotFoundException ex)
        {
            return Results.Problem(
                detail: $"Sample products file not found: {ex.Message}",
                statusCode: 500,
                title: "File Not Found"
            );
        }
        catch (JsonException ex)
        {
            return Results.Problem(
                detail: $"Failed to parse sample products JSON: {ex.Message}",
                statusCode: 500,
                title: "JSON Parse Error"
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Failed to load sample products: {ex.Message}",
                statusCode: 500,
                title: "Load Error"
            );
        }

        // Build response
        logger.LogInformation(
            "Seeding completed: {Total} total, {Success} succeeded, {Failed} failed, {Batches} batches",
            totalProcessed, successCount, failedCount, batchNumber);

        var success = successCount > 0;
        var message = success
            ? failedCount > 0
                ? $"Partially completed: {successCount} products seeded, {failedCount} failed across {batchNumber} batches"
                : $"Successfully seeded {successCount} products in {batchNumber} batches"
            : $"Failed to seed products: {failedCount} failures across {batchNumber} batches";

        return Results.Ok(new SeedProductsResponse(
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
