using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Unbound
{
    public partial class Settings : Page
    {
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "UnboundDpiHub";

        // Флаг, блокирующий автоматические срабатывания при создании страницы
        private bool _isInitialized = false;

        public Settings()
        {
            InitializeComponent();
            CheckAutostartStatus();

            // Восстанавливаем чекбокс анимации
            AnimationCheckBox.IsChecked = GlobalState.IsAnimationEnabled;

            // Сначала устанавливаем выбранную тему в ComboBox на основе глобального состояния
            SetCurrentThemeInSelector();

            // Только теперь, когда страница готова, разрешаем реагировать на клики пользователя
            _isInitialized = true;
        }

        private void CheckAutostartStatus()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false))
            {
                AutostartCheckBox.IsChecked = key?.GetValue(AppName) != null;
            }
        }

        private void SetCurrentThemeInSelector()
        {
            // Временно отключаем флаг, чтобы принудительная установка индекса не вызвала событие
            _isInitialized = false;

            foreach (ComboBoxItem item in ThemeSelector.Items)
            {
                if (item.Tag?.ToString() == GlobalState.CurrentTheme)
                {
                    item.IsSelected = true;
                    break;
                }
            }
        }

        private void Autostart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
                {
                    if (AutostartCheckBox.IsChecked == true)
                    {
                        string? appPath = Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(appPath))
                        {
                            key?.SetValue(AppName, $"\"{appPath}\" -tray");
                        }
                    }
                    else
                    {
                        key?.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка реестра: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnableTimestamps_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "interface tcp set global timestamps=enabled",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                Process.Start(psi);
                MessageBox.Show("TCP Timestamps успешно включены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось выполнить команду: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ОБРАБОТЧИК СМЕНЫ ТЕМЫ С ЗАЩИТОЙ (ИСПРАВЛЕНО)
        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Если страница еще инициализируется — игнорируем событие
            if (!_isInitialized) return;

            if (ThemeSelector.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                string themeTag = selectedItem.Tag.ToString()!;
                ThemeManager.ApplyTheme(themeTag);
            }
        }

        private void Animation_Click(object sender, RoutedEventArgs e)
        {
            GlobalState.IsAnimationEnabled = AnimationCheckBox.IsChecked == true;
            MessageBox.Show(GlobalState.IsAnimationEnabled
                ? "Интерфейсные анимации успешно включены."
                : "Анимации отключены. Нагрузка снижена.",
                "Эффекты", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteDriver_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите удалить драйвер WinDivert?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    ProcessStartInfo stopInfo = new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c sc stop windivert", Verb = "runas", CreateNoWindow = true, UseShellExecute = true };
                    Process.Start(stopInfo)?.WaitForExit();

                    ProcessStartInfo deleteInfo = new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c sc delete windivert", Verb = "runas", CreateNoWindow = true, UseShellExecute = true };
                    Process.Start(deleteInfo)?.WaitForExit();

                    MessageBox.Show("Драйвер WinDivert успешно удален из системы.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении драйвера: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}