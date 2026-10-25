using System.ComponentModel.DataAnnotations;

namespace ApexPost.Application.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MaskedIdentifierAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string text && text.Length == 8 &&
        text.StartsWith("XXXX", StringComparison.OrdinalIgnoreCase) &&
        text.AsSpan(4).ToArray().All(char.IsAsciiLetterOrDigit);

    public override string FormatErrorMessage(string name) =>
        $"{name} must be masked as XXXX followed by four visible characters.";
}
