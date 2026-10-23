
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Services.Branches;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Xunit;

namespace ApexPost.Application.Tests.Services.Branches;

public sealed class BranchServiceTests
{
    [Fact]
    public async Task GetByCodeAsync_ValidCode_ReturnsBranch()
    {
        var branch = CreateBranch();
        var repository = new FakeBranchRepository(branch);
        var service = new BranchService(repository);

        var result = await service.GetByCodeAsync("IND01");

        Assert.Same(branch, result);
    }

    [Fact]
    public async Task GetByCodeAsync_UnknownCode_ReturnsNull()
    {
        var service = new BranchService(new FakeBranchRepository(null));

        var result = await service.GetByCodeAsync("UNKNOWN");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCodeAsync_NormalizesCode()
    {
        var repository = new FakeBranchRepository(null);
        var service = new BranchService(repository);

        await service.GetByCodeAsync(" ind01 ");

        Assert.Equal("IND01", repository.LastRequestedCode?.Value);
    }

    [Fact]
    public async Task GetByCodeAsync_BlankCode_ThrowsArgumentException()
    {
        var service = new BranchService(new FakeBranchRepository(null));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetByCodeAsync(" "));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllBranches()
    {
        var branch = CreateBranch();
        var repository = new FakeBranchRepository(branch);
        var service = new BranchService(repository);

        var result = await service.GetAllAsync();

        Assert.Single(result);
        Assert.Same(branch, result[0]);
    }

    [Fact]
    public async Task IsBranchOpenAsync_ActiveBranchDuringOpeningHours_ReturnsTrue()
    {
        var branch = CreateBranch();
        var service = new BranchService(new FakeBranchRepository(branch));
        var time = new DateTimeOffset(
            2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

        var result = await service.IsBranchOpenAsync("IND01", time);

        Assert.True(result);
    }

    [Fact]
    public async Task IsBranchOpenAsync_ActiveBranchOutsideOpeningHours_ReturnsFalse()
    {
        var branch = CreateBranch();
        var service = new BranchService(new FakeBranchRepository(branch));
        var time = new DateTimeOffset(
            2026, 10, 23, 20, 0, 0, TimeSpan.Zero);

        var result = await service.IsBranchOpenAsync("IND01", time);

        Assert.False(result);
    }

    [Fact]
    public async Task IsBranchOpenAsync_InactiveBranch_ReturnsFalse()
    {
        var branch = CreateBranch();
        branch.Deactivate();

        var service = new BranchService(new FakeBranchRepository(branch));
        var time = new DateTimeOffset(
            2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

        var result = await service.IsBranchOpenAsync("IND01", time);

        Assert.False(result);
    }

    [Fact]
    public async Task GetByCodeAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var service = new BranchService(
            new FakeBranchRepository(CreateBranch()));

        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetByCodeAsync(
                "IND01", cancellationSource.Token));
    }

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BranchService(null!));
    }

    private static Branch CreateBranch() =>
        new(
            new BranchCode("IND01"),
            "Indore Main",
            "Central",
            new TimeOnly(9, 0),
            new TimeOnly(18, 0));

    private sealed class FakeBranchRepository : IBranchRepository
    {
        private readonly Branch? _branch;

        public BranchCode? LastRequestedCode { get; private set; }

        public FakeBranchRepository(Branch? branch)
        {
            _branch = branch;
        }

        public Task<Branch?> GetByCodeAsync(
            BranchCode code,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestedCode = code;

            return Task.FromResult(
                _branch?.Code.Value == code.Value ? _branch : null);
        }

        public Task<IReadOnlyList<Branch>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<Branch> branches = _branch is null
                ? Array.Empty<Branch>()
                : new[] { _branch };

            return Task.FromResult(branches);
        }

        public Task AddAsync(
            Branch branch,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException();
        }

        public Task UpdateAsync(
            Branch branch,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException();
        }
    }
}
