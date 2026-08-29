namespace AiSearch.Labs.Basics.Configuration;

public class AzureSearchOptions
{
    public const string SectionName = "AzureSearch";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    public string ContentField { get; set; } = "Content";
    public string TitleField { get; set; } = "Title";
    public string? VectorField { get; set; }
    public string? SemanticConfigurationName { get; set; } = "semantic-config";

    public string CognitiveServicesKey { get; set; } = string.Empty;
    public int ChunkSize { get; set; } = 2000;
    public int ChunkOverlap { get; set; } = 200;
}