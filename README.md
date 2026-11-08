# Vercel Deployment Monitor

WinUI 3 desktop app for watching Vercel deployments from Windows.

## What it does

- Lists recent Vercel deployments with status, branch, creator, target, and URL.
- Shows taskbar loading (indeterminate progress) while any deployment is `QUEUED`, `INITIALIZING`, or `BUILDING`.
- Polls Vercel on an interval and sends a local Windows notification when a deployment finishes as `READY`, `ERROR`, or `CANCELED`.
- Resolves each project's production domain and uses it as the preferred open URL.
- Clicking a successful deployment notification opens the production domain URL in your default browser.
- Loads build/deployment events for the selected deployment.
- Lets you scope requests to a Vercel team and one project.

## Why polling instead of direct webhooks?

Vercel webhooks are the true push option, but webhook receivers must be reachable through a public HTTPS URL. A WinUI desktop app running on a private Windows machine usually is not publicly reachable, so this app uses the Vercel REST API from the desktop app.

If you want true push later, add a small public relay service that receives Vercel webhook events such as deployment succeeded/error/cancelled, then forwards them to the desktop app through SignalR, WebSockets, Azure Notification Hubs, or another desktop-friendly channel.

## Setup

1. Create a Vercel API token in Vercel Account Settings > Tokens.
2. Run the app.
3. Open Settings.
4. Paste the token.
5. Optional: enter either `team_...` in Team ID or your team slug.
6. Optional: click Load projects and select a project. This fills the Project filter with the project id.
7. Save settings.
8. Go back to Deployments and click Refresh now or Start monitor.

The Vercel token is stored in Windows Credential Locker. Non-secret settings are saved to `%LocalAppData%\\VercelDeploymentMonitor\\settings.json`.

## Vercel API endpoints used

- `GET /v6/deployments` for the deployment list.
- `GET /v3/deployments/{idOrUrl}/events` for build logs/events.
- `GET /v10/projects` for project picker support.
- `GET /v9/projects/{idOrName}/domains?production=true&redirects=false` for production domain resolution.

## Build

```powershell
dotnet build
```

## Run

```powershell
dotnet run
```




