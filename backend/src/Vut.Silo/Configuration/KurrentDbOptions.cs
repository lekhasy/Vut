namespace Vut.Silo.Configuration;

/// <summary>
/// Configuration options for connecting to KurrentDB (EventStoreDB).
/// </summary>
public sealed class KurrentDbOptions
{
    /// <summary>
    /// The configuration section name used in appsettings.json.
    /// </summary>
    public const string SectionName = "KurrentDb";

    /// <summary>
    /// The connection string for the KurrentDB instance.
    /// </summary>
    /// <example>esdb://vut-kurrentdb:2113?tls=false</example>
    public string ConnectionString { get; set; } = "esdb://localhost:2113?tls=false";
}
