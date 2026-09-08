namespace StorageClient.Redesign.DTOs;

public sealed class CurrentUserDto
{
    public int UserId { get; set; }
    public string? Username { get; set; }
    public string? Permission { get; set; }
    public string[] Permissions { get; set; } = [];
}
