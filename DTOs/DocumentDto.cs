namespace StorageClient.Redesign.DTOs;

public sealed class DocumentDto
{
    public int Id { get; set; }
    public int TypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Filename { get; set; } = string.Empty;
}
