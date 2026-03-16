using System.Diagnostics;
using System.Windows.Threading;
using LeatherMatchControl.Models;

namespace LeatherMatchControl.Services;

public class SchedulerService
{
    private readonly DockerService _dockerService;
    private readonly DispatcherTimer _timer;
    private AppSettings _settings = new();
    private bool _isBusy;

    // O gün için eylemin zaten tetiklenip tetiklenmediğini takip eder.
    // Key: "start" veya "stop", Value: tetiklendiği tarih (sadece gün kısmı).
    private readonly Dictionary<string, DateTime> _lastTriggered = new();

    public SchedulerService(DockerService dockerService)
    {
        _dockerService = dockerService;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += OnTimerTick;
    }

    public void Start(AppSettings settings)
    {
        _settings = settings;
        _timer.Start();

        // Hemen ilk kontrolü yap; bir dakika beklemeden tetikle.
        _ = CheckScheduleAsync();

        Debug.WriteLine($"[SchedulerService] Zamanlayıcı başlatıldı. " +
            $"AutoStart={settings.AutoStartEnabled} ({settings.StartTime}), " +
            $"AutoStop={settings.AutoStopEnabled} ({settings.StopTime})");
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;

        // Ayarlar değiştiğinde günlük tetiklenme geçmişini sıfırla
        // ki yeni saatler hemen geçerliliğe girebilsin.
        _lastTriggered.Clear();

        _ = CheckScheduleAsync();

        Debug.WriteLine($"[SchedulerService] Ayarlar güncellendi. " +
            $"AutoStart={settings.AutoStartEnabled} ({settings.StartTime}), " +
            $"AutoStop={settings.AutoStopEnabled} ({settings.StopTime})");
    }

    public void Stop()
    {
        _timer.Stop();
        Debug.WriteLine("[SchedulerService] Zamanlayıcı durduruldu.");
    }

    private async void OnTimerTick(object? sender, EventArgs e)
        => await CheckScheduleAsync();

    private async Task CheckScheduleAsync()
    {
        if (_isBusy) return;
        _isBusy = true;

        try
        {
            var now = DateTime.Now;

            if (_settings.AutoStartEnabled && ShouldTrigger("start", _settings.StartTime, now))
            {
                MarkTriggered("start", now);
                await TryStartServerAsync();
            }

            if (_settings.AutoStopEnabled && ShouldTrigger("stop", _settings.StopTime, now))
            {
                MarkTriggered("stop", now);
                await TryStopServerAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SchedulerService] Beklenmeyen hata: {ex.Message}");
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>
    /// Şu anki zaman, hedef saatten geçtiyse ve bugün henüz tetiklenmemişse true döner.
    /// </summary>
    private bool ShouldTrigger(string key, string targetTime, DateTime now)
    {
        if (!TimeOnly.TryParseExact(targetTime, "HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var target))
            return false;

        var currentTime = TimeOnly.FromDateTime(now);

        // Hedef saat geçildi mi?
        if (currentTime < target)
            return false;

        // Bugün zaten tetiklendi mi?
        if (_lastTriggered.TryGetValue(key, out var lastDate) && lastDate.Date == now.Date)
            return false;

        return true;
    }

    private void MarkTriggered(string key, DateTime when)
        => _lastTriggered[key] = when;


    private async Task TryStartServerAsync()
    {
        try
        {
            Debug.WriteLine($"[SchedulerService] Planlanan başlatma zamanı geldi ({_settings.StartTime}). Durum kontrol ediliyor...");

            var (status, _) = await _dockerService.CheckStatusAsync(_settings.ComposeWorkingDirectory);

            if (status == ServerStatus.Running || status == ServerStatus.Starting)
            {
                Debug.WriteLine("[SchedulerService] Sunucu zaten çalışıyor, başlatma atlandı.");
                return;
            }

            Debug.WriteLine("[SchedulerService] Sunucu başlatılıyor...");
            var (success, message) = await _dockerService.StartServerAsync(_settings.ComposeWorkingDirectory);
            Debug.WriteLine($"[SchedulerService] Başlatma sonucu: {success} — {message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SchedulerService] Otomatik başlatma hatası: {ex.Message}");
        }
    }

    private async Task TryStopServerAsync()
    {
        try
        {
            Debug.WriteLine($"[SchedulerService] Planlanan durdurma zamanı geldi ({_settings.StopTime}). Durum kontrol ediliyor...");

            var (status, _) = await _dockerService.CheckStatusAsync(_settings.ComposeWorkingDirectory);

            if (status == ServerStatus.Stopped || status == ServerStatus.Unknown)
            {
                Debug.WriteLine("[SchedulerService] Sunucu zaten durmuş, durdurma atlandı.");
                return;
            }

            Debug.WriteLine("[SchedulerService] Sunucu durduruluyor...");
            var (success, message) = await _dockerService.StopServerAsync(_settings.ComposeWorkingDirectory);
            Debug.WriteLine($"[SchedulerService] Durdurma sonucu: {success} — {message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SchedulerService] Otomatik durdurma hatası: {ex.Message}");
        }
    }
}
