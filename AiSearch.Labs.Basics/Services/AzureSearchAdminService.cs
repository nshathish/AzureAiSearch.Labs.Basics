using AiSearch.Labs.Basics.Configuration;
using AiSearch.Labs.Basics.Models;
using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Options;

namespace AiSearch.Labs.Basics.Services;

public sealed class AzureSearchAdminService
{
    private readonly AzureSearchOptions _searchOptions;
    private readonly AzureStorageOptions _storageOptions;
    private readonly AzureOpenAiSettings _openAIOptions;

    private readonly SearchIndexClient _indexClient;
    private readonly SearchIndexerClient _indexerClient;

    public AzureSearchAdminService(
        IOptions<AzureSearchOptions> searchOptions,
        IOptions<AzureStorageOptions> storageOptions,
        IOptions<AzureOpenAiSettings> openAIOptions 
    )
    {
        _searchOptions = searchOptions.Value;
        _storageOptions = storageOptions.Value;
        _openAIOptions = openAIOptions.Value;

        _indexClient = new SearchIndexClient(
            new Uri(_searchOptions.Endpoint),
            new AzureKeyCredential(_searchOptions.ApiKey)
        );

        _indexerClient = new SearchIndexerClient(
            new Uri(_searchOptions.Endpoint),
            new AzureKeyCredential(_searchOptions.ApiKey)
        );
    }

    public async Task CreateIndexAsync(string indexName)
    {
        ValidateEnrichmentOptions();

        var fields = new FieldBuilder().Build(typeof(SearchDocument));

        var prioritizedFields = new SemanticPrioritizedFields
        {
            TitleField = new SemanticField(
                nameof(SearchDocument.Title))
        };

        prioritizedFields.ContentFields.Add(
            new SemanticField(nameof(SearchDocument.Content)));

        var semanticConfiguration = new SemanticConfiguration(
            _searchOptions.SemanticConfigurationName,
            prioritizedFields);

        var index = new SearchIndex(indexName, fields)
        {
            VectorSearch = new VectorSearch
            {
                Algorithms =
                {
                    new HnswAlgorithmConfiguration("hnsw-config")
                },
                Profiles =
                {
                    new VectorSearchProfile(
                        "vector-profile",
                        "hnsw-config")
                }
            },
            SemanticSearch = new SemanticSearch
            {
                DefaultConfigurationName =
                    _searchOptions.SemanticConfigurationName,

                Configurations =
                {
                    semanticConfiguration
                }
            }
        };

        await _indexClient.CreateIndexAsync(index);
    }

    public async Task<List<string>> GetIndexesAsync()
    {
        var indexes = new List<string>();

        await foreach (var indexName in _indexClient.GetIndexNamesAsync())
        {
            indexes.Add(indexName);
        }

        return indexes;
    }

    public async Task<string> CreateDataSourceAsync(string indexName)
    {
        ValidateEnrichmentOptions();

        var dataSourceName = $"{indexName}-blob-datasource";
        var indexerName = $"{indexName}-indexer";

        var container = new SearchIndexerDataContainer(
            _storageOptions.ContainerName.Trim());

        var dataSource = new SearchIndexerDataSourceConnection(
            dataSourceName,
            SearchIndexerDataSourceType.AzureBlob,
            _storageOptions.ConnectionString.Trim(),
            container);

        await _indexerClient.CreateOrUpdateDataSourceConnectionAsync(
            dataSource);

        // Read it back to verify what Azure received.
        var createdDataSource =
            await _indexerClient.GetDataSourceConnectionAsync(
                dataSourceName);

        if (!string.Equals(
                createdDataSource.Value.Container.Name,
                _storageOptions.ContainerName.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The created data source points to the wrong container.");
        }

        var skillsetName = await CreateSkillsetAsync(indexName);

        var indexer = new SearchIndexer(
            indexerName,
            dataSourceName,
            indexName)
        {
            SkillsetName = skillsetName,
            Parameters = new IndexingParameters
            {
                IndexingParametersConfiguration =
                    new IndexingParametersConfiguration
                    {
                        ParsingMode = BlobIndexerParsingMode.Default,
                        DataToExtract = BlobIndexerDataToExtract.ContentAndMetadata
                        // ImageAction = BlobIndexerImageAction.GenerateNormalizedImages
                    }
            }
        };

        await _indexerClient.CreateOrUpdateIndexerAsync(indexer);
        return indexerName;
    }

    public async Task<IndexerExecutionResult> RunAndWaitForIndexerAsync(
        string indexName,
        CancellationToken cancellationToken = default)
    {
        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(TimeSpan.FromMinutes(30));
        var token = timeout.Token;

        var indexers = await _indexerClient.GetIndexersAsync(token);

        var indexer = indexers.Value.FirstOrDefault(x =>
            string.Equals(
                x.TargetIndexName,
                indexName,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                x.SkillsetName,
                $"{indexName}-skillset",
                StringComparison.OrdinalIgnoreCase));

        if (indexer is null)
            throw new InvalidOperationException(
                $"No chunking indexer found for '{indexName}'. " +
                "Create a new chunk index first.");

        var before = await _indexerClient.GetIndexerStatusAsync(
            indexer.Name, token);

        var previous = before.Value.LastResult;
        var previousStart = previous?.StartTime;

        var followExistingRun =
            previous?.Status == IndexerExecutionStatus.InProgress;

        if (!followExistingRun)
        {
            try
            {
                await _indexerClient.RunIndexerAsync(indexer.Name, token);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // An automatic/concurrent execution may have started.
                // Only accept the conflict if a run is actually in progress.
                var current = await _indexerClient.GetIndexerStatusAsync(
                    indexer.Name, token);

                if (current.Value.LastResult?.Status !=
                    IndexerExecutionStatus.InProgress)
                {
                    throw;
                }

                followExistingRun = true;
            }
        }

        while (true)
        {
            token.ThrowIfCancellationRequested();

            var status = await _indexerClient.GetIndexerStatusAsync(
                indexer.Name, token);

            var result = status.Value.LastResult;

            if (result is not null &&
                result.StartTime is not null &&
                result.Status != IndexerExecutionStatus.InProgress &&
                (followExistingRun || result.StartTime != previousStart))
            {
                return result;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), token);
        }
    }


    private void ValidateEnrichmentOptions()
    {
        //if (string.IsNullOrWhiteSpace(_searchOptions.CognitiveServicesKey))
        //    throw new InvalidOperationException(
        //        "Configure AzureSearch:CognitiveServicesKey before creating the pipeline.");

        if (_searchOptions.ChunkSize is < 300 or > 50000)
            throw new InvalidOperationException(
                "ChunkSize must be between 300 and 50000 characters.");

        if (_searchOptions.ChunkOverlap < 0 ||
            _searchOptions.ChunkOverlap > _searchOptions.ChunkSize / 2)
            throw new InvalidOperationException(
                "ChunkOverlap must be between zero and half of ChunkSize.");
    }

    private async Task<string> CreateSkillsetAsync(string indexName)
    {
        ValidateEnrichmentOptions();

        var skillsetName = $"{indexName}-skillset";

        //var ocrSkill = new OcrSkill(
        //    [
        //        new InputFieldMappingEntry("image")
        //        {
        //            Source = "/document/normalized_images/*"
        //        }
        //    ],
        //    [
        //        new OutputFieldMappingEntry("text")
        //        {
        //            TargetName = "ocr_text"
        //        }
        //    ])
        //{
        //    Name = "ocr-images",
        //    Context = "/document/normalized_images/*",
        //    DefaultLanguageCode = OcrSkillLanguage.En
        //};

        //var mergeSkill = new MergeSkill(
        //    [
        //        new InputFieldMappingEntry("text")
        //        {
        //            Source = "/document/content"
        //        },
        //        new InputFieldMappingEntry("itemsToInsert")
        //        {
        //            Source = "/document/normalized_images/*/ocr_text"
        //        },
        //        new InputFieldMappingEntry("offsets")
        //        {
        //            Source = "/document/normalized_images/*/contentOffset"
        //        }
        //    ],
        //    [
        //        new OutputFieldMappingEntry("mergedText")
        //        {
        //            TargetName = "merged_text"
        //        }
        //    ])
        //{
        //    Name = "merge-content-and-ocr",
        //    Context = "/document",
        //    InsertPreTag = " ",
        //    InsertPostTag = " "
        //};

        var splitSkill = new SplitSkill(
            [
                new InputFieldMappingEntry("text")
                {
                    Source = "/document/content"
                }
            ],
            [
                new OutputFieldMappingEntry("textItems")
                {
                    TargetName = "chunks"
                }
            ])
        {
            Name = "split-content",
            Context = "/document",
            TextSplitMode = TextSplitMode.Pages,
            DefaultLanguageCode = SplitSkillLanguage.En,
            MaximumPageLength = _searchOptions.ChunkSize,
            PageOverlapLength = _searchOptions.ChunkOverlap
        };

        var embeddingSkill = new AzureOpenAIEmbeddingSkill(
            [
                new InputFieldMappingEntry("text")
                {
                    Source = "/document/chunks/*"
                }
            ],
            [
                new OutputFieldMappingEntry("embedding")
                {
                    TargetName = "content_vector"
                }
            ])
        {
            Name = "embed-chunks",
            Context = "/document/chunks/*",
            ResourceUri = new Uri(_openAIOptions.Endpoint),
            ApiKey = _openAIOptions.ApiKey,
            DeploymentName = _openAIOptions.EmbeddingDeploymentName,
            ModelName = AzureOpenAIModelName.TextEmbedding3Small,
            Dimensions = _openAIOptions.EmbeddingDimensions
        };

        var selector = new SearchIndexerIndexProjectionSelector(
            indexName,
            "ParentId",
            "/document/chunks/*",
            [
                new InputFieldMappingEntry("Title")
                {
                    Source = "/document/metadata_storage_name"
                },
                new InputFieldMappingEntry("Content")
                {
                    Source = "/document/chunks/*"
                },
                new InputFieldMappingEntry("ContentVector")
                {
                    Source = "/document/chunks/*/content_vector"
                }
            ]);

        var skillset = new SearchIndexerSkillset(
            skillsetName,
            [
                // ocrSkill,
                // mergeSkill,
                splitSkill,
                embeddingSkill
            ])
        {
            //CognitiveServicesAccount =
            //    new CognitiveServicesAccountKey(
            //        _searchOptions.CognitiveServicesKey),

            IndexProjection = new SearchIndexerIndexProjection(
                [selector])
            {
                Parameters = new SearchIndexerIndexProjectionsParameters
                {
                    ProjectionMode =
                        IndexProjectionMode.SkipIndexingParentDocuments
                }
            }
        };

        await _indexerClient.CreateOrUpdateSkillsetAsync(skillset);

        return skillsetName;
    }
}