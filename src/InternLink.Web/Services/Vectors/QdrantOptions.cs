namespace InternLink.Web.Services.Vectors;

public class QdrantOptions
{
    public const string SectionName = "Qdrant";

    /// <summary>Cluster base URL with no port. gRPC port 6334 is applied by the client.</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string CollectionName { get; set; } = "internlink-jobs";

    public int GrpcPort { get; set; } = 6334;
}
