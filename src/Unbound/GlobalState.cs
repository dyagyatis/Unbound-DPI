using System.Collections.Generic;

namespace Unbound
{
    public static class GlobalState
    {
        // Хранилище для выбранных фильтров
        public static HashSet<string> ActiveFilters { get; } = new HashSet<string>();

        // Переменная для хранения текущего статуса работы winws2
        public static string Status { get; set; } = "Статус: Ожидание";

        // НАСТРОЙКИ ВИЗУАЛА (НОВОЕ)
        public static string CurrentTheme { get; set; } = "Auto"; // "Dark", "Light", "Auto"
        public static bool IsAnimationEnabled { get; set; } = true;

        public static string SelectedStrategy { get; set; } = "ALT11"; // Текущая выбранная стратегия
        public static bool IsRunningAsService { get; set; } = false; // Запущено как служба или как процесс?
    }
}