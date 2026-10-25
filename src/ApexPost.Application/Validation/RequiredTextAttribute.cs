using System.ComponentModel.DataAnnotations;

namespace ApexPost.Application.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class RequiredTextAttribute(int maximumLength) : ValidationAttribute
{
    public int MaximumLength { get; } = maximumLength > 0
        ? maximumLength
        : throw new ArgumentOutOfRangeException(nameof(maximumLength));

    public override bool IsValid(object? value) =>
        value is string text && !string.IsNullOrWhiteSpace(text) && text.Length <= MaximumLength;

    public override string FormatErrorMessage(string name) =>
        $"{name} is required and must be no longer than {MaximumLength} characters.";
}
