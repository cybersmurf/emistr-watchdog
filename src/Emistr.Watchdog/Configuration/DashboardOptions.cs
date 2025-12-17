namespace Emistr.Watchdog.Configuration;

/// <summary>
/// Configuration for the web dashboard.
/// </summary>
public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    /// <summary>
    /// Whether the dashboard is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Port for the dashboard web server.
    /// </summary>
    public int Port { get; set; } = 5080;

    /// <summary>
    /// Whether to use HTTPS. Default is true.
    /// </summary>
    public bool UseHttps { get; set; } = true;

    /// <summary>
    /// Whether to require authentication for dashboard access.
    /// </summary>
    public bool RequireAuthentication { get; set; }

    /// <summary>
    /// Dashboard page title.
    /// </summary>
    public string Title { get; set; } = "Emistr System Monitor";

    /// <summary>
    /// How often to push updates to clients (in seconds).
    /// </summary>
    public int UpdateIntervalSeconds { get; set; } = 5;
}
