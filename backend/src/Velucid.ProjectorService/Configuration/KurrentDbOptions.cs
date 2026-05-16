namespace Velucid.ProjectorService.Configuration;

public sealed class KurrentDbOptions
{
    public const string SectionName = "KurrentDb";
    public const string DefaultConnectionString = "kurrentdb://localhost:2113?tls=false";
    public string ConnectionString { get; set; } = DefaultConnectionString;
}
