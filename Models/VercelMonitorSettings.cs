namespace VercelDeploymentMonitor.Models;

public sealed class VercelMonitorSettings
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string ApiToken { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string TeamSlug { get; set; } = string.Empty;
    public string ProjectFilter { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 20;

    public bool HasToken => !string.IsNullOrWhiteSpace(ApiToken);
}
