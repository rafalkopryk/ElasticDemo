using Microsoft.Extensions.AI;

namespace ElasticDemo.Api.Features.Products;

public class EmbeddingService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    public const int Dimensions = 4096;

    public async Task<float[]> GenerateEmbeddingAsync(Product product)
    {
        var colors = product.Variants.Select(x => x.Color).Distinct().ToArray();
        var sizes = product.Variants.Select(x => x.Size).Where(s => s != null).Distinct().ToArray();
        var text = $"{product.Name}. Category: {product.Category}. Tags: {string.Join(", ", product.Tags)}. Colors: {string.Join(", ", colors)}. Sizes: {string.Join(", ", sizes)}";
        return await GenerateEmbeddingAsync(text);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var embedding = await embeddingGenerator.GenerateAsync(text);
        return embedding.Vector.ToArray();
    }

    public async Task<float[][]> GenerateEmbeddingsAsync(IReadOnlyList<Product> products)
    {
        var texts = products.Select(p =>
        {
            var colors = p.Variants.Select(x => x.Color).Distinct().ToArray();
            var sizes = p.Variants.Select(x => x.Size).Where(s => s != null).Distinct().ToArray();
            return $"{p.Name}. Category: {p.Category}. Tags: {string.Join(", ", p.Tags)}. Colors: {string.Join(", ", colors)}. Sizes: {string.Join(", ", sizes)}";
        }).ToList();

        var embeddings = await embeddingGenerator.GenerateAsync(texts);
        return embeddings.Select(e => e.Vector.ToArray()).ToArray();
    }
}
