using Microsoft.UI.Xaml;
using VercelDeploymentMonitor.Services;

namespace VercelDeploymentMonitor;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public static AppSettingsStore SettingsStore { get; } = new();
    public static NotificationService Notifications { get; } = new();
    public static TaskbarProgressService TaskbarProgress { get; } = new();
    public static DeploymentMonitorService Monitor { get; } = new(SettingsStore, Notifications);

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Notifications.Initialize();

        _window = new MainWindow();
        TaskbarProgress.Initialize(_window);
        Monitor.DeploymentsChanged += Monitor_DeploymentsChanged;
        Monitor.StatusChanged += Monitor_StatusChanged;

        _window.Closed += (_, _) =>
        {
            Monitor.DeploymentsChanged -= Monitor_DeploymentsChanged;
            Monitor.StatusChanged -= Monitor_StatusChanged;
            Monitor.Dispose();
            TaskbarProgress.Dispose();
            Notifications.Unregister();
        };

        _window.Activate();
    }

    private void Monitor_DeploymentsChanged(object? sender, DeploymentsChangedEventArgs e)
    {
        if (_window is null)
        {
            return;
        }

        var hasBuildingDeployment = e.Deployments.Any(deployment => deployment.IsBuilding);
        _window.DispatcherQueue.TryEnqueue(() => TaskbarProgress.SetBusy(hasBuildingDeployment));
    }

    private void Monitor_StatusChanged(object? sender, MonitorStatusChangedEventArgs e)
    {
        if (_window is null || Monitor.IsRunning)
        {
            return;
        }

        _window.DispatcherQueue.TryEnqueue(() => TaskbarProgress.SetBusy(false));
    }
}
