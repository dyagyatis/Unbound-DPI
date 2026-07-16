using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Unbound
{
    public partial class ServiceCard : UserControl
    {
        private readonly string _filterName;
        private bool _isActive = false;

        public ServiceCard(string displayName, string filterName)
        {
            InitializeComponent();
            TitleText.Text = displayName;
            _filterName = filterName;
        }

        private void Card_Click(object sender, MouseButtonEventArgs e)
        {
            _isActive = !_isActive;

            // Целевой цвет: зеленый при активации, темный (из темы) при деактивации
            Color targetColor = _isActive ? Color.FromRgb(34, 139, 34) : Color.FromRgb(45, 45, 45);

            if (GlobalState.IsAnimationEnabled)
            {
                // 1. ИСПРАВЛЕНИЕ: Клонируем кисть фонового рисунка, чтобы разморозить её для анимации
                if (CardBorder.Background is SolidColorBrush currentBrush)
                {
                    CardBorder.Background = currentBrush.Clone();
                }
                else
                {
                    CardBorder.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                }

                // 2. ИСПРАВЛЕНИЕ: Настраиваем плавную анимацию цвета (300 миллисекунд для четкого визуального эффекта)
                ColorAnimation colorAnim = new ColorAnimation
                {
                    To = targetColor,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } // Плавное замедление в конце
                };

                // Запускаем анимацию именно на свойство Color фоновой кисти нашего Border
                CardBorder.Background.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
            }
            else
            {
                // Если режим для слабых ПК — мгновенно меняем кисть без анимаций
                CardBorder.Background = new SolidColorBrush(targetColor);
            }

            // Управление глобальным списком фильтров
            if (_isActive)
                GlobalState.ActiveFilters.Add(_filterName);
            else
                GlobalState.ActiveFilters.Remove(_filterName);
        }
    }
}