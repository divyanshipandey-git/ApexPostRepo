using System.Data.Common;
using ApexPost.Application;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Application.Abstractions.Security;
using ApexPost.Application.Services.Staff;
using ApexPost.DemoSeed;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

if (args.Length == 1 && args[0] == "--provision-local-test-accounts")
{
    try
    {
        await ProvisionLocalTestAccountsAsync(BuildConfiguration());
        return 0;
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbException)
    {
        await Console.Error.WriteLineAsync(exception.Message);
        return 1;
    }
}

if (args.Length == 1 && args[0] == "--query-diagnostics")
{
    var connection = GetSqlConnectionString();
    var options = new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer(connection).Options;
    await using var db = new ApexPostDbContext(options);
    foreach (var (name, sql) in SqlQueryDiagnostics.Capture(db))
        await Console.Out.WriteLineAsync($"-- {name}\n{sql}\n");
    return 0;
}

if (args.Length == 1 && args[0] == "--sql-report-smoke")
{
    var connection = GetSqlConnectionString();
    var options = new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer(connection).Options;
    await using var db = new ApexPostDbContext(options);
    var reports = new SqlReportDataSource(db);
    var transactions = await reports.GetTransactionsAsync();
    var inventory = await reports.GetInventoryAsync();
    var shipments = await reports.GetShipmentsAsync();
    var moneyOrders = await reports.GetMoneyOrdersAsync();
    var identityChecks = await reports.GetIdentityVerificationsAsync();
    await Console.Out.WriteLineAsync($"SQL report reads succeeded: transactionRows={transactions.Count}, inventoryRows={inventory.Count}, shipmentRows={shipments.Count}, moneyOrderRows={moneyOrders.Count}, identityRows={identityChecks.Count}.");
    return 0;
}

if (args.Length == 2 && string.Equals(args[0], "--data-directory", StringComparison.Ordinal))
{
    try
    {
        var result = await new DemoDataProvisioner().ProvisionAsync(args[1], GetDemoDataPassword());
        await Console.Out.WriteLineAsync(result == DemoDataProvisioningResult.AlreadyProvisioned
            ? "Verified the existing ApexPost demo dataset; no files were changed."
            : "Provisioned the ApexPost demo dataset in the explicitly selected directory.");
        return 0;
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
    {
        await Console.Error.WriteLineAsync(exception.Message);
        return 1;
    }
}

if (args.Length == 3 && args[0] == "--demo-json-200" && args[1] == "--data-directory")
{
    try
    {
        var result = await new DemoDataProvisioner().ProvisionAsync(
            args[2],
            GetDemoDataPassword(),
            transactionCount: 200);
        await Console.Out.WriteLineAsync(result == DemoDataProvisioningResult.AlreadyProvisioned
            ? "Verified the existing synthetic 200-sale demo dataset; no files were changed."
            : "Provisioned explicitly synthetic demo JSON with 200 dated sales. Use --import-preview before the separate SQL import command.");
        return 0;
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
    {
        await Console.Error.WriteLineAsync(exception.Message);
        return 1;
    }
}

if (args.Length == 3 &&
    (args[0] is "--import-preview" or "--import-json" or "--import-audit-jsonl") &&
    args[1] == "--data-directory")
{
    try
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets("ApexPost.CounterPlatform-Development")
            .AddEnvironmentVariables()
            .Build();
        services.AddSqlInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();
        var importer = new JsonDataImporter(provider.GetRequiredService<IDbContextFactory<ApexPostDbContext>>());
        if (args[0] == "--import-preview")
        {
            var preview = await importer.PreviewAsync(args[2]);
            await Console.Out.WriteLineAsync($"Preview only. Fingerprint: {preview.Fingerprint}. Counts: {string.Join(", ", preview.SourceCounts.Select(item => $"{item.Key}={item.Value}"))}. No database changes made.");
        }
        else
        {
            var result = args[0] == "--import-audit-jsonl"
                ? await importer.ImportAuditLogAsync(args[2])
                : await importer.ImportAsync(args[2]);
            await Console.Out.WriteLineAsync(
                $"Import complete. Already imported: {result.AlreadyImported}. Counts: {string.Join(", ", result.ImportedCounts.Select(item => $"{item.Key}={item.Value}"))}.");
        }
        return 0;
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or System.Data.Common.DbException)
    {
        await Console.Error.WriteLineAsync(exception.Message);
        return 1;
    }
}

await Console.Error.WriteLineAsync("Usage: dotnet run --project tools/ApexPost.DemoSeed -- --provision-local-test-accounts | --data-directory <existing-empty-directory> | --demo-json-200 --data-directory <existing-empty-directory> | --import-preview --data-directory <read-only-json-directory> | --import-json --data-directory <read-only-json-directory> | --import-audit-jsonl --data-directory <read-only-json-directory> | --query-diagnostics | --sql-report-smoke");
return 2;

static IConfiguration BuildConfiguration() =>
    new ConfigurationBuilder()
        .AddUserSecrets("ApexPost.CounterPlatform-Development")
        .AddEnvironmentVariables()
        .Build();

static string GetSqlConnectionString(IConfiguration? configuration = null)
{
    configuration ??= BuildConfiguration();
    var connectionString = Environment.GetEnvironmentVariable("APEXPOST_SQL_CONNECTION")
        ?? configuration.GetConnectionString("ApexPost");
    return !string.IsNullOrWhiteSpace(connectionString)
        ? connectionString
        : throw new InvalidOperationException(
            "The SQL Server connection string is not configured. Set ConnectionStrings:ApexPost with user-secrets or APEXPOST_SQL_CONNECTION.");
}

static string GetDemoDataPassword()
{
    var configuration = BuildConfiguration();
    var password = configuration["DemoData:Password"]
        ?? Environment.GetEnvironmentVariable("APEXPOST_DEMO_DATA_PASSWORD");
    return !string.IsNullOrWhiteSpace(password)
        ? password
        : throw new InvalidOperationException(
            "The local demo password is not configured. Set DemoData:Password with user-secrets or APEXPOST_DEMO_DATA_PASSWORD.");
}

static async Task ProvisionLocalTestAccountsAsync(IConfiguration configuration)
{
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Local test accounts can only be provisioned when the environment is Development.");
    }

    var connectionString = GetSqlConnectionString(configuration);
    var connection = new SqlConnectionStringBuilder(connectionString);
    var developmentCatalog = string.Equals(connection.InitialCatalog, "ApexPostDb", StringComparison.OrdinalIgnoreCase)
        || connection.InitialCatalog.StartsWith("ApexPost_SqlIT_", StringComparison.OrdinalIgnoreCase);
    if (!string.Equals(connection.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase)
        || !connection.IntegratedSecurity
        || !developmentCatalog)
    {
        throw new InvalidOperationException(
            "Local test accounts require Windows Authentication and the LocalDB instance (localdb)\\MSSQLLocalDB, using ApexPostDb or an ApexPost_SqlIT_* catalog.");
    }

    var services = new ServiceCollection();
    services.AddApplication();
    services.AddSqlInfrastructure(configuration);
    await using var provider = services.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var staffService = scope.ServiceProvider.GetRequiredService<StaffService>();
    var authenticationService = scope.ServiceProvider.GetRequiredService<StaffAuthenticationService>();
    var branches = await scope.ServiceProvider.GetRequiredService<IBranchRepository>().GetAllAsync();
    var branch = branches
        .Where(candidate => candidate.IsActive)
        .OrderBy(candidate => candidate.Code.Value, StringComparer.Ordinal)
        .FirstOrDefault()
        ?? throw new InvalidOperationException("The migrated development database has no active branch.");
    var counters = await scope.ServiceProvider.GetRequiredService<ICounterRepository>()
        .GetByBranchAsync(branch.Code.Value);
    var counter = counters.OrderBy(candidate => candidate.Id, StringComparer.Ordinal).FirstOrDefault()
        ?? throw new InvalidOperationException($"The active branch {branch.Code.Value} has no counter.");

    var accounts = new[]
    {
        Create("LOCALDEV-ADMIN", "local-admin@example.test", "Local development administrator", StaffRole.Administrator, "AdministratorPassword"),
        Create("LOCALDEV-MANAGER", "local-manager@example.test", "Local development branch manager", StaffRole.BranchManager, "BranchManagerPassword"),
        Create("LOCALDEV-AUDITOR", "local-auditor@example.test", "Local development auditor", StaffRole.Auditor, "AuditorPassword"),
        Create("LOCALDEV-CLERK", "local-clerk@example.test", "Local development counter clerk", StaffRole.CounterClerk, "CounterClerkPassword")
    };

    foreach (var account in accounts)
    {
        var existing = (await staffService.GetAllAsync()).FirstOrDefault(member =>
            string.Equals(member.Id, account.Id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(member.Email, account.Email, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            await staffService.CreateAsync(
                account.Id,
                account.Name,
                account.Email,
                account.Role,
                branch.Code.Value,
                account.Password);
        }
        else if (!string.Equals(existing.Id, account.Id, StringComparison.OrdinalIgnoreCase)
                 || !string.Equals(existing.Email, account.Email, StringComparison.OrdinalIgnoreCase)
                 || existing.Role != account.Role
                 || existing.HomeBranchCode != branch.Code
                 || !existing.IsActive)
        {
            throw new InvalidOperationException(
                $"The existing local account {account.Email} does not match the expected active test account; no credentials were changed.");
        }

        var authenticated = await authenticationService.AuthenticateAsync(account.Email, account.Password);
        if (authenticated is null && existing is not null)
        {
            var db = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IStaffPasswordHasher>();
            var credential = await db.StaffCredentials.SingleOrDefaultAsync(
                item => item.StaffId == account.Id);
            if (credential is null)
            {
                db.StaffCredentials.Add(new StaffCredential(account.Id, hasher.HashPassword(account.Password)));
            }
            else
            {
                db.StaffCredentials.Update(
                    new StaffCredential(account.Id, hasher.HashPassword(account.Password)));
            }
            await db.SaveChangesAsync();
            authenticated = await authenticationService.AuthenticateAsync(account.Email, account.Password);
        }

        if (authenticated is null
            || authenticated.Role != account.Role
            || !string.Equals(authenticated.BranchCode, branch.Code.Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SQL-backed authentication verification failed for {account.Email}; no existing credentials were changed.");
        }

        await Console.Out.WriteLineAsync(
            $"Verified SQL-backed local test account: {account.Email} ({account.Role}), branch {branch.Code.Value}, counter {counter.Id}.");
    }

    (string Id, string Email, string Name, StaffRole Role, string Password) Create(
        string id,
        string email,
        string name,
        StaffRole role,
        string passwordKey)
    {
        var password = configuration[$"DevelopmentTestAccounts:{passwordKey}"];
        return !string.IsNullOrWhiteSpace(password)
            ? (id, email, name, role, password)
            : throw new InvalidOperationException(
                $"Missing user-secret DevelopmentTestAccounts:{passwordKey}. Configure it with dotnet user-secrets for src/ApexPost.Web.");
    }
}
