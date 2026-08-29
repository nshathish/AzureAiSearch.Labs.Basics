namespace AiSearch.Labs.Basics.Configuration;

public class AzureOpenAiSettings
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string EmbeddingDeploymentName { get; set; } = string.Empty;

    public string EmbeddingModelName { get; set; } = string.Empty;

    public int EmbeddingDimensions { get; set; }
}