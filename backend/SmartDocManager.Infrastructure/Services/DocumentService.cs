using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SmartDocManager.Application.DTOs;
using SmartDocManager.Application.Interfaces;
using SmartDocManager.Domain.Entities;
using SmartDocManager.Infrastructure.Data;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SmartDocManager.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ApplicationDbContext context,
        IHostEnvironment environment,
        ILogger<DocumentService> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    public async Task<DocumentDto> UploadDocumentAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is empty or null", nameof(file));
        }

        var uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var documentId = Guid.NewGuid();
        var fileExtension = Path.GetExtension(file.FileName);
        var fileName = $"{documentId}{fileExtension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        string? extractedText = null;
        if (file.ContentType == "application/pdf" || fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                extractedText = await ExtractTextFromPdfAsync(filePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract text from PDF {FileName}", file.FileName);
            }
        }

        var document = new Document
        {
            Id = documentId,
            FileName = fileName,
            OriginalFileName = file.FileName,
            FilePath = filePath,
            FileSize = file.Length,
            ContentType = file.ContentType,
            UploadedAt = DateTime.UtcNow,
            ProcessedAt = extractedText != null ? DateTime.UtcNow : null,
            ExtractedText = extractedText
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync(cancellationToken);

        return new DocumentDto
        {
            Id = document.Id,
            FileName = document.FileName,
            OriginalFileName = document.OriginalFileName,
            FileSize = document.FileSize,
            ContentType = document.ContentType,
            UploadedAt = document.UploadedAt,
            ProcessedAt = document.ProcessedAt
        };
    }

    public async Task<DocumentDto?> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document == null)
            return null;

        return new DocumentDto
        {
            Id = document.Id,
            FileName = document.FileName,
            OriginalFileName = document.OriginalFileName,
            FileSize = document.FileSize,
            ContentType = document.ContentType,
            UploadedAt = document.UploadedAt,
            ProcessedAt = document.ProcessedAt
        };
    }

    public async Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _context.Documents
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        return documents.Select(d => new DocumentDto
        {
            Id = d.Id,
            FileName = d.FileName,
            OriginalFileName = d.OriginalFileName,
            FileSize = d.FileSize,
            ContentType = d.ContentType,
            UploadedAt = d.UploadedAt,
            ProcessedAt = d.ProcessedAt
        });
    }

    public async Task<bool> DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document == null)
            return false;

        if (File.Exists(document.FilePath))
        {
            File.Delete(document.FilePath);
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<string> ExtractTextFromPdfAsync(string filePath, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        
        using (var reader = new iText.Kernel.Pdf.PdfReader(filePath))
        using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(reader))
        {
            var numberOfPages = pdfDoc.GetNumberOfPages();
            
            for (int i = 1; i <= numberOfPages; i++)
            {
                var page = pdfDoc.GetPage(i);
                var pageText = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page);
                text.AppendLine(pageText);
            }
        }

        return await Task.FromResult(text.ToString());
    }
}
