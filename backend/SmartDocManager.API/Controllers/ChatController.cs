using Microsoft.AspNetCore.Mvc;
using SmartDocManager.Application.DTOs;
using SmartDocManager.Application.Interfaces;
using SmartDocManager.Infrastructure.Services;
using System.Text;

namespace SmartDocManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IRAGService _ragService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        IRAGService ragService,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _ragService = ragService;
        _logger = logger;
    }

    [HttpPost("documents/{documentId}/ask")]
    public async Task<ActionResult<ChatResponseDto>> AskQuestion(
        Guid documentId,
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question cannot be empty");
        }

        try
        {
            var response = await _chatService.AskQuestionAsync(documentId, request.Question, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for document {DocumentId}", documentId);
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Document {DocumentId} not processed", documentId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing question for document {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while processing your question");
        }
    }

    [HttpPost("documents/{documentId}/ask-stream")]
    public async Task AskQuestionStream(
        Guid documentId,
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            if (!Response.HasStarted)
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Question cannot be empty", cancellationToken);
            }
            return;
        }

        try
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");

            try
            {
                await foreach (var chunk in _chatService.AskQuestionStreamAsync(documentId, request.Question, cancellationToken))
                {
                    // Format as SSE: encode chunk to base64 to handle any special characters
                    var base64Chunk = Convert.ToBase64String(Encoding.UTF8.GetBytes(chunk));
                    var data = $"data: {base64Chunk}\n\n";
                    var bytes = Encoding.UTF8.GetBytes(data);
                    await Response.Body.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Stream cancelled for document {DocumentId}", documentId);
                // Gracefully handle cancellation - just return without error
                return;
            }
            catch (Exception ex) when (Response.HasStarted)
            {
                // Error occurred during streaming - response has started, send SSE error message
                _logger.LogError(ex, "Error during streaming for document {DocumentId}", documentId);
                var errorMessage = "An error occurred while processing the stream";
                var errorData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { error = errorMessage })}\n\n";
                var errorBytes = Encoding.UTF8.GetBytes(errorData);
                await Response.Body.WriteAsync(errorBytes, 0, errorBytes.Length, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
                return; // Don't re-throw, error already sent
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for document {DocumentId}", documentId);
            
            if (!Response.HasStarted)
            {
                Response.StatusCode = 404;
            }
            
            // Send error as SSE message (JSON, not base64, so error is readable)
            var errorData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message })}\n\n";
            var errorBytes = Encoding.UTF8.GetBytes(errorData);
            await Response.Body.WriteAsync(errorBytes, 0, errorBytes.Length, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Document {DocumentId} not processed", documentId);
            
            if (!Response.HasStarted)
            {
                Response.StatusCode = 400;
            }
            
            // Send error as SSE message (JSON, not base64, so error is readable)
            var errorData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message })}\n\n";
            var errorBytes = Encoding.UTF8.GetBytes(errorData);
            await Response.Body.WriteAsync(errorBytes, 0, errorBytes.Length, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for document {DocumentId}", documentId);
            // Don't send error, just close the stream
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing question for document {DocumentId}", documentId);
            
            if (!Response.HasStarted)
            {
                Response.StatusCode = 500;
            }
            
            // Send error as SSE message (JSON, not base64, so error is readable)
            var errorMessage = "An error occurred while processing your question";
            var errorData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { error = errorMessage })}\n\n";
            var errorBytes = Encoding.UTF8.GetBytes(errorData);
            await Response.Body.WriteAsync(errorBytes, 0, errorBytes.Length, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
