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
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ApplicationDbContext context,
        IAIService aiService,
        ILogger<ChatService> logger)
    {
        _context = context;
        _aiService = aiService;
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

        if (string.IsNullOrWhiteSpace(document.ExtractedText))
        {
            throw new InvalidOperationException("Document text has not been extracted yet");
        }

        await foreach (var chunk in _aiService.ProcessQuestionAsync(document.ExtractedText, question, cancellationToken))
        {
            fullResponse += chunk;
        }

        var chatMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            Message = question,
            Response = fullResponse, // Use the collected string here
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

        if (string.IsNullOrWhiteSpace(document.ExtractedText))
        {
            throw new InvalidOperationException("Document text has not been extracted yet");
        }

        await foreach (var chunk in _aiService.ProcessQuestionAsync(document.ExtractedText, question, cancellationToken))
        {
            yield return chunk;
        }
    }
}
