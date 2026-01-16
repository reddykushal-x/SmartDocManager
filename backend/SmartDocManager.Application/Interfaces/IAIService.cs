namespace SmartDocManager.Application.Interfaces;

public interface IAIService
{
    IAsyncEnumerable<string> ProcessQuestionAsync(
        string documentText,
        string question,
        CancellationToken cancellationToken = default);
}