namespace Velucid.ProjectorService.Configuration;

public sealed class OpenFgaOptions
{
    public const string SectionName = "OpenFga";

    public bool Enabled { get; set; } = true;

    public string ApiUrl { get; set; } = "http://localhost:8080";

    public string StoreName { get; set; } = "velucid";
}
