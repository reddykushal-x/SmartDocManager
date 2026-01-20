using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Text;
using SmartDocManager.Domain.Entities;
using SmartDocManager.Application.Interfaces;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmartDocManager.Infrastructure.Services;

public interface IRAGService
{
    Task ProcessDocumentAsync(Guid documentId, string extractedText);
    Task<IEnumerable<DocumentChunk>> GetRelevantChunksAsync(string query, int topResults = 5);
}

public class RAGService : IRAGService
{
    private readonly ILogger<RAGService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly HttpClient _httpClient;

    public RAGService(
        ILogger<RAGService> logger,
        IConfiguration configuration,
        IVectorStoreService vectorStoreService,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _vectorStoreService = vectorStoreService;
        _httpClient = httpClient;
    }

    public async Task ProcessDocumentAsync(Guid documentId, string extractedText)
    {
        try
        {
            _logger.LogInformation($"Processing document {documentId} for RAG indexing");

            // 1. Split text into chunks using Semantic Kernel's TextChunker
            var chunks = await CreateChunksAsync(extractedText, documentId);
            
            // 2. Generate embeddings for each chunk
            var chunksWithEmbeddings = await GenerateEmbeddingsAsync(chunks);
            
            // 3. Save chunks to vector store
            await _vectorStoreService.AddDocumentChunksAsync(chunksWithEmbeddings);
            
            _logger.LogInformation($"Successfully processed {chunksWithEmbeddings.Count()} chunks for document {documentId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to process document {documentId} for RAG indexing");
            throw;
        }
    }

    private List<string> FastManualTextChunker(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        ReadOnlySpan<char> textSpan = text.AsSpan();
        int start = 0;

        while (start < textSpan.Length)
        {
            int end = Math.Min(start + chunkSize, textSpan.Length);

            // Try to break at the last space within the window to avoid cutting words
            if (end < textSpan.Length)
            {
                // Slice the current window and find the last space
                int lastSpace = textSpan.Slice(start, end - start).LastIndexOf(' ');

                // If a space was found, adjust 'end' to that position
                if (lastSpace > 0)
                {
                    end = start + lastSpace;
                }
            }

            // Only create the string object ONCE per chunk here
            chunks.Add(textSpan.Slice(start, end - start).Trim().ToString());

            // Move the pointer forward, accounting for overlap
            start = end - overlap;

            // Prevent infinite loops if overlap is larger than chunk
            if (start >= textSpan.Length || end >= textSpan.Length) break;
            if (start < 0) start = 0;
        }

        return chunks;
    }

    private async Task<List<DocumentChunk>> CreateChunksAsync(string text, Guid documentId)
    {
        // 1. Pre-calculate expected number of chunks to avoid List resizing
        int estimatedCount = (text.Length / 800) + 5;
        var chunks = new List<DocumentChunk>(estimatedCount);

        // 2. Optimized chunking using spans
        var textChunks = FastManualTextChunker(text, 1000, 200);

        // 3. DocumentId Hash (Consider if you really need an int or can use Guid)
        int docIdHash = documentId.GetHashCode();

        for (int i = 0; i < textChunks.Count; i++)
        {
            chunks.Add(new DocumentChunk
            {
                // Use simple string concatenation or a reusable builder for IDs
                Id = documentId.ToString() + "_" + i,
                DocumentId = docIdHash,
                Text = textChunks[i],
                Vector = ReadOnlyMemory<float>.Empty
            });
        }

        _logger.LogInformation($"Created {chunks.Count} chunks for document {documentId}");
        return chunks;
    }

    private async Task<List<DocumentChunk>> GenerateEmbeddingsAsync(List<DocumentChunk> chunks)
    {
        // Using a free/local embedding solution
        // Option 1: OpenAI text-embedding-3-small (very cost-effective at ~$0.02 per 1M tokens)
        // Option 2: Hugging Face's sentence-transformers via local API
        // Option 3: Ollama with a local embedding model
        
        var embeddingProvider = _configuration["RAG:EmbeddingProvider"]?.ToLower() ?? "openai";
        
        switch (embeddingProvider)
        {
            case "openai":
                return await GenerateOpenAIEmbeddingsAsync(chunks);
            case "huggingface":
                return await GenerateHuggingFaceEmbeddingsAsync(chunks);
            case "ollama":
                return await GenerateOllamaEmbeddingsAsync(chunks);
            default:
                throw new NotSupportedException($"Embedding provider '{embeddingProvider}' is not supported");
        }
    }

    private async Task<List<DocumentChunk>> GenerateOpenAIEmbeddingsAsync(List<DocumentChunk> chunks)
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        var model = _configuration["RAG:EmbeddingModel"] ?? "text-embedding-3-small";
        
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured");
        }

        var processedChunks = new List<DocumentChunk>();
        
        // Process in batches to avoid rate limits
        const int batchSize = 100;
        for (int i = 0; i < chunks.Count; i += batchSize)
        {
            var batch = chunks.Skip(i).Take(batchSize).ToList();
            var texts = batch.Select(c => c.Text).ToArray();
            
            try
            {
                var embeddings = await CallOpenAIEmbeddingAsync(texts, apiKey, model);
                
                for (int j = 0; j < batch.Count; j++)
                {
                    batch[j].Vector = new ReadOnlyMemory<float>(embeddings[j]);
                    processedChunks.Add(batch[j]);
                }
                
                // Add delay to avoid rate limits
                if (i + batchSize < chunks.Count)
                {
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to generate embeddings for batch starting at index {i}");
                throw;
            }
        }
        
        return processedChunks;
    }

    private async Task<float[][]> CallHuggingFaceEmbeddingAsync(string[] texts, string apiKey, string model)
    {
        // Send entire texts array as inputs in a single batched request
        var requestPayload = new { inputs = texts };
        var json = JsonSerializer.Serialize(requestPayload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        // This specific URL format forces the embedding (feature-extraction) logic
        var endpoint = $"https://router.huggingface.co/hf-inference/models/{model}/pipeline/feature-extraction";

        var response = await _httpClient.PostAsync(endpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError($"HF Detail: {errorBody}");
            throw new HttpRequestException($"HF API Error: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();

        // Batched response should return float[][] (array of embeddings)
        try
        {
            // Primary: Expect batched response as float[][]
            var batchedEmbeddings = JsonSerializer.Deserialize<float[][]>(responseJson);
            if (batchedEmbeddings != null && batchedEmbeddings.Length > 0)
            {
                return batchedEmbeddings;
            }
        }
        catch (JsonException)
        {
            // Fallback: If API returns a different format, try to handle it
        }

        // Fallback: Try to deserialize as a single array (unlikely for batched, but handle edge cases)
        try
        {
            var singleVector = JsonSerializer.Deserialize<float[]>(responseJson);
            if (singleVector != null)
            {
                // If only one vector returned for multiple inputs, return it wrapped in array
                return new[] { singleVector };
            }
        }
        catch (JsonException)
        {
            // Last fallback: Try nested format [[...]]
        }

        // If all deserialization attempts fail, throw with context
        throw new InvalidOperationException(
            $"Failed to deserialize Hugging Face embedding response. Expected float[][] for batched input, but got: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}");
    }
    private async Task<float[][]> CallOpenAIEmbeddingAsync(string[] texts, string apiKey, string model)
    {
        var requestPayload = new
        {
            input = texts,
            model = model,
            encoding_format = "float"
        };

        var json = JsonSerializer.Serialize(requestPayload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Clear existing headers and set OpenAI authorization
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _httpClient.PostAsync("https://api.openai.com/v1/embeddings", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"OpenAI API error: {response.StatusCode} - {error}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<OpenAIEmbeddingResponse>(responseJson);
        
        return result?.Data?.Select(d => d.Embedding).ToArray() ?? throw new InvalidOperationException("Invalid response from OpenAI API");
    }

    private async Task<List<DocumentChunk>> GenerateHuggingFaceEmbeddingsAsync(List<DocumentChunk> chunks)
    {
        var apiKey = _configuration["HuggingFace:ApiKey"];
        var model = _configuration["RAG:EmbeddingModel"] ?? "sentence-transformers/all-MiniLM-L6-v2";
        
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Hugging Face API key is not configured");
        }

        var processedChunks = new List<DocumentChunk>();
        
        // Process in batches to avoid rate limits
        const int batchSize = 100;
        for (int i = 0; i < chunks.Count; i += batchSize)
        {
            var batch = chunks.Skip(i).Take(batchSize).ToList();
            var texts = batch.Select(c => c.Text).ToArray();
            
            try
            {
                var embeddings = await CallHuggingFaceEmbeddingAsync(texts, apiKey, model);
                
                for (int j = 0; j < batch.Count; j++)
                {
                    batch[j].Vector = new ReadOnlyMemory<float>(embeddings[j]);
                    processedChunks.Add(batch[j]);
                }
                
                // Add delay to avoid rate limits
                if (i + batchSize < chunks.Count)
                {
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to generate Hugging Face embeddings for batch starting at index {i}");
                throw;
            }
        }
        
        return processedChunks;
    }

    private async Task<List<DocumentChunk>> GenerateOllamaEmbeddingsAsync(List<DocumentChunk> chunks)
    {
        // Implementation for Ollama local embedding models
        // This would require Ollama to be running locally with an embedding model
        throw new NotImplementedException("Ollama embedding generation not implemented yet");
    }

    public async Task<IEnumerable<DocumentChunk>> GetRelevantChunksAsync(string query, int topResults = 5)
    {
        try
        {
            // Generate embedding for the query
            var queryEmbedding = await GenerateQueryEmbeddingAsync(query);
            var allRecords = await _vectorStoreService.GetAllAsync(); // If your service supports it
            _logger.LogInformation($"Total vectors in store: {allRecords.Count()}");

            // Search for similar chunks
            var relevantChunks = await _vectorStoreService.SearchAsync(queryEmbedding, topResults);
            
            _logger.LogInformation($"Found {relevantChunks.Count()} relevant chunks for query");
            return relevantChunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve relevant chunks");
            throw;
        }
    }

    private async Task<float[]> GenerateQueryEmbeddingAsync(string query)
    {
        var embeddingProvider = _configuration["RAG:EmbeddingProvider"]?.ToLower() ?? "openai";
        
        switch (embeddingProvider)
        {
            case "openai":
                var embeddings = await CallOpenAIEmbeddingAsync(new[] { query }, 
                    _configuration["OpenAI:ApiKey"], 
                    _configuration["RAG:EmbeddingModel"] ?? "text-embedding-3-small");
                return embeddings[0];
            case "huggingface":
                var hfEmbeddings = await CallHuggingFaceEmbeddingAsync(new[] { query },
                    _configuration["HuggingFace:ApiKey"],
                    _configuration["RAG:EmbeddingModel"] ?? "sentence-transformers/all-MiniLM-L6-v2");
                return hfEmbeddings[0];
            default:
                throw new NotSupportedException($"Embedding provider '{embeddingProvider}' is not supported");
        }
    }
}

// Helper classes for OpenAI API response
public class OpenAIEmbeddingResponse
{
    public List<OpenAIEmbeddingData> Data { get; set; } = new();
}

public class OpenAIEmbeddingData
{
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

// Helper classes for Hugging Face API response
public class HuggingFaceEmbeddingResponse
{
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
