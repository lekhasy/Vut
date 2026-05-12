using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Velucid.ReadModel;

namespace Velucid.ReadModel.Migrations;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReadModelDbContext>
{
    public ReadModelDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.Length > 0
            ? args[0]
            : "Host=localhost;Database=velucid_readmodel;Username=velucid_app;Password=velucid_dev_password";

        var options = new DbContextOptionsBuilder<ReadModelDbContext>()
            .UseNpgsql(connectionString, b => b.MigrationsAssembly("Velucid.ReadModel.Migrations"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ReadModelDbContext(options);
    }
}
