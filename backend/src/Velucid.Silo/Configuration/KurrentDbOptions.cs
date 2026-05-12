namespace Velucid.Silo.Configuration;

/// <summary>
/// Configuration options for connecting to KurrentDB.
/// </summary>
public sealed class KurrentDbOptions
{
    /// <summary>
    /// The configuration section name used in appsettings.json.
    /// </summary>
    public const string SectionName = "KurrentDb";

    /// <summary>
    /// The default connection string used when no configuration is provided.
    /// </summary>
    public const string DefaultConnectionString = "kurrentdb://localhost:2113?tls=false";

    /// <summary>
    /// The connection string for the KurrentDB instance.
    /// </summary>
    /// <example>kurrentdb://vut-kurrentdb:2113?tls=false</example>
    public string ConnectionString { get; set; } = DefaultConnectionString;
}
