using System.Security.Cryptography;
using ApexPost.DemoSeed;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Infrastructure.Security;
using Xunit;

namespace ApexPost.Application.Tests.DemoSeed;

public sealed class DemoDataProvisionerTests
{
    [Fact]
    public async Task ProvisionAsync_CreatesExpectedSyntheticDataset_AndRepeatIsNoOp()
    {
        using var directory = new TemporaryDirectory();
        var provisioner = new DemoDataProvisioner();
        var password = CreateTestPassword();

        var firstRun = await provisioner.ProvisionAsync(directory.Path, password);
        var secondRun = await provisioner.ProvisionAsync(directory.Path, password);

        Assert.Equal(DemoDataProvisioningResult.Provisioned, firstRun);
        Assert.Equal(DemoDataProvisioningResult.AlreadyProvisioned, secondRun);

        var branches = await new JsonBranchRepository(directory.Path).GetAllAsync();
        var staff = await new JsonStaffRepository(directory.Path).GetAllAsync();
        var products = await new JsonProductRepository(directory.Path).GetAllAsync();
        var counterRepository = new JsonCounterRepository(new JsonPersistenceOptions
        {
            DataDirectory = directory.Path
        });
        var inventoryRepository = new JsonInventoryRepository(new JsonPersistenceOptions
        {
            DataDirectory = directory.Path
        });
        var transactionRepository = new JsonCounterTransactionRepository(new JsonPersistenceOptions
        {
            DataDirectory = directory.Path
        });
        var countersByBranch = await Task.WhenAll(branches.Select(branch =>
            counterRepository.GetByBranchAsync(branch.Code.Value)));
        var inventoryByBranch = await Task.WhenAll(branches.Select(branch =>
            inventoryRepository.GetByBranchAsync(branch.Code.Value)));
        var transactionsByBranch = await Task.WhenAll(branches.Select(branch =>
            transactionRepository.GetByBranchAsync(branch.Code.Value)));
        Assert.Equal(5, branches.Count);
        Assert.Equal(20, staff.Count);
        Assert.Equal(30, products.Count);
        Assert.Equal(40, transactionsByBranch.Sum(items => items.Count));
        Assert.All(countersByBranch, branchCounters => Assert.Single(branchCounters));
        Assert.All(inventoryByBranch, branchInventory => Assert.Equal(30, branchInventory.Count));
        Assert.All(staff, member => Assert.EndsWith("@example.test", member.Email, StringComparison.Ordinal));

        var account = staff.First(member => member.Role == StaffRole.CounterClerk);
        var credentials = new JsonStaffCredentialRepository(directory.Path);
        var hash = await credentials.GetPasswordHashAsync(account.Id);
        Assert.True(new Pbkdf2StaffPasswordHasher().VerifyPassword(password, hash));
    }

    [Fact]
    public async Task ProvisionAsync_RefusesNonEmptyDirectoryWithoutChangingIt()
    {
        using var directory = new TemporaryDirectory();
        var sentinelPath = Path.Combine(directory.Path, "user-data.json");
        await File.WriteAllTextAsync(sentinelPath, "[]");
        var password = CreateTestPassword();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DemoDataProvisioner().ProvisionAsync(directory.Path, password));

        Assert.Equal("[]", await File.ReadAllTextAsync(sentinelPath));
        Assert.Single(Directory.EnumerateFiles(directory.Path));
    }

    [Fact]
    public async Task ProvisionAsync_CanCreateTwoHundredExplicitlySyntheticHistoricalTransactions()
    {
        using var directory = new TemporaryDirectory();
        await new DemoDataProvisioner().ProvisionAsync(
            directory.Path,
            CreateTestPassword(),
            transactionCount: 200);
        var repository = new JsonCounterTransactionRepository(new JsonPersistenceOptions { DataDirectory = directory.Path });
        var transactions = await repository.GetByBranchAsync("AP001");

        Assert.Equal(40, transactions.Count);
        Assert.All(transactions, transaction => Assert.NotNull(transaction.OccurredAtUtc));
        var allBranches = await Task.WhenAll(Enumerable.Range(1, 5)
            .Select(index => repository.GetByBranchAsync($"AP{index:000}")));
        Assert.Equal(200, allBranches.Sum(items => items.Count));
    }

    [Fact]
    public async Task ProvisionAsync_RefusesSeedDirectoryChangedAfterProvisioning()
    {
        using var directory = new TemporaryDirectory();
        var provisioner = new DemoDataProvisioner();
        var password = CreateTestPassword();
        await provisioner.ProvisionAsync(directory.Path, password);
        await File.AppendAllTextAsync(Path.Combine(directory.Path, "staff.json"), " ");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provisioner.ProvisionAsync(directory.Path, password));
    }

    private static string CreateTestPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"apexpost-demo-seed-{Guid.NewGuid():N}");

        public TemporaryDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
