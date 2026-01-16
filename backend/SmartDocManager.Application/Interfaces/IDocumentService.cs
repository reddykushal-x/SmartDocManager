using SmartDocManager.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace SmartDocManager.Application.Interfaces;

public interface IDocumentService
{
    Task<DocumentDto> UploadDocumentAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<DocumentDto?> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default);
}
