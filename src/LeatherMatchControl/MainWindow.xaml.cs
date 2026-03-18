using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using LeatherMatchControl.Models;
using LeatherMatchControl.Services;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using MessageBox = System.Windows.MessageBox;

namespace LeatherMatchControl;

public partial class MainWindow : System.Windows.Window
{
    private readonly DockerService _dockerService = new();
    private readonly HealthCheckService _healthCheckService = new();
    private readonly SettingsService _settingsService = new();
    private AppSettings _settings = null!;
    private DispatcherTimer? _autoRefreshTimer;
    private SchedulerService? _schedulerService;
    private bool _isBusy;

    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0x4C, 0xAF, 0x50));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(0xF4, 0x43, 0x36));
    private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(0xFF, 0xC1, 0x07));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(0x9E, 0x9E, 0x9E));

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _settings = _settingsService.Load();
        UpdatePathDisplay();
        UpdateScheduleDisplay();
        StartAutoRefresh();
        _schedulerService = new SchedulerService(_dockerService);
        _schedulerService.Start(_settings);
        _ = CheckStatusAsync();
    }

    private void StartAutoRefresh()
    {
        _autoRefreshTimer?.Stop();
        _autoRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(5, _settings.AutoRefreshIntervalSeconds))
        };
        _autoRefreshTimer.Tick += async (_, _) => await CheckStatusAsync();
        _autoRefreshTimer.Start();
    }

    private async Task CheckStatusAsync()
    {
        if (_isBusy) return;

        var workDir = _settings.ComposeWorkingDirectory;

        if (!Directory.Exists(workDir))
        {
            UpdateUI(ServerStatus.Error, "Klasör bulunamadı", $"Yol mevcut değil: {workDir}");
            return;
        }

        if (!HasComposeFile(workDir))
        {
            UpdateUI(ServerStatus.Error, "Compose dosyası yok",
                $"docker-compose.yml bulunamadı: {workDir}");
            return;
        }

        var dockerTask = _dockerService.CheckStatusAsync(workDir);
        var healthTask = _healthCheckService.CheckHealthAsync(_settings.HealthCheckUrl);

        await Task.WhenAll(dockerTask, healthTask);

        var (dockerStatus, dockerMessage) = dockerTask.Result;
        var (isHealthy, healthMessage) = healthTask.Result;

        if (dockerStatus == ServerStatus.Running && !isHealthy)
            dockerStatus = ServerStatus.Starting;

        var detail = dockerStatus == ServerStatus.Running || dockerStatus == ServerStatus.Starting
            ? $"{dockerMessage} | {healthMessage}"
            : dockerMessage;

        UpdateUI(dockerStatus, GetStatusDisplayText(dockerStatus), detail);
    }

    private void UpdateUI(ServerStatus status, string statusText, string detail)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = statusText;
            DetailText.Text = detail;
            LastCheckText.Text = $"Son kontrol: {DateTime.Now:HH:mm:ss}";

            var (brush, shadowColor) = status switch
            {
                ServerStatus.Running => (GreenBrush, Colors.Green),
                ServerStatus.Stopped => (RedBrush, Colors.Red),
                ServerStatus.Starting => (YellowBrush, Colors.Orange),
                ServerStatus.Error => (RedBrush, Colors.Red),
                _ => (GrayBrush, Colors.Gray)
            };

            StatusIndicator.Fill = brush;
            if (StatusIndicator.Effect is DropShadowEffect shadow)
            {
                shadow.Color = shadowColor;
            }
        });
    }

    private static string GetStatusDisplayText(ServerStatus status) => status switch
    {
        ServerStatus.Running => "Sunucu Çalışıyor",
        ServerStatus.Stopped => "Sunucu Durduruldu",
        ServerStatus.Starting => "Sunucu Başlatılıyor...",
        ServerStatus.Error => "Hata",
        _ => "Bilinmiyor"
    };

    private async void BtnStart_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isBusy) return;
        SetBusy(true, "Sunucu başlatılıyor...");

        try
        {
            var (success, message) = await _dockerService.StartServerAsync(_settings.ComposeWorkingDirectory);

            if (success)
            {
                UpdateUI(ServerStatus.Starting, "Başlatılıyor...", message);
            }
            else
            {
                MessageBox.Show(message, "Başlatma Hatası", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }

            await CheckStatusAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void BtnStop_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isBusy) return;

        var result = MessageBox.Show(
            "Sunucuyu durdurmak istediğinize emin misiniz?",
            "Onay",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        SetBusy(true, "Sunucu durduruluyor...");

        try
        {
            var (success, message) = await _dockerService.StopServerAsync(_settings.ComposeWorkingDirectory);

            if (!success)
            {
                MessageBox.Show(message, "Durdurma Hatası", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }

            await CheckStatusAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void BtnRefresh_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isBusy) return;
        SetBusy(true, "Kontrol ediliyor...");

        try
        {
            await CheckStatusAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void BtnSettings_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings) { Owner = this };

        if (settingsWindow.ShowDialog() == true)
        {
            _settings = settingsWindow.Settings;
            _settingsService.Save(_settings);
            UpdatePathDisplay();
            UpdateScheduleDisplay();
            _schedulerService?.UpdateSettings(_settings);
            StartAutoRefresh();
            _ = CheckStatusAsync();
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _isBusy = busy;
        BtnStart.IsEnabled = !busy;
        BtnStop.IsEnabled = !busy;
        BtnRefresh.IsEnabled = !busy;
        BtnSettings.IsEnabled = !busy;

        if (busy && message != null)
        {
            StatusText.Text = message;
        }
    }

    private void UpdatePathDisplay()
    {
        PathText.Text = $"Klasör: {_settings.ComposeWorkingDirectory}";

        var url = _settings.HealthCheckUrl?.Trim();
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                var uri = new Uri(url);
                var localIp = GetLocalIpAddress();
                var portPart = uri.IsDefaultPort ? "" : $":{uri.Port}";
                var localAddress = $"{uri.Scheme}://{localIp}{portPart}";
                ServerAddressText.Text = $"Sunucu: {localAddress}";
            }
            catch
            {
                ServerAddressText.Text = $"Sunucu: {url}";
            }
        }
        else
        {
            ServerAddressText.Text = "Sunucu adresi: ayarlı değil";
        }
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
        }
        catch
        {
            return "localhost";
        }
    }

    private void UpdateScheduleDisplay()
    {
        ScheduledStartText.Text = _settings.AutoStartEnabled
            ? $"Planlanan açılış: {_settings.StartTime}"
            : "Planlanan açılış: ayarlı değil";

        ScheduledStopText.Text = _settings.AutoStopEnabled
            ? $"Planlanan kapanış: {_settings.StopTime}"
            : "Planlanan kapanış: ayarlı değil";
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _schedulerService?.Stop();
        base.OnClosing(e);
    }

    private static bool HasComposeFile(string directory)
    {
        string[] composeNames = [
            "docker-compose.yml",
            "docker-compose.yaml",
            "compose.yml",
            "compose.yaml"
        ];
        return composeNames.Any(name => File.Exists(Path.Combine(directory, name)));
    }
}
