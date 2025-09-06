namespace Application.Common;

public static class CodeGenerator
{
	/// <summary>
	///     Generates a code in the format baseCode + name + guid.
	/// </summary>
	/// <param name="baseCode">The base code to use as a prefix.</param>
	/// <param name="name">The name to include in the code.</param>
	/// <returns>The generated code string.</returns>
	public static string GenerateCode(string baseCode, string name)
    {
        if (string.IsNullOrWhiteSpace(baseCode))
            throw new ArgumentException("Base code cannot be empty for code generation.");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty for code generation.");

        var guid = Guid.NewGuid().ToString("N");
        var cleanName = name.Replace(" ", string.Empty);
        var code = $"{baseCode}_{cleanName}_{guid}".ToUpperInvariant();
        return code;
    }
}