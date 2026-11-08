using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using VercelDeploymentMonitor.Pages;

namespace VercelDeploymentMonitor;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var loader = new ResourceLoader();
        var appDisplayName = loader.GetString("AppDisplayName");
        Title = appDisplayName;
        AppTitleBar.Title = appDisplayName;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        if (NavFrame.Content is null)
        {
            NavFrame.Navigate(typeof(DeploymentsPage));
        }
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
        {
            NavFrame.GoBack();
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
        }
        else if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            switch (tag)
            {
                case "deployments":
                    NavFrame.Navigate(typeof(DeploymentsPage));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown navigation item tag: {tag}");
            }
        }
    }
}
