using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Unbound
{
    public partial class MainWindow : Window
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private bool _isClosingFromTray = false;

        private readonly Dashboard _dashboardPage = new Dashboard();
        private readonly Services _servicesPage = new Services();
        private readonly Settings _settingsPage = new Settings();
        private readonly About _aboutPage = new About();

        public MainWindow()
        {
            // Добавь эти две строки перед InitializeComponent()
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;

            InitializeComponent();

            // Загружаем начальный экран
            MainFrame.Navigate(_dashboardPage);

            // Инициализация трея
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Icon = System.Drawing.SystemIcons.Shield;
            _notifyIcon.Text = "Unbound Hub - Остановлен";
            _notifyIcon.Visible = true;

            _notifyIcon.Click += (s, e) =>
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            };

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("Открыть Хаб", null, (s, e) => { this.Show(); this.WindowState = WindowState.Normal; });
            contextMenu.Items.Add("Полный выход", null, (s, e) =>
            {
                _isClosingFromTray = true;
                DpiManager.Stop();
                _notifyIcon.Dispose();
                this.Close();
            });
            _notifyIcon.ContextMenuStrip = contextMenu;

            // Проверка аргументов запуска в трее
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg.Equals("-tray", StringComparison.OrdinalIgnoreCase))
                {
                    this.WindowState = WindowState.Minimized;
                    this.Hide();
                    break;
                }
            }

            // ФИЧА 2: Живой вывод логов в TextBox на главном окне
            DpiManager.OnLogReceived += (logMessage) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {logMessage}{Environment.NewLine}");
                    LogTextBox.ScrollToEnd();
                }));
            };

            // ФИЧА 4: Подписка на изменение статуса для обновления трея
            System.Timers.Timer statusCheckTimer = new System.Timers.Timer(1000);
            statusCheckTimer.Elapsed += (s, e) => Dispatcher.BeginInvoke(new Action(UpdateTrayStatus));
            statusCheckTimer.Start();

            Loaded += async (s, e) =>
            {
                await CheckAndDownloadUpdatesAsync(); // Обновление winws2.exe
                await DownloadRuListAsync();          // Обновление reestr_hostname.txt
            };
        }

        // ФИЧА 4: Динамический статус иконки в трее
        private void UpdateTrayStatus()
        {
            bool isRunning = GlobalState.Status.Contains("Запущено");
            _notifyIcon.Text = $"Unbound Hub - {(isRunning ? "Активен" : "Остановлен")}";
            _notifyIcon.Icon = isRunning ? System.Drawing.SystemIcons.Shield : System.Drawing.SystemIcons.Warning;
        }

        // Фоновое скачивание свежего списка заблокированных сайтов из репозитория bol-van/rulist
        private async Task DownloadRuListAsync()
        {
            try
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] [System] Синхронизация списка заблокированных ресурсов (rulist)...\n");
                using (HttpClient client = new HttpClient())
                {
                    // Прямая ссылка на сырой файл reestr_hostname.txt из репозитория bol-van/rulist
                    string url = "https://raw.githubusercontent.com/bol-van/rulist/master/reestr_hostname.txt";
                    string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "files", "reestr_hostname.txt");

                    // Проверяем существование папки files
                    string? directoryPath = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    // Скачиваем и перезаписываем файл
                    string listContent = await client.GetStringAsync(url);
                    await File.WriteAllTextAsync(targetPath, listContent, Encoding.UTF8);

                    LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] [System] Список reestr_hostname.txt успешно обновлен.\n");
                }
            }
            catch (Exception ex)
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] [Warning] Не удалось загрузить rulist: {ex.Message}\n");
            }
        }

        // ФИЧА 5: Фоновое автообновление winws2 с официального репозитория zapret-win-bundle
        private async Task CheckAndDownloadUpdatesAsync()
        {
            try
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] [System] Проверка обновлений winws2...\n");
                using (HttpClient client = new HttpClient())
                {
                    // ИСПРАВЛЕНО: Прямая официальная ссылка на winws2.exe из бандла bol-van
                    string downloadUrl = "https://raw.githubusercontent.com/bol-van/zapret-win-bundle/master/zapret-winws/winws2.exe";
                    string localExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "winws2.exe");

                    // Проверяем, не запущен ли процесс в данный момент
                    if (Process.GetProcessesByName("winws2").Length == 0)
                    {
                        byte[] fileBytes = await client.GetByteArrayAsync(downloadUrl);

                        // Проверяем наличие папки назначения и создаем её при необходимости
                        string? directoryPath = Path.GetDirectoryName(localExePath);
                        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }

                        await File.WriteAllBytesAsync(localExePath, fileBytes);
                        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] [System] Движок winws2.exe успешно синхронизирован с официальным репозиторием bol-van.\n");
                    }
                    else
                    {
                        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] [System] Обновление пропущено: процесс winws2 активен.\n");
                    }
                }
            }
            catch (Exception ex)
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] [Warning] Не удалось обновить бинарники: {ex.Message}\n");
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    var mousePos = PointToScreen(e.GetPosition(this));
                    this.Top = mousePos.Y - 15;
                    this.WindowState = WindowState.Normal;
                }
                this.DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

        private void ToggleMaximize()
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                BtnMaximize.Content = "▢";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                BtnMaximize.Content = "❐";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();

        private void MainFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            if (GlobalState.IsAnimationEnabled)
            {
                DoubleAnimation fadeIn = new DoubleAnimation { From = 0.0, To = 1.0, Duration = TimeSpan.FromMilliseconds(250) };
                MainFrame.BeginAnimation(Frame.OpacityProperty, fadeIn);
            }
            else
            {
                MainFrame.BeginAnimation(Frame.OpacityProperty, null);
                MainFrame.Opacity = 1.0;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isClosingFromTray)
            {
                e.Cancel = true;
                this.Hide();
            }
            base.OnClosing(e);
        }

        private void Nav_Main_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_dashboardPage);
        private void Nav_Services_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_servicesPage);
        private void Nav_Settings_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_settingsPage);
        private void Nav_About_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_aboutPage);
    }
}