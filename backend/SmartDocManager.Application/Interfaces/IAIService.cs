namespace SmartDocManager.Application.Interfaces;

public interface IAIService
{
    IAsyncEnumerable<string> ProcessQuestionAsync(
        string documentText,
        string question,
        CancellationToken cancellationToken = default);
        
    IAsyncEnumerable<string> ProcessQuestionWithRAGAsync(
        string context,
        string question,
        CancellationToken cancellationToken = default);
}