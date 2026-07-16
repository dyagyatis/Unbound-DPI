using System;
using System.Windows;
using Microsoft.Win32;

namespace Unbound
{
    public static class ThemeManager
    {
        public static void ApplyTheme(string themeName)
        {
            GlobalState.CurrentTheme = themeName;
            string themeFile = "DarkTheme.xaml";

            if (themeName == "Light")
            {
                themeFile = "LightTheme.xaml";
            }
            else if (themeName == "Auto")
            {
                themeFile = IsWindowsInDarkMode() ? "DarkTheme.xaml" : "LightTheme.xaml";
            }

            try
            {
                // Создаем новый словарь ресурсов для темы
                var newThemeDict = new ResourceDictionary
                {
                    Source = new Uri(themeFile, UriKind.Relative)
                };

                var appResources = Application.Current.Resources.MergedDictionaries;
                bool themeReplaced = false;

                // Перебираем уже подключенные словари в поисках старой темы
                for (int i = 0; i < appResources.Count; i++)
                {
                    string sourceUrl = appResources[i].Source.OriginalString;

                    // Если нашли старый файл темы, точечно заменяем его
                    if (sourceUrl.Contains("DarkTheme.xaml") || sourceUrl.Contains("LightTheme.xaml"))
                    {
                        appResources[i] = newThemeDict;
                        themeReplaced = true;
                        break;
                    }
                }

                // Если старой темы в списке не нашлось, просто добавляем новую
                if (!themeReplaced)
                {
                    appResources.Add(newThemeDict);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при смене темы: {ex.Message}");
            }
        }

        private static bool IsWindowsInDarkMode()
        {
            try
            {
                // Для поддержки null-safety в более новых версиях .NET добавляем знак '?' к типу RegistryKey
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object? registryValue = key?.GetValue("AppsUseLightTheme");
                    if (registryValue != null)
                    {
                        return (int)registryValue == 0;
                    }
                }
            }
            catch { }
            return true;
        }
    }
}