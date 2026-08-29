using AiSearch.Labs.Basics.Configuration;
using AiSearch.Labs.Basics.Models;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using SearchDocument = AiSearch.Labs.Basics.Models.SearchDocument;

namespace AiSearch.Labs.Basics.Services;

public sealed class AzureAiSearchService
{
    private readonly AzureSearchOptions _options;
    private readonly AzureKeyCredential _credential;
    private readonly Uri _serviceUri;

    public AzureAiSearchService(
        IOptions<AzureSearchOptions> options)
    {
        _options = options.Value;

        _serviceUri = new Uri(_options.Endpoint);
        _credential = new AzureKeyCredential(_options.ApiKey);
    }

    public async Task<SearchResponseView> SearchAsync(
        string indexName,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new SearchResponseView
            {
                TotalCount = 0,
                Mode = "Keyword"
            };

        var searchClient = new SearchClient(
            _serviceUri,
            indexName,
            _credential);

        var options = new SearchOptions
        {
            Size = 5,
            IncludeTotalCount = true,
            QueryType = SearchQueryType.Simple,
            SearchMode = SearchMode.Any
        };

        // Search within the chunk content.
        options.SearchFields.Add(nameof(SearchDocument.Content));

        // Only return fields needed by the application.
        options.Select.Add(nameof(SearchDocument.ChunkId));
        options.Select.Add(nameof(SearchDocument.ParentId));
        options.Select.Add(nameof(SearchDocument.Title));
        options.Select.Add(nameof(SearchDocument.Content));


        var response = await searchClient.SearchAsync<SearchDocument>(
            query,
            options,
            cancellationToken);

        var output = new SearchResponseView
        {
            TotalCount = response.Value.TotalCount,
            Mode = "Keyword"
        };

        await foreach (var result in response.Value.GetResultsAsync())
        {
            var document = result.Document;

            output.Results.Add(new SearchResultView
            {
                ChunkId = result.Document.ChunkId,
                ParentId = result.Document.ParentId,
                Title = result.Document.Title,
                Content = result.Document.Content,
                Score = result.Score
            });
        }

        return output;
    }
    
}