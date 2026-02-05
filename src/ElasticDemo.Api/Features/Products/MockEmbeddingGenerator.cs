using Microsoft.Extensions.AI;

namespace ElasticDemo.Api.Features.Products;

public class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public EmbeddingGeneratorMetadata Metadata { get; } = new("mock");

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = values.Select(_ =>
            new Embedding<float>(new float[EmbeddingService.Dimensions])).ToList();

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
