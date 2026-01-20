using Microsoft.EntityFrameworkCore;
using SmartDocManager.Application.DTOs;
using SmartDocManager.Application.Interfaces;
using SmartDocManager.Domain.Entities;
using SmartDocManager.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace SmartDocManager.Infrastructure.Services;

public class ChatService : IChatService
{
    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly IRAGService _ragService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ApplicationDbContext context,
        IAIService aiService,
        IRAGService ragService,
        ILogger<ChatService> logger)
    {
        _context = context;
        _aiService = aiService;
        _ragService = ragService;
        _logger = logger;
    }

    public async Task<ChatResponseDto> AskQuestionAsync(Guid documentId, string question, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        var fullResponse = string.Empty;

        if (document == null)
        {
            throw new ArgumentException($"Document with id {documentId} not found", nameof(documentId));
        }

        // Use RAG to get relevant chunks instead of full document text
        var relevantChunks = await _ragService.GetRelevantChunksAsync(question, 3);
        
        if (!relevantChunks.Any())
        {
            throw new InvalidOperationException("No relevant content found in the document. The document may not have been processed for RAG yet.");
        }

        // Combine the relevant chunks into context
        var context = string.Join("\n\n", relevantChunks.Select(c => c.Text));

        await foreach (var chunk in _aiService.ProcessQuestionWithRAGAsync(context, question, cancellationToken))
        {
            fullResponse += chunk;
        }

        var chatMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            Message = question,
            Response = fullResponse,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync(cancellationToken);

        return new ChatResponseDto
        {
            Response = fullResponse,
            CreatedAt = chatMessage.CreatedAt
        };
    }

    public async IAsyncEnumerable<string> AskQuestionStreamAsync(
        Guid documentId,
        string question,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            throw new ArgumentException($"Document with id {documentId} not found", nameof(documentId));
        }

        // Use RAG to get relevant chunks instead of full document text
        var relevantChunks = await _ragService.GetRelevantChunksAsync(question, 3);
        
        if (!relevantChunks.Any())
        {
            throw new InvalidOperationException("No relevant content found in the document. The document may not have been processed for RAG yet.");
        }

        // Combine the relevant chunks into context
        var context = string.Join("\n\n", relevantChunks.Select(c => c.Text));

        await foreach (var chunk in _aiService.ProcessQuestionWithRAGAsync(context, question, cancellationToken))
        {
            yield return chunk;
        }
    }
}
