namespace Emistr.Watchdog.Configuration;

public class ServiceRestartConfig
{
    public bool Enabled { get; set; }
    public string WindowsServiceName { get; set; } = string.Empty;
    public int MaxRestartAttempts { get; set; } = 3;
    public int RestartDelaySeconds { get; set; } = 30;
    public bool RestartOnCritical { get; set; } = true;
}
