namespace Domain.Constants;

public static class UserValidationConstraints
{
	public const int MaxUserNameLength = 64;
	public const int MaxEmailLength = 256;
	public const int MaxNameLength = 128;
	public const int MinPasswordLength = 8;
	public const int MaxPasswordLength = 128;
	public const int MaxTokenLength = 1024;
	public const int MaxUrlLength = 2048;
}