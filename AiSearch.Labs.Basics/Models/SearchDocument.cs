using Azure.Search.Documents.Indexes;
using System.Text.Json.Serialization;

namespace AiSearch.Labs.Basics.Models;

public class SearchDocument
{
    [JsonPropertyName("ChunkId")]
    [SearchableField(
        IsKey = true,
        IsFilterable = true,
        AnalyzerName = "keyword")]
    public string ChunkId { get; set; } = string.Empty;

    [JsonPropertyName("ParentId")]
    [SimpleField(IsFilterable = true)]
    public string ParentId { get; set; } = string.Empty;

    [JsonPropertyName("Title")]
    [SearchableField(
        IsFilterable = true,
        IsSortable = true)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("Content")]
    [SearchableField]
    public string Content { get; set; } = string.Empty;
}