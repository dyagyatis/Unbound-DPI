using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks; // Для CS0103
using System.Windows;
using System.Windows.Controls;
using System.Net.NetworkInformation;
using System.Drawing; // Для SystemIcons

namespace Unbound
{
    public partial class MainWindow : Window
    {
        // 1. Исправлено объявление (добавлен '?')
        private Process? _winwsProcess;
        private System.Windows.Forms.NotifyIcon? _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            LoadStrategies();
            SetupTray();
        }

        private void SetupTray()
        {
            // Используем полные пути, чтобы не зависеть от конфликтов using
            _trayIcon = new System.Windows.Forms.NotifyIcon();
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            _trayIcon.Visible = true;
            _trayIcon.Click += (s, e) => {
                this.Show();
                this.WindowState = WindowState.Normal;
            };
        }

        private void LoadStrategies()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "strategies");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            StrategySelector.Items.Clear();
            foreach (string file in Directory.GetFiles(path, "*.txt"))
            {
                StrategySelector.Items.Add(new ComboBoxItem
                {
                    Content = Path.GetFileNameWithoutExtension(file),
                    Tag = File.ReadAllText(file).Trim()
                });
            }
            if (StrategySelector.Items.Count > 0) StrategySelector.SelectedIndex = 0;
        }

        private void Log(string message)
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logPath, logEntry);
        }

        // 2. Исправлены аргументы метода для совместимости с кнопками
        private void StartButton_Click(object? sender, RoutedEventArgs e)
        {
            string strategy = (StrategySelector.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
            string tcp = (GameTCP.IsChecked == true || GameAll.IsChecked == true) ? "1024-65535" : "12";
            string udp = (GameAll.IsChecked == true) ? "1024-65535" : "12";
            string args = $"--wf-tcp=80,443,2053,2083,2087,2096,8443,{tcp} --wf-udp=443,19294-19344,50000-50100,{udp} {strategy}";

            KillWinwsProcesses();
            _winwsProcess = new Process { StartInfo = { FileName = "winws.exe", Arguments = args, UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden } };
            _winwsProcess.Start();
            StatusText.Text = "Статус: Запущено";
        }

        private void StopButton_Click(object? sender, RoutedEventArgs e) => KillWinwsProcesses();

        private void KillWinwsProcesses()
        {
            foreach (var p in Process.GetProcessesByName("winws")) p.Kill();
            StatusText.Text = "Статус: Остановлено";
        }

        private async void RunPingTest_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Тестирование...";
            foreach (ComboBoxItem item in StrategySelector.Items)
            {
                // Вызываем StartButton_Click с фиктивными параметрами
                StartButton_Click(null, new RoutedEventArgs());
                await Task.Delay(2000);
                bool success = await Task.Run(() => new Ping().Send("8.8.8.8", 1000).Status == IPStatus.Success);
                if (success) { StatusText.Text = $"Лучшая: {item.Content}"; return; }
            }
        }
        private async Task CheckForUpdates()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                string remoteVersion = await client.GetStringAsync("https://raw.githubusercontent.com/dyagyatis/Unbound-DPI/master/version.txt");
                string localVersion = "1.0.0"; // Твоя текущая версия

                if (remoteVersion.Trim() != localVersion)
                {
                    StatusText.Text = "Доступно обновление стратегий!";
                }
            }
            catch { /* Игнорируем, если нет интернета */ }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized) this.Hide();
            base.OnStateChanged(e);
        }
    }
}