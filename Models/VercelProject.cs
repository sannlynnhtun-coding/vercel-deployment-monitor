using System.Text.Json.Serialization;

namespace VercelDeploymentMonitor.Models;

public sealed class VercelProjectsResponse
{
    [JsonPropertyName("projects")]
    public List<VercelProject> Projects { get; set; } = new();
}

public sealed class VercelProject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("framework")]
    public string Framework { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public long? UpdatedAtMs { get; set; }

    public string FrameworkDisplay => string.IsNullOrWhiteSpace(Framework) ? "unknown framework" : Framework;
    public string UpdatedLabel => VercelDeployment.ToDateTimeOffset(UpdatedAtMs).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string Summary => $"{FrameworkDisplay} / {Id}";
}
