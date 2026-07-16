using System;
using System.Windows.Controls;

namespace Unbound
{
    public partial class Logs : Page
    {
        public Logs()
        {
            InitializeComponent();

            // Подписываемся на событие получения логов из DpiManager
            DpiManager.OnLogReceived += AppendLog;

            // Выводим приветственное сообщение при открытии вкладки
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Открыта панель мониторинга трафика.\n");
        }

        private void AppendLog(string message)
        {
            // Перенаправляем выполнение в основной поток GUI, чтобы не было крэша
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogTextBox.AppendText($"[{timestamp}] {message}\n");

                // Автоматически прокручиваем текст вниз к самым свежим логам
                LogTextBox.ScrollToEnd();
            });
        }

        // Очищаем подписку при уничтожении страницы (опционально, для предотвращения утечек памяти)
        ~Logs()
        {
            DpiManager.OnLogReceived -= AppendLog;
        }
    }
}