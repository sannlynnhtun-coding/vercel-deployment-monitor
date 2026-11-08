using System.Text.Json.Serialization;

namespace VercelDeploymentMonitor.Models;

public sealed class VercelProjectDomainsResponse
{
    [JsonPropertyName("domains")]
    public List<VercelProjectDomain> Domains { get; set; } = new();
}

public sealed class VercelProjectDomain
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("redirect")]
    public string Redirect { get; set; } = string.Empty;

    [JsonPropertyName("apexName")]
    public string ApexName { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsRedirect => !string.IsNullOrWhiteSpace(Redirect);

    [JsonIgnore]
    public string Url => string.IsNullOrWhiteSpace(Name) ? string.Empty : $"https://{Name}";

    [JsonIgnore]
    public bool IsDefaultVercelDomain => Name.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase);
}
