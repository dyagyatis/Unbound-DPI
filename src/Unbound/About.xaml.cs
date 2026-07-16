using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Unbound
{
    public partial class About : Page
    {
        public About()
        {
            InitializeComponent();
            CheckForUpdates();
        }

        private async void CheckForUpdates()
        {
            string localVersionPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.txt");
            string localVersion = "1.0.0";

            if (File.Exists(localVersionPath))
            {
                localVersion = File.ReadAllText(localVersionPath).Trim();
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Исправлено: ссылка теперь указывает на ветку master
                    string url = "https://raw.githubusercontent.com/dyagyatis/Unbound-DPI/master/version.txt";

                    // Обязательный заголовок, чтобы GitHub одобрил запрос от нашего приложения
                    client.DefaultRequestHeaders.Add("User-Agent", "Unbound-Hub-Client");

                    string remoteVersion = (await client.GetStringAsync(url)).Trim();

                    if (remoteVersion != localVersion)
                    {
                        MessageBox.Show($"Доступно обновление Хаба!\n\nЛокальная версия: {localVersion}\nНовая версия: {remoteVersion}",
                            "Обновление системы", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch
            {
                // Ошибки сети или отсутствие файла временно игнорируются, чтобы приложение не падало
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}