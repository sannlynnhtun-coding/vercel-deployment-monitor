using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using VercelDeploymentMonitor.Models;

namespace VercelDeploymentMonitor.Services;

public sealed class VercelApiClient
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.vercel.com")
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly VercelMonitorSettings _settings;

    public VercelApiClient(VercelMonitorSettings settings)
    {
        _settings = settings;
    }

    public async Task<IReadOnlyList<VercelDeployment>> GetDeploymentsAsync(int limit = 30, CancellationToken cancellationToken = default)
    {
        var query = CreateScopedQuery();
        query["limit"] = limit.ToString();

        if (!string.IsNullOrWhiteSpace(_settings.ProjectFilter))
        {
            var projectFilter = _settings.ProjectFilter.Trim();
            query[projectFilter.StartsWith("prj_", StringComparison.OrdinalIgnoreCase) ? "projectId" : "app"] = projectFilter;
        }

        var response = await GetAsync<VercelDeploymentsResponse>("/v6/deployments", query, cancellationToken);
        return response?.Deployments is { } deployments ? deployments : Array.Empty<VercelDeployment>();
    }

    public async Task<IReadOnlyList<VercelDeploymentEvent>> GetDeploymentEventsAsync(string deploymentIdOrUrl, int limit = 300, CancellationToken cancellationToken = default)
    {
        var query = CreateScopedQuery();
        query["builds"] = "1";
        query["direction"] = "forward";
        query["limit"] = limit.ToString();

        var path = $"/v3/deployments/{Uri.EscapeDataString(deploymentIdOrUrl)}/events";
        var response = await GetAsync<List<VercelDeploymentEvent>>(path, query, cancellationToken);
        return response is { } events ? events : Array.Empty<VercelDeploymentEvent>();
    }

    public async Task<IReadOnlyList<VercelProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var query = CreateScopedQuery();
        query["limit"] = "100";

        var response = await GetAsync<VercelProjectsResponse>("/v10/projects", query, cancellationToken);
        return response?.Projects is { } projects ? projects : Array.Empty<VercelProject>();
    }

    public async Task<IReadOnlyList<VercelProjectDomain>> GetProjectProductionDomainsAsync(string projectIdOrName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectIdOrName))
        {
            return Array.Empty<VercelProjectDomain>();
        }

        var query = CreateScopedQuery();
        query["production"] = "true";
        query["redirects"] = "false";
        query["limit"] = "100";

        var path = $"/v9/projects/{Uri.EscapeDataString(projectIdOrName)}/domains";
        var response = await GetAsync<VercelProjectDomainsResponse>(path, query, cancellationToken);
        return response?.Domains is { } domains ? domains : Array.Empty<VercelProjectDomain>();
    }

    private async Task<T?> GetAsync<T>(string path, Dictionary<string, string> query, CancellationToken cancellationToken)
    {
        EnsureToken();

        var requestUri = BuildUri(path, query);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiToken);
        request.Headers.UserAgent.ParseAdd("VercelDeploymentMonitor/1.0");

        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new VercelApiException(response.StatusCode, body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private Dictionary<string, string> CreateScopedQuery()
    {
        var query = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(_settings.TeamId))
        {
            query["teamId"] = _settings.TeamId.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(_settings.TeamSlug))
        {
            query["slug"] = _settings.TeamSlug.Trim();
        }

        return query;
    }

    private void EnsureToken()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiToken))
        {
            throw new InvalidOperationException("Add a Vercel API token in Settings before refreshing deployments.");
        }
    }

    private static string BuildUri(string path, Dictionary<string, string> query)
    {
        var queryString = string.Join("&", query
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return string.IsNullOrWhiteSpace(queryString) ? path : $"{path}?{queryString}";
    }
}

public sealed class VercelApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ResponseBody { get; }

    public VercelApiException(HttpStatusCode statusCode, string responseBody)
        : base($"Vercel API returned {(int)statusCode} {statusCode}. {TrimBody(responseBody)}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    private static string TrimBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        return body.Length <= 500 ? body : body[..500];
    }
}
