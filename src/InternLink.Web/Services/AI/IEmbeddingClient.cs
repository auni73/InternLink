namespace InternLink.Web.Services.AI;

public interface IEmbeddingClient
{
    /// <summary>Returns a 768-dimension embedding for <paramref name="text"/>.</summary>
    /// <exception cref="SemanticSearchUnavailableException">Any embedding failure.</exception>
    Task<float[]> EmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken ct = default);
}
