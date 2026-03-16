using System.IO;
using System.Windows;
using LeatherMatchControl.Models;
using MessageBox = System.Windows.MessageBox;

namespace LeatherMatchControl;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; private set; }

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        Settings = current;
        LoadValues();
    }

    private void LoadValues()
    {
        TxtComposePath.Text = Settings.ComposeWorkingDirectory;
        TxtHealthUrl.Text = Settings.HealthCheckUrl;
        TxtRefreshInterval.Text = Settings.AutoRefreshIntervalSeconds.ToString();

        ChkAutoStart.IsChecked = Settings.AutoStartEnabled;
        TxtStartTime.Text = Settings.StartTime;

        ChkAutoStop.IsChecked = Settings.AutoStopEnabled;
        TxtStopTime.Text = Settings.StopTime;

        UpdateStartTimeRowState(Settings.AutoStartEnabled);
        UpdateStopTimeRowState(Settings.AutoStopEnabled);
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "docker-compose.yml dosyasını seçin",
            Filter = "Docker Compose|docker-compose.yml;docker-compose.yaml;compose.yml;compose.yaml|Tüm dosyalar|*.*",
            InitialDirectory = Directory.Exists(Settings.ComposeWorkingDirectory)
                ? Settings.ComposeWorkingDirectory
                : @"C:\"
        };

        if (dialog.ShowDialog(this) == true)
        {
            TxtComposePath.Text = Path.GetDirectoryName(dialog.FileName)!;
        }
    }

    private void ChkAutoStart_Changed(object sender, RoutedEventArgs e)
        => UpdateStartTimeRowState(ChkAutoStart.IsChecked == true);

    private void ChkAutoStop_Changed(object sender, RoutedEventArgs e)
        => UpdateStopTimeRowState(ChkAutoStop.IsChecked == true);

    private void UpdateStartTimeRowState(bool enabled)
    {
        StartTimeRow.Opacity = enabled ? 1.0 : 0.35;
        TxtStartTime.IsEnabled = enabled;
    }

    private void UpdateStopTimeRowState(bool enabled)
    {
        StopTimeRow.Opacity = enabled ? 1.0 : 0.35;
        TxtStopTime.IsEnabled = enabled;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInputs(out var errorMessage))
        {
            MessageBox.Show(errorMessage, "Geçersiz Değer",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Settings.ComposeWorkingDirectory = TxtComposePath.Text.Trim();
        Settings.HealthCheckUrl = TxtHealthUrl.Text.Trim();
        Settings.AutoRefreshIntervalSeconds = int.Parse(TxtRefreshInterval.Text.Trim());
        Settings.AutoStartEnabled = ChkAutoStart.IsChecked == true;
        Settings.StartTime = TxtStartTime.Text.Trim();
        Settings.AutoStopEnabled = ChkAutoStop.IsChecked == true;
        Settings.StopTime = TxtStopTime.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private bool ValidateInputs(out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(TxtComposePath.Text))
        {
            errorMessage = "Docker Compose yolu boş olamaz.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(TxtHealthUrl.Text))
        {
            errorMessage = "Health Check URL boş olamaz.";
            return false;
        }

        if (!int.TryParse(TxtRefreshInterval.Text, out var interval) || interval < 5)
        {
            errorMessage = "Yenileme aralığı en az 5 saniye olmalıdır.";
            return false;
        }

        if (ChkAutoStart.IsChecked == true && !IsValidTime(TxtStartTime.Text))
        {
            errorMessage = "Başlangıç saati HH:mm formatında olmalıdır. Örnek: 09:00";
            return false;
        }

        if (ChkAutoStop.IsChecked == true && !IsValidTime(TxtStopTime.Text))
        {
            errorMessage = "Kapanış saati HH:mm formatında olmalıdır. Örnek: 18:00";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool IsValidTime(string value)
        => TimeOnly.TryParseExact(value.Trim(), "HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _);
}
