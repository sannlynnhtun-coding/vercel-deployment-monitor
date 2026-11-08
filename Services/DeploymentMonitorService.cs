using VercelDeploymentMonitor.Models;

namespace VercelDeploymentMonitor.Services;

public sealed class DeploymentMonitorService : IDisposable
{
    private readonly AppSettingsStore _settingsStore;
    private readonly NotificationService _notifications;
    private readonly Dictionary<string, string> _knownStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _productionDomainByProjectId = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _monitorCts;
    private bool _hasBaseline;
    private bool _isRefreshing;

    public event EventHandler<DeploymentsChangedEventArgs>? DeploymentsChanged;
    public event EventHandler<MonitorStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<DeploymentCompletedEventArgs>? DeploymentCompleted;

    public DeploymentMonitorService(AppSettingsStore settingsStore, NotificationService notifications)
    {
        _settingsStore = settingsStore;
        _notifications = notifications;
    }

    public IReadOnlyList<VercelDeployment> CurrentDeployments { get; private set; } = Array.Empty<VercelDeployment>();
    public bool IsRunning => _monitorCts is not null;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_monitorCts is not null)
        {
            return;
        }

        _monitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        StatusChanged?.Invoke(this, new MonitorStatusChangedEventArgs("Monitor started. Checking Vercel now...", true));

        try
        {
            await RefreshAsync(_monitorCts.Token);
            _ = RunMonitorLoopAsync(_monitorCts.Token);
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        if (_monitorCts is null)
        {
            return;
        }

        _monitorCts.Cancel();
        _monitorCts.Dispose();
        _monitorCts = null;
        StatusChanged?.Invoke(this, new MonitorStatusChangedEventArgs("Monitor stopped.", true));
    }

    public void ResetBaseline()
    {
        _knownStates.Clear();
        _productionDomainByProjectId.Clear();
        _hasBaseline = false;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            StatusChanged?.Invoke(this, new MonitorStatusChangedEventArgs("Refreshing deployments...", true));
            var settings = await _settingsStore.LoadAsync(cancellationToken);
            var client = new VercelApiClient(settings);
            var deployments = await client.GetDeploymentsAsync(30, cancellationToken);

            var ordered = deployments
                .OrderByDescending(deployment => deployment.CreatedAtMs ?? deployment.Created ?? 0)
                .ToList();

            await EnrichProductionUrlsAsync(client, ordered, cancellationToken);

            CurrentDeployments = ordered;
            DetectFinishedDeployments(CurrentDeployments);
            DeploymentsChanged?.Invoke(this, new DeploymentsChangedEventArgs(CurrentDeployments));
            StatusChanged?.Invoke(this, new MonitorStatusChangedEventArgs($"Last checked {DateTime.Now:HH:mm:ss}.", true));
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task EnrichProductionUrlsAsync(VercelApiClient client, IReadOnlyList<VercelDeployment> deployments, CancellationToken cancellationToken)
    {
        var projectIds = deployments
            .Select(deployment => deployment.ProjectId)
            .Where(projectId => !string.IsNullOrWhiteSpace(projectId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var projectId in projectIds)
        {
            if (!_productionDomainByProjectId.ContainsKey(projectId))
            {
                try
                {
                    var domains = await client.GetProjectProductionDomainsAsync(projectId, cancellationToken);
                    _productionDomainByProjectId[projectId] = SelectPreferredProductionDomainUrl(domains);
                }
                catch
                {
                    // Per-project domain lookup failures should not block deployment monitoring.
                    _productionDomainByProjectId[projectId] = string.Empty;
                }
            }
        }

        foreach (var deployment in deployments)
        {
            if (string.IsNullOrWhiteSpace(deployment.ProjectId))
            {
                continue;
            }

            if (_productionDomainByProjectId.TryGetValue(deployment.ProjectId, out var productionDomainUrl)
                && !string.IsNullOrWhiteSpace(productionDomainUrl))
            {
                deployment.ProductionDomainUrl = productionDomainUrl;
            }
        }
    }

    private static string SelectPreferredProductionDomainUrl(IReadOnlyList<VercelProjectDomain> domains)
    {
        if (domains.Count == 0)
        {
            return string.Empty;
        }

        var preferred = domains
            .Where(domain => domain.Verified)
            .OrderBy(domain => domain.IsDefaultVercelDomain)
            .ThenBy(domain => domain.Name.Length)
            .FirstOrDefault();

        if (preferred is null)
        {
            preferred = domains.FirstOrDefault();
        }

        return preferred?.Url ?? string.Empty;
    }

    private async Task RunMonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var settings = await _settingsStore.LoadAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), cancellationToken);
                await RefreshAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, new MonitorStatusChangedEventArgs($"Monitor error: {ex.Message}", false));
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            }
        }
    }

    private void DetectFinishedDeployments(IReadOnlyList<VercelDeployment> deployments)
    {
        foreach (var deployment in deployments)
        {
            var key = deployment.Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var newState = deployment.StateDisplay;
            var wasKnown = _knownStates.TryGetValue(key, out var previousState);
            var shouldNotify = _hasBaseline
                && deployment.IsFinal
                && (!wasKnown || !IsFinalState(previousState) || !string.Equals(previousState, newState, StringComparison.OrdinalIgnoreCase));

            _knownStates[key] = newState;

            if (shouldNotify)
            {
                _notifications.ShowDeploymentResult(deployment);
                DeploymentCompleted?.Invoke(this, new DeploymentCompletedEventArgs(deployment));
            }
        }

        _hasBaseline = true;
    }

    private static bool IsFinalState(string? state)
    {
        return string.Equals(state, "READY", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "ERROR", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "CANCELED", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        Stop();
    }
}

public sealed class DeploymentsChangedEventArgs : EventArgs
{
    public DeploymentsChangedEventArgs(IReadOnlyList<VercelDeployment> deployments)
    {
        Deployments = deployments;
    }

    public IReadOnlyList<VercelDeployment> Deployments { get; }
}

public sealed class MonitorStatusChangedEventArgs : EventArgs
{
    public MonitorStatusChangedEventArgs(string message, bool isHealthy)
    {
        Message = message;
        IsHealthy = isHealthy;
    }

    public string Message { get; }
    public bool IsHealthy { get; }
}

public sealed class DeploymentCompletedEventArgs : EventArgs
{
    public DeploymentCompletedEventArgs(VercelDeployment deployment)
    {
        Deployment = deployment;
    }

    public VercelDeployment Deployment { get; }
}
