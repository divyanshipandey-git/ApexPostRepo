
using System.Reflection;

namespace ApexPost.Infrastructure.Persistence.Repositories;

internal static class JsonEntityPropertyReader
{
    private static readonly string[] BranchPropertyNames =
    [
        "BranchCode",
        "OriginBranchCode",
        "SourceBranchCode",
        "SendingBranchCode"
    ];

    public static string GetBranchCode<TEntity>(TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        var entityType = entity.GetType();

        foreach (var propertyName in BranchPropertyNames)
        {
            var property = entityType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

            if (property is null)
            {
                continue;
            }

            var value = property.GetValue(entity);

            if (value is null)
            {
                continue;
            }

            var valueProperty = value.GetType().GetProperty(
                "Value",
                BindingFlags.Instance | BindingFlags.Public);

            var rawValue = valueProperty?.GetValue(value) ?? value;

            if (rawValue is string branchCode &&
                !string.IsNullOrWhiteSpace(branchCode))
            {
                return branchCode.Trim();
            }
        }

        throw new InvalidOperationException(
            $"Type '{entityType.Name}' does not expose a supported branch-code property.");
    }
}
