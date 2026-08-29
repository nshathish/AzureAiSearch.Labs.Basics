# Azure AI Search Labs — Phases 1 and 2

This solution is a hands-on .NET Blazor tutorial for building a document-search application with Azure Blob Storage and Azure AI Search.

So far, the application can:

- upload documents to Azure Blob Storage;
- create an Azure AI Search index, data source, skillset, and indexer;
- extract and split document text into overlapping chunks;
- project those chunks into a searchable index;
- run semantic search over the indexed chunks; and
- show both the keyword relevance score and semantic reranker score.

> **Current scope:** Phases 1 and 2 implement text chunking and semantic search. Vector embeddings, hybrid vector search, and RAG answer generation are shown in the roadmap diagram, but are not implemented yet.

## Architecture at a glance

The Blazor application coordinates two main workflows: document ingestion and search.

![Application workflow showing the Blazor app, Blob Storage, Azure AI Search administration, indexing, and search](<Azure AI Search Basic Indexing-2026-08-26-065157.png>)

When a user uploads a document, the application stores it in Blob Storage. An Azure AI Search indexer reads the blob, extracts its text, passes the text through a skillset, and writes searchable chunks to the index. The Search page then queries that index.

## What we built

### Phase 1 — Blob indexing and chunk-based search

Phase 1 established the complete ingestion pipeline.

![Azure AI Search indexing pipeline from Blob Storage through a text split skill to chunk documents](<Azure AI Search Indexing-2026-08-26-065226.png>)

The workflow is:

1. The user selects a document on the Blazor **Documents** page.
2. `BlobStorageService` uploads it to the configured Blob Storage container.
3. The application creates an Azure AI Search index with these fields:
   - `ChunkId` — unique key for each chunk;
   - `ParentId` — links a chunk to its source document;
   - `Title` — the original blob filename; and
   - `Content` — the searchable chunk text.
4. `AzureSearchAdminService` creates a Blob data source.
5. It creates a skillset containing an Azure AI Search `SplitSkill`.
6. The split skill divides extracted text into overlapping chunks.
7. An index projection maps each chunk into its own search document.
8. The indexer runs and the UI reports processed items, failures, and warnings.

The text-splitting step solves an important limitation of basic blob indexing: without a skillset, the indexer extracts a whole document but does not create smaller, independently retrievable passages.

![Comparison showing the missing skillset and text split skill in a basic blob indexing workflow](<Blob Indexing Workflow-2026-08-26-065239.png>)

Chunking is configured with:

```json
{
  "ChunkSize": 2000,
  "ChunkOverlap": 200
}
```

- `ChunkSize` controls the maximum page length passed to the split skill.
- `ChunkOverlap` repeats a small amount of text between adjacent chunks so relevant context is less likely to be cut at a boundary.
- Parent documents are skipped during projection, so search results contain chunks rather than duplicate parent-and-child records.

### Phase 2 — Semantic search and relevance scores

Phase 2 builds on the same chunk index by enabling Azure AI Search semantic ranking.

When an index is created, the application adds a semantic configuration:

- `Title` is the prioritized title field;
- `Content` is the prioritized content field; and
- the configured semantic configuration is set as the index default.

The Search page sends a semantic query, requests the five best matches, and limits returned data to the fields used by the UI. For every result it displays:

- **Keyword score** — the initial text-search relevance score; and
- **Semantic score** — the score produced when the semantic ranker reranks the initial results.

This makes it easier to compare classic lexical matching with semantic relevance while learning how the ranking pipeline behaves.

## Solution structure

```text
AiSearch.Labs.Basics/
├── AiSearch.Labs.Basics.slnx
├── README.md
├── *.png                              Architecture diagrams
└── AiSearch.Labs.Basics/
    ├── Components/Pages/
    │   ├── Upload.razor               Upload and indexing workflow
    │   └── Search.razor               Semantic search UI
    ├── Configuration/
    │   ├── AzureSearchOptions.cs
    │   └── AzureStorageOptions.cs
    ├── Models/
    │   ├── SearchDocument.cs          Search index schema
    │   └── SearchResponses.cs         UI response models
    ├── Services/
    │   ├── BlobStorageService.cs      Blob upload and listing
    │   ├── AzureSearchAdminService.cs Index/data source/skillset/indexer
    │   └── AzureAiSearchService.cs    Semantic query execution
    └── Styles/                        Tailwind CSS source
```

## Prerequisites

Before running the lab, install or create:

- the .NET SDK required by the project (`net10.0` at the time of writing);
- Node.js and npm, used to compile Tailwind CSS;
- an Azure Storage account with a blob container;
- an Azure AI Search service that supports semantic ranking; and
- an Azure AI Search admin key for this learning project.

> **Security:** Do not commit connection strings or API keys. The project has a `UserSecretsId`, so local .NET user secrets are the simplest development option. If credentials have previously been committed or shared, rotate them in Azure.

## Configure the application

From the inner project directory:

```powershell
cd .\AiSearch.Labs.Basics
```

Store local credentials with .NET user secrets:

```powershell
dotnet user-secrets set "AzureStorage:ConnectionString" "<your-storage-connection-string>"
dotnet user-secrets set "AzureStorage:ContainerName" "rag-documents"
dotnet user-secrets set "AzureSearch:Endpoint" "https://<service-name>.search.windows.net"
dotnet user-secrets set "AzureSearch:ApiKey" "<your-search-admin-key>"
dotnet user-secrets set "AzureSearch:SemanticConfigurationName" "semantic-config"
dotnet user-secrets set "AzureSearch:ChunkSize" "2000"
dotnet user-secrets set "AzureSearch:ChunkOverlap" "200"
```

The semantic configuration name must not be blank. An empty value results in this Azure AI Search validation error:

```text
semantic.configurations[0].name : The name field is required
```

The application settings bind to `AzureStorageOptions` and `AzureSearchOptions` in `Program.cs`, then inject those values into the storage and search services.

## Build and run

Install the UI dependencies and compile Tailwind CSS:

```powershell
npm install
npm run build:css
```

Build and start the application:

```powershell
dotnet build
dotnet run
```

Open the local URL printed by `dotnet run`.

During UI development, keep Tailwind watching for Razor class changes:

```powershell
npm run watch:css
```

## Tutorial: ingest your first document

1. Open **Documents** from the application navigation.
2. Select a supported document of no more than 25 MB.
3. Choose **Upload to Blob Storage**.
4. Enter a lowercase Azure AI Search index name, for example `lab-documents`.
5. Choose **Create Index**.
6. The application creates the index, Blob data source, split skillset, and indexer.
7. Confirm the new index is selected.
8. Choose **Start Indexer**.
9. Wait for the status message to report completion and review any warnings or failures.

Azure AI Search performs the document text extraction. The custom skillset then splits that extracted content and projects the chunks into the target index.

## Tutorial: run a semantic search

1. Open **Search** from the application navigation.
2. Select the index created in the previous section.
3. Enter a natural-language query related to the uploaded document.
4. Submit the search.
5. Inspect the returned chunk previews.
6. Compare each result's keyword score with its semantic score.
7. Expand **View full chunk** or **Technical details** to inspect the content, chunk ID, and parent ID.

Semantic search is a reranking step: Azure AI Search first retrieves candidate documents using text search, then the semantic ranker reorders the strongest candidates according to their meaning and context.

## Naming created Azure resources

For an index named `lab-documents`, the application creates:

| Resource | Generated name |
|---|---|
| Search index | `lab-documents` |
| Blob data source | `lab-documents-blob-datasource` |
| Skillset | `lab-documents-skillset` |
| Indexer | `lab-documents-indexer` |
| Semantic configuration | Value of `AzureSearch:SemanticConfigurationName` |

Keeping these names related makes the resources easier to locate in the Azure portal while troubleshooting.

## Troubleshooting

### Semantic configuration name is required

Make sure `AzureSearch:SemanticConfigurationName` resolves to a non-empty value, such as `semantic-config`. Remember that an empty value in `appsettings.Development.json`, an environment variable, or user secrets overrides the default value in `AzureSearchOptions`.

### No chunking indexer found

Create a new index from the application before starting the indexer. The application looks for an indexer that targets the selected index and uses `<index-name>-skillset`.

### New Tailwind styles do not appear

Run `npm run build:css`, or leave `npm run watch:css` active. The compiled file in `wwwroot/css/app.css` is what the browser receives.

### Indexer completes with warnings or failures

Read the status shown in the Documents page, then inspect the indexer execution history in the Azure portal. Common causes include unsupported or protected files, an incorrect container name, invalid storage credentials, or a field mapping mismatch.

## Roadmap — Phase 3 and beyond

The following diagram captures the intended evolution toward vector search and retrieval-augmented generation (RAG):

![Roadmap showing document ingestion, text splitting, embeddings, hybrid search, semantic ranking, and Azure OpenAI grounded answers](<Blob Indexing Workflow-2026-08-26-065348.png>)

Likely next steps are:

1. add a vector field to the index schema;
2. configure an embedding skill or generate embeddings in application code;
3. configure an HNSW vector search profile;
4. combine text and vector queries as hybrid search;
5. retain semantic reranking over the hybrid candidates; and
6. pass the most relevant chunks to a chat model to generate a grounded answer with citations.

These features are intentionally separated from Phases 1 and 2 so each stage can be understood and tested independently.
