using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VercelDeploymentMonitor.Models;
using VercelDeploymentMonitor.Services;

namespace VercelDeploymentMonitor.Pages;

public sealed partial class SettingsPage : Page
{
    public ObservableCollection<VercelProject> Projects { get; } = new();

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        var settings = await App.SettingsStore.LoadAsync();
        TokenBox.Password = settings.ApiToken;
        TeamIdBox.Text = settings.TeamId;
        TeamSlugBox.Text = settings.TeamSlug;
        ProjectFilterBox.Text = settings.ProjectFilter;
        PollIntervalBox.Value = settings.PollIntervalSeconds;
        SettingsPathBox.Text = App.SettingsStore.SettingsPath;
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        await SaveSettingsFromFormAsync();
        ShowSettingsInfo("Saved", "Vercel settings saved locally.", InfoBarSeverity.Success);
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        await RunSettingsActionAsync(async () =>
        {
            var settings = await SaveSettingsFromFormAsync();
            var client = new VercelApiClient(settings);
            var deployments = await client.GetDeploymentsAsync(3);
            ShowSettingsInfo("Connection works", $"Vercel returned {deployments.Count} deployment item(s).", InfoBarSeverity.Success);
        });
    }

    private async void LoadProjects_Click(object sender, RoutedEventArgs e)
    {
        await RunSettingsActionAsync(async () =>
        {
            var settings = await SaveSettingsFromFormAsync();
            var client = new VercelApiClient(settings);
            var projects = await client.GetProjectsAsync();

            Projects.Clear();
            foreach (var project in projects.OrderBy(project => project.Name))
            {
                Projects.Add(project);
            }

            ShowSettingsInfo("Projects loaded", $"Loaded {Projects.Count} project(s) from Vercel.", InfoBarSeverity.Success);
        });
    }

    private void ProjectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectsList.SelectedItem is VercelProject project)
        {
            ProjectFilterBox.Text = project.Id;
            ShowSettingsInfo("Project selected", $"Using {project.Name} ({project.Id}) as the deployment filter.", InfoBarSeverity.Informational);
        }
    }

    private async Task<VercelMonitorSettings> SaveSettingsFromFormAsync()
    {
        var settings = new VercelMonitorSettings
        {
            ApiToken = TokenBox.Password,
            TeamId = TeamIdBox.Text,
            TeamSlug = TeamSlugBox.Text,
            ProjectFilter = ProjectFilterBox.Text,
            PollIntervalSeconds = double.IsNaN(PollIntervalBox.Value) ? 20 : (int)Math.Round(PollIntervalBox.Value)
        };

        await App.SettingsStore.SaveAsync(settings);
        App.Monitor.ResetBaseline();
        return settings;
    }

    private async Task RunSettingsActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowSettingsInfo("Vercel request failed", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void ShowSettingsInfo(string title, string message, InfoBarSeverity severity)
    {
        SettingsInfoBar.Title = title;
        SettingsInfoBar.Message = message;
        SettingsInfoBar.Severity = severity;
        SettingsInfoBar.IsOpen = true;
    }
}
