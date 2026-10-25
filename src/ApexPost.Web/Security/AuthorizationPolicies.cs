namespace ApexPost.Web.Security;

public static class AuthorizationPolicies
{
    public const string BranchManagerOnly = "BranchManagerOnly";
    public const string SameBranchOnly = "SameBranchOnly";
    public const string InventoryStaff = "InventoryStaff";
    public const string CounterSales = "CounterSales";
    public const string LoginRateLimit = "LoginRateLimit";
    public const string PartnerIntegrationRateLimit = "PartnerIntegrationRateLimit";
}
