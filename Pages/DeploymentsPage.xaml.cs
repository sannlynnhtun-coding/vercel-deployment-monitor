using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VercelDeploymentMonitor.Models;
using VercelDeploymentMonitor.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace VercelDeploymentMonitor.Pages;

public sealed partial class DeploymentsPage : Page
{
    private VercelDeployment? _selectedDeployment;

    public ObservableCollection<VercelDeployment> Deployments { get; } = new();

    public DeploymentsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        App.Monitor.DeploymentsChanged += Monitor_DeploymentsChanged;
        App.Monitor.StatusChanged += Monitor_StatusChanged;
        App.Monitor.DeploymentCompleted += Monitor_DeploymentCompleted;

        ApplyDeployments(App.Monitor.CurrentDeployments);
        UpdateMonitorButtons();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        App.Monitor.DeploymentsChanged -= Monitor_DeploymentsChanged;
        App.Monitor.StatusChanged -= Monitor_StatusChanged;
        App.Monitor.DeploymentCompleted -= Monitor_DeploymentCompleted;
        base.OnNavigatedFrom(e);
    }

    private async void StartMonitoring_Click(object sender, RoutedEventArgs e)
    {
        await RunPageActionAsync(async () =>
        {
            await App.Monitor.StartAsync();
            UpdateMonitorButtons();
            ShowStatus("Monitor", "Polling Vercel for deployment status changes.", InfoBarSeverity.Success);
        });
    }

    private void StopMonitoring_Click(object sender, RoutedEventArgs e)
    {
        App.Monitor.Stop();
        UpdateMonitorButtons();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RunPageActionAsync(async () => await App.Monitor.RefreshAsync());
    }

    private async void DeploymentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedDeployment = DeploymentsList.SelectedItem as VercelDeployment;
        OpenDeploymentButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedDeployment?.DisplayUrl);
        OpenProductionButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedDeployment?.ProductionUrl);
        OpenDashboardButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedDeployment?.DashboardUrl);
        CopyLogsButton.IsEnabled = false;

        if (_selectedDeployment is null)
        {
            SelectedDeploymentText.Text = "Select a deployment to load build + deployment events.";
            LogsTextBox.Text = string.Empty;
            return;
        }

        var productionHint = string.IsNullOrWhiteSpace(_selectedDeployment.ProductionUrl)
            ? "no production domain found"
            : _selectedDeployment.ProductionUrl;
        SelectedDeploymentText.Text = $"{_selectedDeployment.Name} / {_selectedDeployment.StatusLabel} / {productionHint}";
        await LoadLogsAsync(_selectedDeployment);
    }

    private async Task LoadLogsAsync(VercelDeployment deployment)
    {
        await RunLogActionAsync(async () =>
        {
            LogsTextBox.Text = "Loading build logs from Vercel...";
            var settings = await App.SettingsStore.LoadAsync();
            var client = new VercelApiClient(settings);
            var events = await client.GetDeploymentEventsAsync(deployment.Key, 200);

            if (events.Count == 0)
            {
                LogsTextBox.Text = "Vercel returned no build events for this deployment.";
            }
            else
            {
                var lines = events
                    .Where(logEvent => logEvent.ShouldDisplay)
                    .OrderBy(logEvent => logEvent.EventTime)
                    .Select(logEvent => logEvent.DisplayLine)
                    .ToList();

                LogsTextBox.Text = lines.Count == 0
                    ? "Vercel returned events, but none included readable log lines."
                    : string.Join(Environment.NewLine, lines);
            }

            CopyLogsButton.IsEnabled = !string.IsNullOrWhiteSpace(LogsTextBox.Text);
            LogsInfoBar.IsOpen = false;
        });
    }

    private async void OpenDeployment_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_selectedDeployment?.DisplayUrl))
        {
            await Launcher.LaunchUriAsync(new Uri(_selectedDeployment.DisplayUrl));
        }
    }

    private async void OpenProduction_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_selectedDeployment?.ProductionUrl))
        {
            await Launcher.LaunchUriAsync(new Uri(_selectedDeployment.ProductionUrl));
        }
    }

    private async void OpenDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_selectedDeployment?.DashboardUrl))
        {
            await Launcher.LaunchUriAsync(new Uri(_selectedDeployment.DashboardUrl));
        }
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(LogsTextBox.Text);
        Clipboard.SetContent(package);
        ShowLogsInfo("Copied", "Deployment logs copied to the clipboard.", InfoBarSeverity.Success);
    }

    private async Task RunPageActionAsync(Func<Task> action)
    {
        SetBusy(true);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowStatus("Vercel request failed", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            SetBusy(false);
            UpdateMonitorButtons();
        }
    }

    private async Task RunLogActionAsync(Func<Task> action)
    {
        SetBusy(true);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            LogsTextBox.Text = string.Empty;
            ShowLogsInfo("Could not load logs", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Monitor_DeploymentsChanged(object? sender, DeploymentsChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => ApplyDeployments(e.Deployments));
    }

    private void Monitor_StatusChanged(object? sender, MonitorStatusChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            LastRefreshText.Text = e.Message;
            ShowStatus(e.IsHealthy ? "Monitor" : "Monitor issue", e.Message, e.IsHealthy ? InfoBarSeverity.Informational : InfoBarSeverity.Error);
        });
    }

    private void Monitor_DeploymentCompleted(object? sender, DeploymentCompletedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var severity = e.Deployment.IsReady ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ShowStatus("Deployment finished", $"{e.Deployment.Name} is {e.Deployment.StatusLabel}.", severity);
        });
    }

    private void ApplyDeployments(IReadOnlyList<VercelDeployment> deployments)
    {
        Deployments.Clear();
        foreach (var deployment in deployments)
        {
            Deployments.Add(deployment);
        }

        TotalCountText.Text = Deployments.Count.ToString("N0");
        BuildingCountText.Text = Deployments.Count(deployment => deployment.IsBuilding).ToString("N0");
        ReadyCountText.Text = Deployments.Count(deployment => deployment.IsReady).ToString("N0");
        FailedCountText.Text = Deployments.Count(deployment => deployment.IsFailed).ToString("N0");
    }

    private void UpdateMonitorButtons()
    {
        StartButton.IsEnabled = !App.Monitor.IsRunning;
        StopButton.IsEnabled = App.Monitor.IsRunning;
    }

    private void SetBusy(bool isBusy)
    {
        BusyRing.IsActive = isBusy;
        RefreshButton.IsEnabled = !isBusy;
    }

    private void ShowStatus(string title, string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Title = title;
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    private void ShowLogsInfo(string title, string message, InfoBarSeverity severity)
    {
        LogsInfoBar.Title = title;
        LogsInfoBar.Message = message;
        LogsInfoBar.Severity = severity;
        LogsInfoBar.IsOpen = true;
    }
}
