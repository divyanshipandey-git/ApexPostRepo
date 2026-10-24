using System.Security.Cryptography;
using System.Text.Json;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Infrastructure.Security;

namespace ApexPost.DemoSeed;

public enum DemoDataProvisioningResult
{
    Provisioned,
    AlreadyProvisioned
}

/// <summary>Creates synthetic local-demo data in a directory explicitly selected by the operator.</summary>
public sealed class DemoDataProvisioner
{
    public const string MarkerFileName = ".apexpost-demo-seed.json";
    public const string DemoPassword = "ApexPost-Demo-Only-2026!";
    private const int SeedVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<DemoDataProvisioningResult> ProvisionAsync(
        string dataDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var directory = JsonPersistenceOptions.ResolveDataDirectory(dataDirectory);
        if (!Directory.Exists(directory))
        {
            throw new ArgumentException("The selected data directory must already exist.", nameof(dataDirectory));
        }

        var markerPath = Path.Combine(directory, MarkerFileName);
        if (File.Exists(markerPath))
        {
            await VerifyExistingSeedAsync(directory, markerPath, cancellationToken);
            return DemoDataProvisioningResult.AlreadyProvisioned;
        }

        if (Directory.EnumerateFileSystemEntries(directory).Any())
        {
            throw new InvalidOperationException(
                "The selected data directory is not empty and has no ApexPost demo seed marker. No files were changed.");
        }

        var options = new JsonPersistenceOptions { DataDirectory = directory };
        var branches = new JsonBranchRepository(directory);
        var counters = new JsonCounterRepository(options);
        var staff = new JsonStaffRepository(directory);
        var credentials = new JsonStaffCredentialRepository(directory);
        var products = new JsonProductRepository(directory);
        var inventory = new JsonInventoryRepository(options);
        var transactions = new JsonCounterTransactionRepository(options);
        var passwordHash = new Pbkdf2StaffPasswordHasher().HashPassword(DemoPassword);
        var expectedFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var branch in CreateBranches())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await branches.AddAsync(branch, cancellationToken);
            var counterId = CounterId(branch.Code.Value);
            await counters.AddAsync(new Counter(counterId, branch.Code, "Counter 1"), cancellationToken);
        }

        var branchCodes = CreateBranchCodes();
        var staffRecords = CreateStaff(branchCodes);
        foreach (var member in staffRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await staff.AddAsync(member, cancellationToken);
            await credentials.AddAsync(new StaffCredential(member.Id, passwordHash), cancellationToken);
        }

        var productRecords = CreateProducts();
        foreach (var product in productRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await products.AddAsync(product, cancellationToken);
        }

        foreach (var branchCode in branchCodes)
        foreach (var product in productRecords)
        {
            await inventory.AddAsync(new InventoryStock(branchCode, product.Sku, 100), cancellationToken);
        }

        var rates = CreateTaxRates();
        for (var index = 0; index < 40; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var branchCode = branchCodes[index % branchCodes.Length];
            var product = productRecords[index % productRecords.Length];
            var clerk = staffRecords.First(member =>
                member.Role == StaffRole.CounterClerk && member.HomeBranchCode == branchCode);
            var transaction = new CounterTransaction(
                $"DEMO-TXN-{index + 1:000}",
                branchCode,
                CounterId(branchCode.Value),
                clerk.Id,
                rates);
            transaction.AddLineItem(new TransactionLineItem(
                product.Name,
                "Retail",
                1 + index % 3,
                product.UnitPrice,
                product.TaxCategory,
                product.Sku.Value));
            transaction.CompletePayment(Enum.GetValues<PaymentMethod>()[index % Enum.GetValues<PaymentMethod>().Length]);
            await transactions.AddAsync(transaction, cancellationToken);
        }

        foreach (var fileName in new[]
                 {
                     "branches.json", "counters.json", "staff.json", "staff-credentials.json",
                     "products.json", "inventory.json", "counter-transactions.json"
                 })
        {
            expectedFiles.Add(fileName);
        }

        var hashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileName in expectedFiles)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(directory, fileName), cancellationToken);
            hashes.Add(fileName, Convert.ToHexString(SHA256.HashData(bytes)));
        }

        var manifest = new SeedManifest(SeedVersion, hashes);
        await using (var marker = new FileStream(markerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(marker, manifest, JsonOptions, cancellationToken);
            await marker.FlushAsync(cancellationToken);
        }

        return DemoDataProvisioningResult.Provisioned;
    }

    private static async Task VerifyExistingSeedAsync(
        string directory,
        string markerPath,
        CancellationToken cancellationToken)
    {
        await using var marker = new FileStream(markerPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var manifest = await JsonSerializer.DeserializeAsync<SeedManifest>(marker, cancellationToken: cancellationToken);
        if (manifest is null || manifest.Version != SeedVersion || manifest.Files.Count != 7)
        {
            throw new InvalidOperationException("The demo seed marker is invalid or uses an unsupported version. No files were changed.");
        }

        var actualNames = Directory.EnumerateFiles(directory)
            .Select(Path.GetFileName)
            .Where(name => name is not null && !string.Equals(name, MarkerFileName, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!actualNames.SetEquals(manifest.Files.Keys))
        {
            throw new InvalidOperationException("The seeded directory contents have changed. No files were changed.");
        }

        foreach (var (fileName, expectedHash) in manifest.Files)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(directory, fileName), cancellationToken);
            var actualHash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(actualHash),
                    Convert.FromHexString(expectedHash)))
            {
                throw new InvalidOperationException("The seeded data has changed since provisioning. No files were changed.");
            }
        }
    }

    private static Branch[] CreateBranches()
    {
        var names = new[] { "Central", "North", "South", "East", "West" };
        return names.Select((name, index) => new Branch(
            new BranchCode($"AP{index + 1:000}"),
            $"ApexPost {name}",
            name,
            new TimeOnly(8, 0),
            new TimeOnly(20, 0))).ToArray();
    }

    private static BranchCode[] CreateBranchCodes() =>
        Enumerable.Range(1, 5).Select(index => new BranchCode($"AP{index:000}")).ToArray();

    private static Staff[] CreateStaff(IReadOnlyList<BranchCode> branches)
    {
        var roles = Enum.GetValues<StaffRole>();
        return Enumerable.Range(0, 20).Select(index =>
        {
            var role = roles[index % roles.Length];
            var roleName = role.ToString().ToLowerInvariant();
            var id = $"DEMO-{roleName}-{index + 1:00}";
            return new Staff(
                id,
                $"Demo {role} {index + 1:00}",
                $"{roleName}{index + 1:00}@example.test",
                role,
                branches[index % branches.Count]);
        }).ToArray();
    }

    private static Product[] CreateProducts() => Enumerable.Range(1, 30).Select(index =>
    {
        var category = (ProductCategory)((index - 1) % Enum.GetValues<ProductCategory>().Length);
        var tax = category == ProductCategory.Packaging ? TaxCategory.Reduced : TaxCategory.Standard;
        return new Product(
            new Sku($"DEMO{index:000}"),
            $"Demo {category} Item {index:00}",
            category,
            new Money(25m + index * 7m),
            tax);
    }).ToArray();

    private static TaxRate[] CreateTaxRates() =>
    [
        new TaxRate(TaxCategory.Standard, 0.18m),
        new TaxRate(TaxCategory.Reduced, 0.05m),
        new TaxRate(TaxCategory.ZeroRated, 0m),
        new TaxRate(TaxCategory.Exempt, 0m)
    ];

    private static string CounterId(string branchCode) => $"COUNTER-{branchCode}-1";

    private sealed record SeedManifest(int Version, SortedDictionary<string, string> Files);
}
