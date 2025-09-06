namespace Domain.Models.Common;

public class AuthValidationResponse
{
    public string? UserName { get; set; }
    public Guid? UserId { get; set; }
    public List<string>? Roles { get; set; }
    public List<string>? Permissions { get; set; }
    public string? Email { get; set; }
}