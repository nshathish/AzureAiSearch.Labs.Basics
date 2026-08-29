namespace AiSearch.Labs.Basics.Models;

public class SearchResponseView
{
    public long? TotalCount { get; set; }

    public string Mode { get; set; } = string.Empty;

    public List<SearchAnswerView> Answers { get; set; } = new();

    public List<SearchResultView> Results { get; set; } = new();
}

public class SearchAnswerView
{
    public string Text { get; set; } = string.Empty;

    public double? Score { get; set; }
}

public class SearchResultView
{
    public string? ChunkId { get; set; }
    public string? ParentId { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;

    public double? KeywordScore { get; set; }
    public double? SemanticScore { get; set; }
}