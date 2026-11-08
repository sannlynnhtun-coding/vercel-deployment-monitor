using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using VercelDeploymentMonitor.Models;
using Windows.System;

namespace VercelDeploymentMonitor.Services;

public sealed class NotificationService
{
    private bool _registered;

    public void Initialize()
    {
        try
        {
            var manager = AppNotificationManager.Default;
            manager.NotificationInvoked += NotificationManager_NotificationInvoked;
            manager.Register();
            _registered = true;
            _ = TryHandleLaunchActivationAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App notification registration failed: {ex.Message}");
        }
    }

    public void ShowDeploymentResult(VercelDeployment deployment)
    {
        if (!_registered)
        {
            return;
        }

        try
        {
            var title = deployment.IsReady
                ? "Vercel deployment succeeded"
                : "Vercel deployment failed";

            var launchUrl = deployment.NotificationLaunchUrl;
            var urlText = deployment.IsReady
                ? (string.IsNullOrWhiteSpace(deployment.ProductionUrl) ? deployment.DisplayUrl : deployment.ProductionUrl)
                : launchUrl;

            var builder = new AppNotificationBuilder()
                .AddArgument("action", "openDeploymentUrl")
                .AddArgument("url", launchUrl)
                .AddText(title)
                .AddText($"{deployment.Name} is {deployment.StatusLabel}")
                .AddText(string.IsNullOrWhiteSpace(urlText) ? deployment.Summary : urlText);

            if (!string.IsNullOrWhiteSpace(launchUrl))
            {
                builder.AddButton(new AppNotificationButton("Open deployment")
                    .AddArgument("action", "openDeploymentUrl")
                    .AddArgument("url", launchUrl));
            }

            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Showing app notification failed: {ex.Message}");
        }
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        try
        {
            AppNotificationManager.Default.NotificationInvoked -= NotificationManager_NotificationInvoked;
            AppNotificationManager.Default.Unregister();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App notification unregister failed: {ex.Message}");
        }
    }

    private static void NotificationManager_NotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        _ = HandleNotificationActivationAsync(args);
    }

    private static async Task TryHandleLaunchActivationAsync()
    {
        try
        {
            var activatedArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activatedArgs.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.AppNotification
                && activatedArgs.Data is AppNotificationActivatedEventArgs notificationArgs)
            {
                await HandleNotificationActivationAsync(notificationArgs);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Reading notification launch activation failed: {ex.Message}");
        }
    }

    private static async Task HandleNotificationActivationAsync(AppNotificationActivatedEventArgs args)
    {
        try
        {
            if (!args.Arguments.TryGetValue("action", out var action)
                || !string.Equals(action, "openDeploymentUrl", StringComparison.OrdinalIgnoreCase)
                || !args.Arguments.TryGetValue("url", out var url)
                || string.IsNullOrWhiteSpace(url)
                || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return;
            }

            await Launcher.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Handling notification activation failed: {ex.Message}");
        }
    }
}
