using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace VercelDeploymentMonitor.Models;

public sealed class VercelDeploymentsResponse
{
    [JsonPropertyName("deployments")]
    public List<VercelDeployment> Deployments { get; set; } = new();
}

public sealed class VercelDeployment
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("inspectorUrl")]
    public string InspectorUrl { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("readyState")]
    public string ReadyState { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public long? Created { get; set; }

    [JsonPropertyName("createdAt")]
    public long? CreatedAtMs { get; set; }

    [JsonPropertyName("buildingAt")]
    public long? BuildingAtMs { get; set; }

    [JsonPropertyName("ready")]
    public long? ReadyAtMs { get; set; }

    [JsonPropertyName("creator")]
    public VercelUser? Creator { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }

    [JsonIgnore]
    public string ProductionDomainUrl { get; set; } = string.Empty;

    private string StateKey => StateDisplay.Trim().ToUpperInvariant();

    public string Key => string.IsNullOrWhiteSpace(Uid) ? Id : Uid;

    public string StateDisplay => !string.IsNullOrWhiteSpace(State)
        ? State
        : (!string.IsNullOrWhiteSpace(ReadyState) ? ReadyState : "UNKNOWN");

    public string TargetDisplay => string.IsNullOrWhiteSpace(Target) ? "preview" : Target;
    public string CreatorDisplay => Creator?.Username ?? Creator?.Email ?? "unknown user";
    public string BranchDisplay => GetMetaString("githubCommitRef") ?? GetMetaString("gitlabCommitRef") ?? GetMetaString("bitbucketCommitRef") ?? "no branch";
    public string CommitMessage => GetMetaString("githubCommitMessage") ?? GetMetaString("gitlabCommitMessage") ?? GetMetaString("bitbucketCommitMessage") ?? string.Empty;

    public string DisplayUrl => ToHttpsUrl(Url);
    public string DashboardUrl => ToHttpsUrl(InspectorUrl);
    public string ProductionUrl => ToHttpsUrl(ProductionDomainUrl);
    public string PreferredOpenUrl => string.IsNullOrWhiteSpace(ProductionUrl) ? DisplayUrl : ProductionUrl;

    public string NotificationLaunchUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ProductionUrl))
            {
                return ProductionUrl;
            }

            if (IsReady)
            {
                return DisplayUrl;
            }

            if (!string.IsNullOrWhiteSpace(DashboardUrl))
            {
                return DashboardUrl;
            }

            return PreferredOpenUrl;
        }
    }

    public string Summary => $"{TargetDisplay} / {BranchDisplay} / {CreatorDisplay}";
    public string CreatedLabel => ToDateTimeOffset(CreatedAtMs ?? Created).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string ShortId => Key.Length > 10 ? Key[..10] : Key;
    public string ShortIdLabel => $"id {ShortId}";

    public string StatusLabel => StateKey switch
    {
        "READY" => "Ready",
        "ERROR" => "Failed",
        "CANCELED" => "Canceled",
        "BUILDING" => "Building",
        "QUEUED" => "Queued",
        "INITIALIZING" => "Initializing",
        _ => ToTitleCase(StateDisplay)
    };

    public bool IsReady => StateKey == "READY";
    public bool IsFailed => StateKey == "ERROR" || StateKey == "CANCELED";

    public bool IsBuilding => StateKey == "BUILDING"
        || StateKey == "QUEUED"
        || StateKey == "INITIALIZING";

    public bool IsFinal => IsReady || IsFailed;

    public string StatusBucket => IsReady
        ? "success"
        : IsFailed
            ? "danger"
            : IsBuilding
                ? "warning"
                : "neutral";

    public Brush StatusBadgeBackground => new SolidColorBrush(StatusBackgroundColor);
    public Brush StatusBadgeBorder => new SolidColorBrush(StatusBorderColor);
    public Brush StatusBadgeForeground => new SolidColorBrush(StatusForegroundColor);

    private Color StatusBackgroundColor => StatusBucket switch
    {
        "success" => ColorHelper.FromArgb(0xFF, 0x13, 0x8A, 0x36),
        "danger" => ColorHelper.FromArgb(0xFF, 0xD1, 0x34, 0x38),
        "warning" => ColorHelper.FromArgb(0xFF, 0xE3, 0x9A, 0x00),
        _ => ColorHelper.FromArgb(0xFF, 0x5E, 0x5E, 0x5E)
    };

    private Color StatusBorderColor => StatusBucket switch
    {
        "success" => ColorHelper.FromArgb(0xFF, 0x67, 0xC5, 0x89),
        "danger" => ColorHelper.FromArgb(0xFF, 0xF0, 0x8A, 0x8D),
        "warning" => ColorHelper.FromArgb(0xFF, 0xFF, 0xD1, 0x6B),
        _ => ColorHelper.FromArgb(0xFF, 0x8A, 0x8A, 0x8A)
    };

    private Color StatusForegroundColor => StatusBucket == "warning"
        ? ColorHelper.FromArgb(0xFF, 0x1F, 0x1F, 0x1F)
        : Colors.White;

    private string? GetMetaString(string key)
    {
        if (Meta is null || !Meta.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static string ToHttpsUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"https://{trimmed.TrimStart('/')}";
    }

    private static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        var normalized = value.Replace("_", " ").ToLowerInvariant();
        return char.ToUpperInvariant(normalized[0]) + normalized[1..];
    }

    public static DateTimeOffset ToDateTimeOffset(long? unixTime)
    {
        if (unixTime is null or <= 0)
        {
            return DateTimeOffset.MinValue;
        }

        return unixTime > 10_000_000_000
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixTime.Value)
            : DateTimeOffset.FromUnixTimeSeconds(unixTime.Value);
    }
}

public sealed class VercelUser
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}
