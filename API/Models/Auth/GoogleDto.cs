namespace API.Models.Auth;

public class GoogleDto
{
    public string? Email      { get; set; }
    public string? Name       { get; set; }
    public string? ProviderId { get; set; }
    public string? PictureUrl { get; set; }
    public string  Role       { get; set; } = "farmer";
}