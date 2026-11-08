using System.Text.Json;
using VercelDeploymentMonitor.Models;
using Windows.Security.Credentials;

namespace VercelDeploymentMonitor.Services;

public sealed class AppSettingsStore
{
    private const string CredentialResource = "VercelDeploymentMonitor.VercelApiToken";
    private const string CredentialUserName = "default";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VercelDeploymentMonitor",
        "settings.json");

    public async Task<VercelMonitorSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        VercelMonitorSettings settings;
        if (!File.Exists(SettingsPath))
        {
            settings = new VercelMonitorSettings();
        }
        else
        {
            await using var stream = File.OpenRead(SettingsPath);
            settings = await JsonSerializer.DeserializeAsync<VercelMonitorSettings>(stream, JsonOptions, cancellationToken)
                ?? new VercelMonitorSettings();
        }

        var normalized = Normalize(settings);
        normalized.ApiToken = LoadToken();
        return normalized;
    }

    public async Task SaveAsync(VercelMonitorSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(settings);
        SaveToken(normalized.ApiToken);

        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
    }

    private static VercelMonitorSettings Normalize(VercelMonitorSettings settings)
    {
        settings.ApiToken = settings.ApiToken.Trim();
        settings.TeamId = settings.TeamId.Trim();
        settings.TeamSlug = settings.TeamSlug.Trim();
        settings.ProjectFilter = settings.ProjectFilter.Trim();
        settings.PollIntervalSeconds = Math.Clamp(settings.PollIntervalSeconds, 10, 600);
        return settings;
    }

    private static string LoadToken()
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(CredentialResource, CredentialUserName);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void SaveToken(string token)
    {
        var vault = new PasswordVault();
        try
        {
            var existing = vault.Retrieve(CredentialResource, CredentialUserName);
            vault.Remove(existing);
        }
        catch
        {
            // Missing credentials are fine; this keeps saves idempotent.
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            vault.Add(new PasswordCredential(CredentialResource, CredentialUserName, token));
        }
    }
}
