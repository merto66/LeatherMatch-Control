using System.Net.Http;

namespace LeatherMatchControl.Services;

public class HealthCheckService
{
    private readonly HttpClient _httpClient;

    public HealthCheckService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
    }

    public async Task<(bool IsHealthy, string Message)> CheckHealthAsync(string healthUrl)
    {
        try
        {
            var response = await _httpClient.GetAsync(healthUrl);

            if (response.IsSuccessStatusCode)
                return (true, "API sağlıklı çalışıyor");

            return (false, $"API yanıt verdi ama durum kodu: {(int)response.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            return (false, "API zaman aşımı");
        }
        catch (HttpRequestException)
        {
            return (false, "API'ye erişilemiyor");
        }
        catch (Exception ex)
        {
            return (false, $"Sağlık kontrolü hatası: {ex.Message}");
        }
    }
}
