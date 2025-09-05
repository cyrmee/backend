using Domain.DTOs.Common;

namespace Domain.DTOs;

public class UserDto : BaseDto
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? ProfilePicture { get; set; }
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public AppSettingsDto? AppSettings { get; set; }
}

public class UserBaseDto : BaseDto
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? ProfilePicture { get; set; }
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class CreateUserDto
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? ProfilePicture { get; set; }
}

public class UpdateUserDto
{
    public string? Name { get; set; }
    public string? ProfilePicture { get; set; }
    public bool? IsActive { get; set; }
}

public class UserLoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}
