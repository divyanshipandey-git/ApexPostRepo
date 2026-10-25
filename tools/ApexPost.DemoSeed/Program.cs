using ApexPost.DemoSeed;

if (args.Length != 2 || !string.Equals(args[0], "--data-directory", StringComparison.Ordinal))
{
    await Console.Error.WriteLineAsync("Usage: dotnet run --project tools/ApexPost.DemoSeed -- --data-directory <existing-empty-directory>");
    return 2;
}

try
{
    var result = await new DemoDataProvisioner().ProvisionAsync(args[1]);
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
