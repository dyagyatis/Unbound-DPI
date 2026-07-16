using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Unbound
{
    public partial class Services : Page
    {
        private readonly string _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "files", "list-youtube.txt");

        public Services()
        {
            InitializeComponent();
            LoadServiceCards();
        }

        private void LoadServiceCards()
        {
            ServiceGrid.Children.Clear();
            ServiceGrid.Children.Add(new ServiceCard("YouTube (QUIC)", "windivert_part.quic_initial_ietf"));
            ServiceGrid.Children.Add(new ServiceCard("Discord Media", "windivert_part.discord_media"));
            ServiceGrid.Children.Add(new ServiceCard("Discord Voice", "windivert_part.stun"));
            ServiceGrid.Children.Add(new ServiceCard("WireGuard VPN", "windivert_part.wireguard"));
            ServiceGrid.Children.Add(new ServiceCard("DHT Torrent", "windivert_part.dht"));
        }

        private void OpenEditor_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_filePath))
            {
                HostlistTextBox.Text = File.ReadAllText(_filePath);
                EditorGrid.Visibility = Visibility.Visible;
            }
            else
            {
                MessageBox.Show("Файл list-youtube.txt не найден в папке files!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // УМНАЯ ОЧИСТКА И СОРТИРОВКА ПРИ СОХРАНЕНИИ (НОВОЕ)
        private void SaveEditor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Читаем текст из редактора, разбиваем на строки и прогоняем через фильтры
                var processedDomains = HostlistTextBox.Text
                    .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim().ToLower()) // Избавляемся от пробелов и приводим к нижнему регистру
                    .Select(line =>
                    {
                        string domain = line;

                        // Удаляем протоколы, если пользователь вставил прямые ссылки
                        if (domain.StartsWith("https://")) domain = domain.Substring(8);
                        if (domain.StartsWith("http://")) domain = domain.Substring(7);
                        if (domain.StartsWith("www.")) domain = domain.Substring(4);

                        // Если это полноценный URL (например, domain.com/page/1), отсекаем всё после слэша
                        int slashIndex = domain.IndexOf('/');
                        if (slashIndex > 0) domain = domain.Substring(0, slashIndex);

                        return domain.Trim();
                    })
                    // Убираем пустые строки, комментарии движка zapret и некорректные записи (без точек)
                    .Where(domain => !string.IsNullOrWhiteSpace(domain) && domain.Contains(".") && !domain.StartsWith("#"))
                    .Distinct() // Удаляем дубликаты
                    .OrderBy(domain => domain) // Сортируем по алфавиту A-Z
                    .ToList();

                // Записываем очищенный массив строк обратно в файл
                File.WriteAllLines(_filePath, processedDomains);

                // Синхронизируем TextBox с отформатированным результатом, чтобы пользователь сразу увидел изменения
                HostlistTextBox.Text = string.Join(Environment.NewLine, processedDomains);

                EditorGrid.Visibility = Visibility.Collapsed;
                MessageBox.Show("Список сайтов успешно оптимизирован, очищен от дубликатов и сохранен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseEditor_Click(object sender, RoutedEventArgs e)
        {
            EditorGrid.Visibility = Visibility.Collapsed;
        }
    }
}