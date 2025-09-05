namespace Domain.Constants;

public static class UserConstants
{
    public const int MaxUserNameLength = 256;
    public const int MaxEmailLength = 256;
    public const int MaxNameLength = 100;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MaxFailedLoginAttempts = 5;
    public const int LockoutDurationMinutes = 5;
}