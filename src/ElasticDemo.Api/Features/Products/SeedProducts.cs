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
    IConfiguration configuration,
    EmbeddingService embeddingService)
{
    private bool SkipEmbeddings => configuration.GetValue<bool>("Embeddings:UseMock");

    // Without embeddings: ~8KB per product → 5000 products = ~40MB (safe)
    // With embeddings: ~16KB per product (4096 floats) → 500 products = ~8MB (safe)
    private int BatchSize => SkipEmbeddings ? 5_000 : 500;
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
                .Index(ProductIndex.Active)
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
                var failedCount = itemsWithErrors.Count();
                var successCount = batch.Count - failedCount;

                if (itemsWithErrors.Any())
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

    private async Task<float[][]> GenerateEmbeddingsInChunksAsync(List<Product> products)
    {
        const int EmbeddingBatchSize = 50;
        var results = new float[products.Count][];
        for (int i = 0; i < products.Count; i += EmbeddingBatchSize)
        {
            var chunk = products.GetRange(i, Math.Min(EmbeddingBatchSize, products.Count - i));
            var embeddings = await embeddingService.GenerateEmbeddingsAsync(chunk);
            embeddings.CopyTo(results, i);
        }
        return results;
    }

    public async Task<IResult> Handle()
    {
        var batchSize = BatchSize;
        var skipEmbeddings = SkipEmbeddings;
        logger.LogInformation("Seeding with BatchSize={BatchSize}, SkipEmbeddings={SkipEmbeddings}", batchSize, skipEmbeddings);

        var currentBatch = new List<Product>(batchSize);

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

                if (currentBatch.Count >= batchSize)
                {
                    List<Product> productsToIndex;
                    if (skipEmbeddings)
                    {
                        logger.LogInformation("Indexing batch {BatchNumber} with {Count} products (no embeddings)", batchNumber + 1, currentBatch.Count);
                        productsToIndex = currentBatch;
                    }
                    else
                    {
                        logger.LogInformation("Generating embeddings for batch {BatchNumber} with {Count} products", batchNumber + 1, currentBatch.Count);
                        var embeddings = await GenerateEmbeddingsInChunksAsync(currentBatch);
                        productsToIndex = currentBatch
                            .Select((p, i) => p with { Embedding = embeddings[i] })
                            .ToList();
                    }

                    var result = await SendBatchAsync(productsToIndex, ++batchNumber);
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
                List<Product> productsToIndex;
                if (skipEmbeddings)
                {
                    logger.LogInformation("Indexing final batch {BatchNumber} with {Count} products (no embeddings)", batchNumber + 1, currentBatch.Count);
                    productsToIndex = currentBatch;
                }
                else
                {
                    logger.LogInformation("Generating embeddings for final batch {BatchNumber} with {Count} products", batchNumber + 1, currentBatch.Count);
                    var embeddings = await GenerateEmbeddingsInChunksAsync(currentBatch);
                    productsToIndex = currentBatch
                        .Select((p, i) => p with { Embedding = embeddings[i] })
                        .ToList();
                }

                var result = await SendBatchAsync(productsToIndex, ++batchNumber);
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
