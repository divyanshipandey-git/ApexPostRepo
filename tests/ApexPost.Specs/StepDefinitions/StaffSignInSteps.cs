using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Specs.Support;
using Reqnroll;
using Xunit;

namespace ApexPost.Specs.StepDefinitions;

/// <summary>Steps of StaffSignIn.feature. SERVICE level: the real StaffAuthenticationService and the real PBKDF2 hasher.</summary>
[Binding]
public sealed class StaffSignInSteps(LoginScenarioContext context)
{
    // ------------------------------------------------------------------ given

    [Given(@"staff member ""([^""]*)"" is registered at branch ""([^""]*)"" as ""([^""]*)"" with password ""([^""]*)""")]
    public async Task GivenStaffMemberIsRegistered(string email, string branchCode, string role, string password)
    {
        var staff = new Staff(
            context.NextStaffId(),
            email.Split('@')[0],
            email,
            Enum.Parse<StaffRole>(role),
            new BranchCode(branchCode));

        await context.StaffMembers.AddAsync(staff);
        await context.Credentials.AddPasswordHashAsync(staff.Id, context.Hasher.HashPassword(password));
    }

    [Given(@"staff member ""([^""]*)"" is deactivated")]
    public async Task GivenStaffMemberIsDeactivated(string email)
    {
        var staff = await FindAsync(email);
        staff.Deactivate();
    }

    // ------------------------------------------------------------------ when

    [When(@"someone signs in with email ""([^""]*)"" and password ""([^""]*)""")]
    public async Task WhenSomeoneSignsIn(string email, string password) =>
        context.Result = await context.CreateService().AuthenticateAsync(email, password);

    // ------------------------------------------------------------------ then

    [Then(@"sign-in succeeds as ""([^""]*)"" at branch ""([^""]*)""")]
    public void ThenSignInSucceeds(string role, string branchCode)
    {
        Assert.NotNull(context.Result);
        Assert.Equal(Enum.Parse<StaffRole>(role), context.Result.Role);
        Assert.Equal(branchCode, context.Result.BranchCode);
    }

    [Then("sign-in is refused")]
    public void ThenSignInIsRefused() => Assert.Null(context.Result);

    [Then(@"the stored credential of ""([^""]*)"" is a salted hash that does not contain ""([^""]*)""")]
    public async Task ThenTheStoredCredentialIsAHash(string email, string password)
    {
        var staff = await FindAsync(email);
        var stored = await context.Credentials.GetPasswordHashAsync(staff.Id);

        Assert.NotNull(stored);
        Assert.StartsWith("pbkdf2-sha256$", stored, StringComparison.Ordinal);
        Assert.DoesNotContain(password, stored, StringComparison.Ordinal);
    }

    private async Task<Staff> FindAsync(string email) =>
        Assert.Single(await context.StaffMembers.GetAllAsync(), member => member.Email == email);
}
