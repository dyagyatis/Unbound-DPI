using System;
using System.ComponentModel;
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
            InitializeComponent();

            // Загружаем начальный экран
            MainFrame.Navigate(_dashboardPage);

            // Инициализация трея
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Icon = System.Drawing.SystemIcons.Shield;
            _notifyIcon.Text = "Unbound Hub";
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

            // ФИСКАЛЬНЫЙ ШТРИХ: Проверяем аргументы командной строки при старте
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg.Equals("-tray", StringComparison.OrdinalIgnoreCase))
                {
                    // Сворачиваем и скрываем окно в трей до его первой отрисовки
                    this.WindowState = WindowState.Minimized;
                    this.Hide();
                    break;
                }
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

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

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

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MainFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            if (GlobalState.IsAnimationEnabled)
            {
                DoubleAnimation fadeIn = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
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