using ApexPost.Application.Abstractions.Security;
using ApexPost.Application.Services.MoneyOrders;
using ApexPost.Application.Services.Staff;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Security;

namespace ApexPost.Specs.Support;

/// <summary>State of ONE money-order scenario (created per scenario by Reqnroll context injection).</summary>
public sealed class MoneyOrderScenarioContext
{
    public decimal VerificationLimit { get; set; } = 1000m;

    public string BranchCode { get; set; } = "IND01";

    public string TransactionId { get; set; } = "TXN-1001";

    public ScenarioClock Clock { get; } = new();

    public InMemoryMoneyOrderRepository Orders { get; } = new();

    public CountingUnitOfWork UnitOfWork { get; } = new();

    public Exception? LastError { get; set; }

    public MoneyOrderService CreateService() => new(
        Orders,
        new MoneyOrderPolicy(new Money(VerificationLimit)),
        UnitOfWork,
        Clock);
}

/// <summary>State of ONE sign-in scenario. Uses the real PBKDF2 hasher so the credential check is the real one.</summary>
public sealed class LoginScenarioContext
{
    private int _nextStaffNumber = 1;

    public InMemoryStaffRepository StaffMembers { get; } = new();

    public InMemoryCredentialRepository Credentials { get; } = new();

    public IStaffPasswordHasher Hasher { get; } = new Pbkdf2StaffPasswordHasher();

    public AuthenticatedStaff? Result { get; set; }

    public StaffAuthenticationService CreateService() => new(StaffMembers, Credentials, Hasher);

    public string NextStaffId() => $"STF-{_nextStaffNumber++:000}";
}
