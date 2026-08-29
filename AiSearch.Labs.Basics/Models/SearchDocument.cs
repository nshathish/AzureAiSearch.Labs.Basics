using Azure.Search.Documents.Indexes;

namespace AiSearch.Labs.Basics.Models;

public class SearchDocument
{
    [SearchableField(
        IsKey = true,
        IsFilterable = true,
        AnalyzerName = "keyword")]
    public string ChunkId { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    public string ParentId { get; set; } = string.Empty;

    [SearchableField(
        IsFilterable = true,
        IsSortable = true)]
    public string Title { get; set; } = string.Empty;

    [SearchableField]
    public string Content { get; set; } = string.Empty;

    [VectorSearchField(
        VectorSearchDimensions = 1536,
        VectorSearchProfileName = "vector-profile")]
    public float[] ContentVector { get; set; } = [];
}