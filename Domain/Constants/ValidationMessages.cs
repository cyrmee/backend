namespace Domain.Constants;

public static class ValidationMessages
{
    public const string RequiredField = "The {0} field is required.";
    public const string MaxLength = "The {0} field must not exceed {1} characters.";
    public const string MinLength = "The {0} field must be at least {1} characters.";
    public const string InvalidEmail = "The email address is not valid.";
    public const string PasswordTooWeak = "The password does not meet the requirements.";
}