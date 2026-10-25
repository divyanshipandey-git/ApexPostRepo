
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Staff;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using StaffEntity = ApexPost.Domain.Entities.Staff;
using Xunit;

namespace ApexPost.Application.Tests.Services.Staff;

public sealed class StaffServiceTests
{
    [Fact]
    public async Task GetByIdAsync_ExistingStaff_ReturnsStaff()
    {
        var staff = CreateStaff();
        var service = new StaffService(new FakeStaffRepository(staff));

        var result = await service.GetByIdAsync("EMP001");

        Assert.Same(staff, result);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownStaff_ReturnsNull()
    {
        var service = new StaffService(new FakeStaffRepository());

        var result = await service.GetByIdAsync("UNKNOWN");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByIdAsync_BlankId_ThrowsArgumentException(string staffId)
    {
        var service = new StaffService(new FakeStaffRepository());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetByIdAsync(staffId));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllStaff()
    {
        var first = CreateStaff();
        var second = new StaffEntity(
            "EMP002",
            "Employee Two",
            "employee2@example.com",
            StaffRole.BranchManager,
            new BranchCode("IND01"));

        var service = new StaffService(
            new FakeStaffRepository(first, second));

        var result = await service.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(first, result);
        Assert.Contains(second, result);
    }

    [Fact]
    public async Task GetByBranchAsync_ReturnsStaffForRequestedBranch()
    {
        var staff = CreateStaff();
        var repository = new FakeStaffRepository(staff);
        var service = new StaffService(repository);

        var result = await service.GetByBranchAsync("ind01");

        Assert.Single(result);
        Assert.Same(staff, result[0]);
    }

    [Fact]
    public async Task GetByBranchAsync_NormalizesBranchCode()
    {
        var repository = new FakeStaffRepository();
        var service = new StaffService(repository);

        await service.GetByBranchAsync(" ind01 ");

        Assert.Equal("IND01", repository.LastRequestedBranchCode);
    }

    [Fact]
    public async Task GetByBranchAsync_BlankBranchCode_ThrowsArgumentException()
    {
        var service = new StaffService(new FakeStaffRepository());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetByBranchAsync(" "));
    }

    [Fact]
    public async Task SetActiveStatusAsync_DeactivatedStaff_UpdatesRepository()
    {
        var staff = CreateStaff();
        var repository = new FakeStaffRepository(staff);
        var service = new StaffService(repository);

        var result = await service.SetActiveStatusAsync("EMP001", false);

        Assert.True(result);
        Assert.False(staff.IsActive);
        Assert.Same(staff, repository.LastUpdatedStaff);
    }

    [Fact]
    public async Task SetActiveStatusAsync_ActivatedStaff_UpdatesRepository()
    {
        var staff = CreateStaff();
        staff.Deactivate();

        var repository = new FakeStaffRepository(staff);
        var service = new StaffService(repository);

        var result = await service.SetActiveStatusAsync("EMP001", true);

        Assert.True(result);
        Assert.True(staff.IsActive);
        Assert.Same(staff, repository.LastUpdatedStaff);
    }

    [Fact]
    public async Task SetActiveStatusAsync_UnknownStaff_ReturnsFalse()
    {
        var repository = new FakeStaffRepository();
        var service = new StaffService(repository);

        var result = await service.SetActiveStatusAsync("UNKNOWN", false);

        Assert.False(result);
        Assert.Null(repository.LastUpdatedStaff);
    }

    [Fact]
    public async Task GetByIdAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var service = new StaffService(
            new FakeStaffRepository(CreateStaff()));

        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetByIdAsync(
                "EMP001", cancellationSource.Token));
    }

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new StaffService(null!));
    }

    private static StaffEntity CreateStaff() =>
        new(
            "EMP001",
            "Employee One",
            "employee1@example.com",
            StaffRole.CounterClerk,
            new BranchCode("IND01"));

    private sealed class FakeStaffRepository : IStaffRepository
    {
        private readonly List<StaffEntity> _staff;

        public string? LastRequestedBranchCode { get; private set; }

        public StaffEntity? LastUpdatedStaff { get; private set; }

        public FakeStaffRepository(params StaffEntity[] staff)
        {
            _staff = [.. staff];
        }

        public Task<StaffEntity?> GetByIdAsync(
            string staffId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _staff.FirstOrDefault(
                    staff => staff.Id == staffId));
        }

        public Task<IReadOnlyList<StaffEntity>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<StaffEntity> staff = _staff.ToArray();
            return Task.FromResult(staff);
        }

        public Task<IReadOnlyList<StaffEntity>> GetByBranchAsync(
            string branchCode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestedBranchCode = branchCode;

            IReadOnlyList<StaffEntity> staff = _staff
                .Where(person =>
                    person.HomeBranchCode.Value == branchCode)
                .ToArray();

            return Task.FromResult(staff);
        }

        public Task AddAsync(
            StaffEntity staff,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException();
        }

        public Task UpdateAsync(
            StaffEntity staff,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastUpdatedStaff = staff;
            return Task.CompletedTask;
        }
    }
}
