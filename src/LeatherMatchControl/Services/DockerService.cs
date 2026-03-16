using System.Diagnostics;
using LeatherMatchControl.Models;

namespace LeatherMatchControl.Services;

public class DockerService
{
    private const int StatusTimeoutMs = 8000;
    private const int ActionTimeoutMs = 30000;

    public async Task<(ServerStatus Status, string Message)> CheckStatusAsync(string workingDirectory)
    {
        try
        {
            var (exitCode, stdout, stderr) = await RunCommandAsync(
                "docker", "compose ps --format json", workingDirectory, StatusTimeoutMs);

            if (exitCode != 0)
            {
                if (stderr.Contains("no configuration file provided", StringComparison.OrdinalIgnoreCase) ||
                    stderr.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return (ServerStatus.Error, "docker-compose.yml bulunamadı");
                }
                return (ServerStatus.Error, $"Docker hatası: {stderr.Trim()}");
            }

            var output = stdout.Trim();
            if (string.IsNullOrEmpty(output))
            {
                return (ServerStatus.Stopped, "Container bulunamadı");
            }

            if (output.Contains("running", StringComparison.OrdinalIgnoreCase))
            {
                return (ServerStatus.Running, "Container çalışıyor");
            }

            if (output.Contains("exited", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("dead", StringComparison.OrdinalIgnoreCase))
            {
                return (ServerStatus.Stopped, "Container durdurulmuş");
            }

            if (output.Contains("restarting", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("created", StringComparison.OrdinalIgnoreCase))
            {
                return (ServerStatus.Starting, "Container başlatılıyor...");
            }

            return (ServerStatus.Unknown, output);
        }
        catch (TimeoutException)
        {
            return (ServerStatus.Error, "Docker yanıt vermedi (zaman aşımı)");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (ex.Message.Contains("docker", StringComparison.OrdinalIgnoreCase) ||
                ex is System.ComponentModel.Win32Exception)
            {
                return (ServerStatus.Error, "Docker bulunamadı. Docker Desktop kurulu ve çalışıyor mu?");
            }
            return (ServerStatus.Error, $"Hata: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> StartServerAsync(string workingDirectory)
    {
        try
        {
            var (exitCode, stdout, stderr) = await RunCommandAsync(
                "docker", "compose up -d", workingDirectory, ActionTimeoutMs);

            if (exitCode == 0)
                return (true, "Sunucu başlatıldı");

            return (false, $"Başlatma hatası: {(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr).Trim()}");
        }
        catch (TimeoutException)
        {
            return (false, "Docker komutu zaman aşımına uğradı");
        }
        catch (Exception ex)
        {
            return (false, $"Hata: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> StopServerAsync(string workingDirectory)
    {
        try
        {
            var (exitCode, stdout, stderr) = await RunCommandAsync(
                "docker", "compose down", workingDirectory, ActionTimeoutMs);

            if (exitCode == 0)
                return (true, "Sunucu durduruldu");

            return (false, $"Durdurma hatası: {(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr).Trim()}");
        }
        catch (TimeoutException)
        {
            return (false, "Docker komutu zaman aşımına uğradı");
        }
        catch (Exception ex)
        {
            return (false, $"Hata: {ex.Message}");
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCommandAsync(
        string fileName, string arguments, string workingDirectory, int timeoutMs)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Komut {timeoutMs / 1000} saniye içinde tamamlanamadı");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
    }
}
