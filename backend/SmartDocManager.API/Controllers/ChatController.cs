using Microsoft.AspNetCore.Mvc;
using SmartDocManager.Application.DTOs;
using SmartDocManager.Application.Interfaces;
using System.Text;

namespace SmartDocManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
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
            Response.StatusCode = 400;
            await Response.WriteAsync("Question cannot be empty", cancellationToken);
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
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for document {DocumentId}", documentId);
            Response.StatusCode = 404;
            var errorData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message })}\n\n";
            var errorBytes = Encoding.UTF8.GetBytes(errorData);
            await Response.Body.WriteAsync(errorBytes, 0, errorBytes.Length, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Document {DocumentId} not processed", documentId);
            Response.StatusCode = 400;
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
            Response.StatusCode = 500;
            var errorData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { error = "An error occurred while processing your question" })}\n\n";
            var errorBytes = Encoding.UTF8.GetBytes(errorData);
            await Response.Body.WriteAsync(errorBytes, 0, errorBytes.Length, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
