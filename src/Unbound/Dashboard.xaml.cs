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

        public Dashboard()
        {
            InitializeComponent();
            StatusText.Text = GlobalState.Status;

            _pingTimer = new DispatcherTimer();
            _pingTimer.Interval = TimeSpan.FromSeconds(3);
            _pingTimer.Tick += PingTimer_Tick;
            _pingTimer.Start();

            // Инициализируем анимацию пульсации
            InitPulseAnimation();

            // Если при открытии вкладки служба уже запущена — включаем пульсацию
            if (GlobalState.Status == "Статус: Запущено")
            {
                StartPulse();
            }
        }

        private void InitPulseAnimation()
        {
            // Анимация изменения прозрачности от 1.0 до 0.4 и обратно
            DoubleAnimation pulseAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.4,
                Duration = TimeSpan.FromSeconds(1.2),
                AutoReverse = true, // Анимация идет обратно (затухание -> разгорание)
                RepeatBehavior = RepeatBehavior.Forever // Бесконечный цикл
            };

            Storyboard.SetTarget(pulseAnimation, StatusText);
            Storyboard.SetTargetProperty(pulseAnimation, new PropertyPath(TextBlock.OpacityProperty));

            _pulseStoryboard = new Storyboard();
            _pulseStoryboard.Children.Add(pulseAnimation);
        }

        private void StartPulse()
        {
            // Запускаем пульсацию только если включены анимации для мощных ПК
            if (GlobalState.IsAnimationEnabled && _pulseStoryboard != null)
            {
                _pulseStoryboard.Begin();
            }
        }

        private void StopPulse()
        {
            if (_pulseStoryboard != null)
            {
                _pulseStoryboard.Stop();
                StatusText.Opacity = 1.0; // Сбрасываем прозрачность в исходное состояние
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            DpiManager.Start();
            StatusText.Text = GlobalState.Status;

            if (GlobalState.Status == "Статус: Запущено")
            {
                StatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
                StartPulse();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            DpiManager.Stop();
            StatusText.Text = GlobalState.Status;
            StatusText.Foreground = System.Windows.Media.Brushes.LightGray;
            StopPulse();
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
                        PingText.Text = "Пинг до YouTube: Недоступен (Блокировка)";
                        PingText.Foreground = System.Windows.Media.Brushes.IndianRed;
                    }
                }
            }
            catch
            {
                PingText.Text = "Пинг до YouTube: Ошибка сети";
                PingText.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }
    }
}