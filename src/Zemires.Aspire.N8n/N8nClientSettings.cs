namespace Microsoft.Extensions.Hosting;

public class N8nClientSettings
{
    public Uri? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public bool DisableHealthChecks { get; set; }
    public int? HealthCheckTimeout { get; set; }
}