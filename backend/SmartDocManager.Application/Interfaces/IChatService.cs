using SmartDocManager.Application.DTOs;

namespace SmartDocManager.Application.Interfaces;

public interface IChatService
{
    Task<ChatResponseDto> AskQuestionAsync(Guid documentId, string question, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> AskQuestionStreamAsync(Guid documentId, string question, CancellationToken cancellationToken = default);
}
