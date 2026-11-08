using System.Text.Json.Serialization;

namespace VercelDeploymentMonitor.Models;

public sealed class VercelDeploymentEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public long? Created { get; set; }

    [JsonPropertyName("text")]
    public string? TextRaw { get; set; }

    [JsonPropertyName("deploymentId")]
    public string? DeploymentId { get; set; }

    [JsonPropertyName("payload")]
    public VercelDeploymentEventPayload? Payload { get; set; }

    [JsonIgnore]
    public DateTimeOffset EventTime => VercelDeployment.ToDateTimeOffset(Payload?.Created ?? Payload?.Date ?? Created);

    [JsonIgnore]
    public string TimeLabel => EventTime == DateTimeOffset.MinValue ? "--:--:--" : EventTime.ToLocalTime().ToString("HH:mm:ss");

    [JsonIgnore]
    public string TypeDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Payload?.Info?.Type))
            {
                return Payload.Info.Type;
            }

            return string.IsNullOrWhiteSpace(Type) ? "log" : Type;
        }
    }

    [JsonIgnore]
    public string Text
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TextRaw))
            {
                return TextRaw;
            }

            if (!string.IsNullOrWhiteSpace(Payload?.Text))
            {
                return Payload.Text;
            }

            if (!string.IsNullOrWhiteSpace(Payload?.Message))
            {
                return Payload.Message;
            }

            if (!string.IsNullOrWhiteSpace(Payload?.Info?.Step))
            {
                return $"{Payload.Info.Step} ({Payload.Info.ReadyState})";
            }

            if (!string.IsNullOrWhiteSpace(Payload?.Info?.Name))
            {
                return Payload.Info.Name;
            }

            return "(no message)";
        }
    }

    [JsonIgnore]
    public bool ShouldDisplay => !string.Equals(Type, "delimiter", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string DisplayLine => $"[{TimeLabel}] {TypeDisplay}: {Text}";
}

public sealed class VercelDeploymentEventPayload
{
    [JsonPropertyName("created")]
    public long? Created { get; set; }

    [JsonPropertyName("date")]
    public long? Date { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("info")]
    public VercelDeploymentEventInfo? Info { get; set; }
}

public sealed class VercelDeploymentEventInfo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("step")]
    public string Step { get; set; } = string.Empty;

    [JsonPropertyName("readyState")]
    public string ReadyState { get; set; } = string.Empty;
}
