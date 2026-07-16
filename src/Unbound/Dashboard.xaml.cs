using System;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Unbound
{
    public partial class Dashboard : Page
    {
        private DispatcherTimer _pingTimer;
        private Storyboard? _pulseStoryboard;
        private bool _isInitialized = false;

        public Dashboard()
        {
            InitializeComponent();
            StatusText.Text = GlobalState.Status;

            _pingTimer = new DispatcherTimer();
            _pingTimer.Interval = TimeSpan.FromSeconds(3);
            _pingTimer.Tick += PingTimer_Tick;
            _pingTimer.Start();

            InitPulseAnimation();

            // Если система уже запущена (например, после автозапуска), включаем пульсацию
            if (GlobalState.Status.Contains("Запущено"))
            {
                StartPulse();
            }

            foreach (ComboBoxItem item in StrategySelector.Items)
            {
                if (item.Tag?.ToString() == GlobalState.SelectedStrategy)
                {
                    item.IsSelected = true;
                    break;
                }
            }
            _isInitialized = true;
        }

        private void InitPulseAnimation()
        {
            DoubleAnimation pulseAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.4,
                Duration = TimeSpan.FromSeconds(1.2),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(pulseAnimation, StatusText);
            Storyboard.SetTargetProperty(pulseAnimation, new PropertyPath(TextBlock.OpacityProperty));
            _pulseStoryboard = new Storyboard();
            _pulseStoryboard.Children.Add(pulseAnimation);
        }

        private void StartPulse()
        {
            if (GlobalState.IsAnimationEnabled && _pulseStoryboard != null) _pulseStoryboard.Begin();
        }

        private void StopPulse()
        {
            if (_pulseStoryboard != null) { _pulseStoryboard.Stop(); StatusText.Opacity = 1.0; }
        }
        // --- УНИВЕРСАЛЬНЫЙ ЗАПУСК в Dashboard.xaml.cs ---
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Запуск Zapret
            DpiManager.Start();

            // 2. Запуск TG Proxy (передаем только 2 аргумента, как в твоем TgProxyManager)
            // Ты можешь вынести настройки порта и воркера в настройки или брать их дефолтными
            TgProxyManager.Start("1080", "");

            StatusText.Text = "Статус: Запущено (DPI + TG Proxy)";
            StatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
            StartPulse();
        }

        // --- УНИВЕРСАЛЬНАЯ ОСТАНОВКА ---
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            DpiManager.Stop();
            TgProxyManager.Stop();

            StatusText.Text = "Статус: Остановлено";
            StatusText.Foreground = System.Windows.Media.Brushes.LightGray;
            StopPulse();
        }

        private void StrategySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (StrategySelector.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                GlobalState.SelectedStrategy = item.Tag.ToString()!;
                if (GlobalState.Status.Contains("Запущено")) DpiManager.Start();
            }
        }

        private async void PingTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                using (Ping pingSender = new Ping())
                {
                    PingReply reply = await pingSender.SendPingAsync("youtube.com", 1000);
                    if (reply.Status == IPStatus.Success)
                    {
                        PingText.Text = $"Пинг до YouTube: {reply.RoundtripTime} мс";
                        PingText.Foreground = System.Windows.Media.Brushes.LimeGreen;
                    }
                    else
                    {
                        PingText.Text = "Пинг до YouTube: Ошибка сети";
                        PingText.Foreground = System.Windows.Media.Brushes.IndianRed;
                    }
                }
            }
            catch { PingText.Text = "Пинг до YouTube: Ошибка"; }
        }
    }
}