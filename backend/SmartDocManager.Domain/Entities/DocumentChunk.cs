namespace SmartDocManager.Domain.Entities;

public class DocumentChunk
{
    public string Id { get; set; } = string.Empty;
    public int DocumentId { get; set; }
    public string Text { get; set; } = string.Empty;
    public ReadOnlyMemory<float> Vector { get; set; }
}
