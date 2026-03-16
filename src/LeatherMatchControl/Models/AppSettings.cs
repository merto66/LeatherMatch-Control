namespace LeatherMatchControl.Models;

public class AppSettings
{
    public string ComposeWorkingDirectory { get; set; } = @"C:\LeatherMatch";
    public string HealthCheckUrl { get; set; } = "http://localhost:8080/api/health";
    public int AutoRefreshIntervalSeconds { get; set; } = 15;
    public bool AutoStartEnabled { get; set; } = false;
    public bool AutoStopEnabled { get; set; } = false;
    public string StartTime { get; set; } = "09:00";
    public string StopTime { get; set; } = "18:00";
}
