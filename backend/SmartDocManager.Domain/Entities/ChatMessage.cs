namespace SmartDocManager.Domain.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Document? Document { get; set; }
}
