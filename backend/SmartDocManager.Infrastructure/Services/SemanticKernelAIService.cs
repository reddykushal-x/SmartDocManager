using Microsoft.SemanticKernel;
using SmartDocManager.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace SmartDocManager.Infrastructure.Services;

public class SemanticKernelAIService : IAIService
{
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticKernelAIService> _logger;

    public SemanticKernelAIService(Kernel kernel, ILogger<SemanticKernelAIService> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> ProcessQuestionAsync(
        string documentText,
        string question,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = $@"You are a helpful assistant that answers questions based on the provided document content.

Document Content:
{documentText}

Question: {question}

Please provide a clear and concise answer based on the document content above. If the answer cannot be found in the document, please say so.";

        // Use InvokePromptStreamingAsync instead of InvokePromptAsync
        var stream = _kernel.InvokePromptStreamingAsync(prompt, cancellationToken: cancellationToken);

        await foreach (var chunk in stream.WithCancellation(cancellationToken))
        {
            // Each chunk is a 'StreamingKernelContent' object; convert it to string
            var content = chunk.ToString();
            if (!string.IsNullOrEmpty(content))
            {
                yield return content;
            }
        }
    }
}