using Microsoft.EntityFrameworkCore;
using Vut.ReadModel;
using Vut.ReadModel.Migrations;

var connectionString = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("CONNECTION_STRING");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("Usage: Vut.ReadModel.Migrations <connection-string>");
    Console.WriteLine("   or set CONNECTION_STRING environment variable");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  dotnet run -- \"Host=localhost;Database=vut_readmodel;Username=vut_app;Password=vut_dev_password\"");
    return 1;
}

var options = new DbContextOptionsBuilder<ReadModelDbContext>()
    .UseNpgsql(connectionString)
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
