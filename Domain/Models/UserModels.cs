using Domain.Models.Common;

namespace Domain.Models;

public class UserModel : BaseModel
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? ProfilePicture { get; set; }
    public DateTime? LastLoginAt { get; set; }
}