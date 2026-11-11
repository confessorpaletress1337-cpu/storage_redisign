namespace StorageClient.Redesign.DTOs;

public sealed class DocumentTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
