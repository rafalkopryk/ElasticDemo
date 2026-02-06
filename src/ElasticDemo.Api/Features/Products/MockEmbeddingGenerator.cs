using Microsoft.Extensions.AI;

namespace ElasticDemo.Api.Features.Products;

public class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = values.Select(_ =>
        {
            var vector = new float[EmbeddingService.Dimensions];
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] = (float)Random.Shared.NextDouble();
            }
            return new Embedding<float>(vector);
        }).ToList();

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
