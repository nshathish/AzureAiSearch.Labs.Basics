using AiSearch.Labs.Basics.Configuration;
using AiSearch.Labs.Basics.Models;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using SearchDocument = AiSearch.Labs.Basics.Models.SearchDocument;

namespace AiSearch.Labs.Basics.Services;

public sealed class AzureAiSearchService
{
    private readonly AzureSearchOptions _searchOptions;
    private readonly AzureOpenAiSettings _openAiOptions;

    private readonly AzureKeyCredential _searchCredential;
    private readonly Uri _searchServiceUri;

    private readonly EmbeddingClient _embeddingClient;

    public AzureAiSearchService(
        IOptions<AzureSearchOptions> searchOptions,
        IOptions<AzureOpenAiSettings> openAiOptions)
    {
        _searchOptions = searchOptions.Value;
        _openAiOptions = openAiOptions.Value;

        _searchServiceUri = new Uri(_searchOptions.Endpoint);
        _searchCredential =
            new AzureKeyCredential(_searchOptions.ApiKey);

        var azureOpenAiClient = new AzureOpenAIClient(
            new Uri(_openAiOptions.Endpoint),
            new AzureKeyCredential(_openAiOptions.ApiKey));

        _embeddingClient = azureOpenAiClient.GetEmbeddingClient(
            _openAiOptions.EmbeddingDeploymentName);
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
                Mode = "Semantic"
            };

        var searchClient = new SearchClient(
            _searchServiceUri,
            indexName,
            _searchCredential);

        // Convert the user's query into an embedding.
        var embeddingResponse =
            await _embeddingClient.GenerateEmbeddingAsync(
                query,
                new EmbeddingGenerationOptions
                {
                    Dimensions =
                        _openAiOptions.EmbeddingDimensions
                },
                cancellationToken);

        ReadOnlyMemory<float> queryVector =
            embeddingResponse.Value.ToFloats();

        // Search for the five closest chunk vectors.
        var vectorQuery = new VectorizedQuery(queryVector)
        {
            KNearestNeighborsCount = 5
        };

        vectorQuery.Fields.Add(
            nameof(SearchDocument.ContentVector));

        var options = new SearchOptions
        {
            Size = 5,
            IncludeTotalCount = true,
            VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    vectorQuery
                }
            }
        };

        // Do not return ContentVector: it contains 1536 numbers.
        options.Select.Add(nameof(SearchDocument.ChunkId));
        options.Select.Add(nameof(SearchDocument.ParentId));
        options.Select.Add(nameof(SearchDocument.Title));
        options.Select.Add(nameof(SearchDocument.Content));


        // Null means there is no keyword query.
        var response =
            await searchClient.SearchAsync<SearchDocument>(
                searchText: null,
                options,
                cancellationToken);

        var output = new SearchResponseView
        {
            TotalCount = response.Value.TotalCount,
            Mode = "Vector"
        };

        await foreach (
            var result in response.Value.GetResultsAsync())
        {
            var document = result.Document;

            output.Results.Add(new SearchResultView
            {
                ChunkId = document.ChunkId,
                ParentId = document.ParentId,
                Title = document.Title,
                Content = document.Content,

                SemanticScore = null,

                // For pure vector search, Score is the vector score.
                KeywordScore = result.Score
            });
        }

        return output;
    }
}