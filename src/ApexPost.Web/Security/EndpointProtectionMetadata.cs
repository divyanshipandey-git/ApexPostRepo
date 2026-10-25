namespace ApexPost.Web.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class ApiKeyProtectedAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class IpAllowlistProtectedAttribute(string configurationKey) : Attribute
{
    public string ConfigurationKey { get; } = configurationKey;
}
