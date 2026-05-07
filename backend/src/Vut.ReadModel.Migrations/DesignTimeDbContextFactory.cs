using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Vut.ReadModel;

namespace Vut.ReadModel.Migrations;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReadModelDbContext>
{
    public ReadModelDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.Length > 0
            ? args[0]
            : "Host=localhost;Database=vut_readmodel;Username=vut_app;Password=vut_dev_password";

        var options = new DbContextOptionsBuilder<ReadModelDbContext>()
            .UseNpgsql(connectionString, b => b.MigrationsAssembly("Vut.ReadModel.Migrations"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ReadModelDbContext(options);
    }
}
