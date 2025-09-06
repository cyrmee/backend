using System.ComponentModel.DataAnnotations;

namespace Domain.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class NotEmptyGuidAttribute() : ValidationAttribute("The {0} field cannot be an empty GUID.")
{
    public override bool IsValid(object? value)
    {
        if (value is Guid guid) return guid != Guid.Empty;

        return false;
    }
}