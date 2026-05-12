using Microsoft.EntityFrameworkCore;
using Velucid.ReadModel;
using Velucid.ReadModel.Migrations;

var connectionString = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("CONNECTION_STRING");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("Usage: Velucid.ReadModel.Migrations <connection-string>");
    Console.WriteLine("   or set CONNECTION_STRING environment variable");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  dotnet run -- \"Host=localhost;Database=velucid_readmodel;Username=velucid_app;Password=velucid_dev_password\"");
    return 1;
}

var options = new DbContextOptionsBuilder<ReadModelDbContext>()
    .UseNpgsql(connectionString, b => b.MigrationsAssembly("Velucid.ReadModel.Migrations"))
    .UseSnakeCaseNamingConvention()
    .Options;

await using var context = new ReadModelDbContext(options);
var pending = await context.Database.GetPendingMigrationsAsync();

if (!pending.Any())
{
    Console.WriteLine("No pending migrations.");
    return 0;
}

Console.WriteLine($"Applying {pending.Count()} migration(s): {string.Join(", ", pending)}");
await context.Database.MigrateAsync();
Console.WriteLine("Migration completed successfully.");
return 0;
