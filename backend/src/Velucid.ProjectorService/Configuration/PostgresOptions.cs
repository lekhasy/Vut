namespace Velucid.ProjectorService.Configuration;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";
    public const string DefaultConnectionString = "Host=localhost;Port=5432;Database=velucid_readmodel;Username=velucid;Password=velucid";
    public string ConnectionString { get; set; } = DefaultConnectionString;
}
