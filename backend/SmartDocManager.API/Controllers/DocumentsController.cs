using Microsoft.AspNetCore.Mvc;
using SmartDocManager.Application.DTOs;
using SmartDocManager.Application.Interfaces;

namespace SmartDocManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentService documentService,
        ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<DocumentDto>> UploadDocument(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        try
        {
            var document = await _documentService.UploadDocumentAsync(file, cancellationToken);
            return Ok(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            return StatusCode(500, "An error occurred while uploading the document");
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentDto>>> GetAllDocuments(CancellationToken cancellationToken)
    {
        try
        {
            var documents = await _documentService.GetAllDocumentsAsync(cancellationToken);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents");
            return StatusCode(500, "An error occurred while retrieving documents");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentDto>> GetDocument(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var document = await _documentService.GetDocumentByIdAsync(id, cancellationToken);
            if (document == null)
            {
                return NotFound();
            }
            return Ok(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document {DocumentId}", id);
            return StatusCode(500, "An error occurred while retrieving the document");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _documentService.DeleteDocumentAsync(id, cancellationToken);
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId}", id);
            return StatusCode(500, "An error occurred while deleting the document");
        }
    }
}
