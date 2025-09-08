using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Domain.Constants;
using Domain.Models.User;

namespace Domain.Models;

public class RegisterModel
{
    [Required(ErrorMessage = ValidationMessages.RequiredField)]
    [EmailAddress(ErrorMessage = ValidationMessages.InvalidEmail)]
    [StringLength(UserValidationConstraints.MaxEmailLength, ErrorMessage = ValidationMessages.MaxLength)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = ValidationMessages.RequiredField)]
    [StringLength(UserValidationConstraints.MaxUserNameLength, ErrorMessage = ValidationMessages.MaxLength)]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = ValidationMessages.RequiredField)]
    [StringLength(UserValidationConstraints.MaxNameLength, ErrorMessage = ValidationMessages.MaxLength)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = ValidationMessages.RequiredField)]
    [MinLength(UserValidationConstraints.MinPasswordLength, ErrorMessage = ValidationMessages.MinLength)]
    [StringLength(UserValidationConstraints.MaxPasswordLength, ErrorMessage = ValidationMessages.MaxLength)]
    public string Password { get; set; } = string.Empty;

    [Phone]
    [StringLength(32, ErrorMessage = ValidationMessages.MaxLength)]
    public string? PhoneNumber { get; set; }
}

public class LoginModel
{
    [Required(ErrorMessage = ValidationMessages.RequiredField)]
    [EmailAddress(ErrorMessage = ValidationMessages.InvalidEmail)]
    [StringLength(UserValidationConstraints.MaxEmailLength, ErrorMessage = ValidationMessages.MaxLength)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = ValidationMessages.RequiredField)]
    [MinLength(UserValidationConstraints.MinPasswordLength, ErrorMessage = ValidationMessages.MinLength)]
    [StringLength(UserValidationConstraints.MaxPasswordLength, ErrorMessage = ValidationMessages.MaxLength)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = false;
}

public class JwtAuthResponseModel
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public UserModel User { get; set; } = new();
}

public class RefreshTokenRequest
{
    [Required(ErrorMessage = ValidationMessages.RequiredField)]
    [StringLength(UserValidationConstraints.MaxTokenLength, ErrorMessage = ValidationMessages.MaxLength)]
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutModel
{
    [Required(ErrorMessage = ValidationMessages.RequiredField)]
    [StringLength(UserValidationConstraints.MaxTokenLength, ErrorMessage = ValidationMessages.MaxLength)]
    public string? RefreshToken { get; set; }

    [JsonIgnore] public string? AccessToken { get; set; }
    [JsonIgnore] public string? UserId { get; set; }
}