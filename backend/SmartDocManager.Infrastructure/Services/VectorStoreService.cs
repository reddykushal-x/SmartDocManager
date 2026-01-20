using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using SmartDocManager.Domain.Entities;
using System.Data.SQLite;
using System.Numerics.Tensors;

namespace SmartDocManager.Infrastructure.Services;

public interface IVectorStoreService
{
    Task InitializeVectorStoreAsync();
    Task<IEnumerable<DocumentChunk>> SearchAsync(float[] queryVector, int topResults = 5);
    Task AddDocumentChunkAsync(DocumentChunk chunk);
    Task AddDocumentChunksAsync(IEnumerable<DocumentChunk> chunks);
    Task<IEnumerable<DocumentChunk>> GetAllAsync();
    Task DeleteChunksByDocumentIdAsync(Guid documentId);
}

public class VectorStoreService : IVectorStoreService
{
    private readonly ILogger<VectorStoreService> _logger;
    private readonly string _connectionString;
    public VectorStoreService(ILogger<VectorStoreService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Data Source=SmartDocManager.db";
    }

    public async Task InitializeVectorStoreAsync()
    {
        try
        {
            _logger.LogInformation("Initializing SQLite Vector Store...");

            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            // 1. Create the table manually using standard SQL
            // This makes us independent of the problematic 'IVectorStore' interface
            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS DocumentChunks (
                Id TEXT PRIMARY KEY,
                DocumentId TEXT NOT NULL,
                Text TEXT NOT NULL,
                Vector BLOB NOT NULL
            );";
            await createTableCommand.ExecuteNonQueryAsync();

            // DELETE OR COMMENT OUT THE LINE BELOW (This is causing your error)
            // _vectorStore = new SqliteVectorStore(connection); 

            _logger.LogInformation("Vector table 'DocumentChunks' is ready.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Vector Store.");
            throw;
        }
    }

    public async Task<IEnumerable<DocumentChunk>> SearchAsync(float[] queryVector, int topResults = 5)
    {
        try
        {
            // Fetch all DocumentChunk records from the database
            var allChunks = await GetAllAsync();
            var chunksList = allChunks.ToList();

            // Handle empty database case
            if (chunksList.Count == 0)
            {
                _logger.LogInformation("No document chunks found in database. Returning empty results.");
                return Enumerable.Empty<DocumentChunk>();
            }

            // Convert queryVector to ReadOnlySpan<float> for similarity calculation
            ReadOnlySpan<float> querySpan = queryVector;

            // Store chunks with their similarity scores
            var chunksWithSimilarity = new List<(DocumentChunk Chunk, float Similarity)>();

            // Calculate cosine similarity for each chunk using foreach loop
            foreach (var chunk in chunksList)
            {
                // Access the Span from ReadOnlyMemory<float> for calculation
                ReadOnlySpan<float> chunkSpan = chunk.Vector.Span;
                
                // Use TensorPrimitives.CosineSimilarity to calculate similarity
                // Note: Both spans must have the same length
                float similarity = 0f;
                if (chunkSpan.Length == querySpan.Length && chunkSpan.Length > 0)
                {
                    similarity = TensorPrimitives.CosineSimilarity(querySpan, chunkSpan);
                }

                chunksWithSimilarity.Add((chunk, similarity));
            }

            // Sort by similarity descending and take top results
            chunksWithSimilarity.Sort((x, y) => y.Similarity.CompareTo(x.Similarity));
            var topResultsList = chunksWithSimilarity
                .Take(topResults)
                .Select(x => x.Chunk)
                .ToList();

            // Extract top similarity score for logging
            float topSimilarity = chunksWithSimilarity.Count > 0 
                ? chunksWithSimilarity[0].Similarity 
                : 0f;

            _logger.LogInformation(
                "Searched {TotalChunks} document chunks. Top similarity score: {TopSimilarity:F4}. Returning top {TopResults} results.",
                chunksList.Count,
                topSimilarity,
                topResultsList.Count);

            return topResultsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search document chunks in vector store");
            throw;
        }
    }

    public async Task AddDocumentChunkAsync(DocumentChunk chunk)
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();
            
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT OR REPLACE INTO DocumentChunks (Id, DocumentId, Text, Vector)
                VALUES (@id, @documentId, @text, @vector)";
            
            insertCommand.Parameters.AddWithValue("@id", chunk.Id);
            insertCommand.Parameters.AddWithValue("@documentId", chunk.DocumentId);
            insertCommand.Parameters.AddWithValue("@text", chunk.Text);
            
            // Convert float array to bytes for storage
            var vectorBytes = new byte[chunk.Vector.Length * sizeof(float)];
            Buffer.BlockCopy(chunk.Vector.ToArray(), 0, vectorBytes, 0, vectorBytes.Length);
            insertCommand.Parameters.AddWithValue("@vector", vectorBytes);
            
            await insertCommand.ExecuteNonQueryAsync();
            
            _logger.LogInformation($"Added document chunk {chunk.Id} to vector store");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add document chunk to vector store");
            throw;
        }
    }

    public async Task AddDocumentChunksAsync(IEnumerable<DocumentChunk> chunks)
    {
        try
        {
            var chunkList = chunks.ToList();
            foreach (var chunk in chunkList)
            {
                await AddDocumentChunkAsync(chunk);
            }
            
            _logger.LogInformation($"Added {chunkList.Count} document chunks to vector store");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add document chunks to vector store");
            throw;
        }
    }
    public async Task<IEnumerable<DocumentChunk>> GetAllAsync()
    {
        var chunks = new List<DocumentChunk>();
        using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new SQLiteCommand("SELECT Id, DocumentId, Text, Vector FROM DocumentChunks", connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var vectorBytes = (byte[])reader["Vector"];
            var floatArray = new float[vectorBytes.Length / sizeof(float)];
            Buffer.BlockCopy(vectorBytes, 0, floatArray, 0, vectorBytes.Length);

            chunks.Add(new DocumentChunk
            {
                Id = reader["Id"].ToString(),
                DocumentId = Convert.ToInt32(reader["DocumentId"]),
                Text = reader["Text"].ToString(),
                Vector = floatArray
            });
        }
        return chunks;
    }

    public async Task DeleteChunksByDocumentIdAsync(Guid documentId)
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            // We use the hashcode because that's how you stored it in your chunking logic
            command.CommandText = "DELETE FROM DocumentChunks WHERE DocumentId = @docId";
            command.Parameters.AddWithValue("@docId", documentId.GetHashCode());

            int rowsAffected = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Deleted {Count} chunks from vector store for document {Id}", rowsAffected, documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chunks for document {Id}", documentId);
            throw;
        }
    }
}