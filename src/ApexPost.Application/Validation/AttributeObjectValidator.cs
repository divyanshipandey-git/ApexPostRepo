using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ApexPost.Application.Validation;

/// <summary>
/// Validates custom and data-annotation attributes with property metadata cached per runtime type.
/// Cached rules avoid repeating reflection for each request; attribute validation still runs per object.
/// </summary>
public static class AttributeObjectValidator
{
    private static readonly ConcurrentDictionary<Type, PropertyRule[]> RulesByType = new();

    public static bool TryValidateObjectGraph(object instance, out IReadOnlyList<ValidationResult> results)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var failures = new List<ValidationResult>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        ValidateNode(instance, visited, failures);
        results = failures;
        return failures.Count == 0;
    }

    private static void ValidateNode(
        object? instance,
        HashSet<object> visited,
        List<ValidationResult> failures)
    {
        if (instance is null || IsLeaf(instance.GetType()) || !visited.Add(instance))
        {
            return;
        }

        if (instance is IEnumerable sequence)
        {
            foreach (var item in sequence)
            {
                ValidateNode(item, visited, failures);
            }

            return;
        }

        foreach (var rule in RulesByType.GetOrAdd(instance.GetType(), CreateRules))
        {
            var value = rule.Property.GetValue(instance);
            var context = new ValidationContext(instance) { MemberName = rule.Property.Name };
            foreach (var attribute in rule.Attributes)
            {
                var result = attribute.GetValidationResult(value, context);
                if (result != ValidationResult.Success)
                {
                    failures.Add(result!);
                }
            }

            ValidateNode(value, visited, failures);
        }
    }

    private static PropertyRule[] CreateRules(Type type) => type
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
        .Select(property => new PropertyRule(
            property,
            property.GetCustomAttributes<ValidationAttribute>(inherit: true).ToArray()))
        .ToArray();

    private static bool IsLeaf(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
        type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid) ||
        type == typeof(TimeSpan);

    private sealed record PropertyRule(PropertyInfo Property, ValidationAttribute[] Attributes);
}
