using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Unbound
{
    public static class DpiManager
    {
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static Process? _winwsProcess;

        // Событие, на которое подпишется страница логов
        public static event Action<string>? OnLogReceived;

        // БАЗА ДАННЫХ НОВЫХ СТРАТЕГИЙ (Переведены под синтаксис нового движка winws2)
        private static readonly Dictionary<string, string> DpiStrategies = new Dictionary<string, string>
        {
            { "ALT", "fake:blob=fake_default_tls:tcp_md5:repeats=6 --lua-desync=fakedsplit:ip_autottl=-2,3-20:tcp_md5" },
            { "ALT2", "multisplit:pos=2" },
            { "ALT3", "fake:blob=fake_default_tls:tcp_md5:tls_mod=rnd,dupsid,sni=www.google.com --lua-desync=hostfakesplit:pos=1" },
            { "ALT4", "fake:blob=fake_default_tls:tcp_seq=-10000:repeats=6 --lua-desync=multisplit:pos=1" },
            { "ALT5", "syndata --lua-desync=multidisorder:pos=1,midsld" },
            { "ALT6", "multisplit:pos=1" },
            { "ALT7", "multisplit:pos=2,sniext+1" },
            { "ALT8", "fake:blob=fake_default_tls:tcp_seq=-2:repeats=6" },
            { "ALT9", "hostfakesplit:pos=1" },
            { "ALT10", "fake:blob=fake_default_tls:tcp_md5:repeats=6" },
            { "ALT11", "fake:blob=fake_default_tls:tcp_md5:repeats=8 --lua-desync=multisplit:pos=1" },
            { "ALT12", "fake:blob=fake_default_tls:tcp_md5:repeats=8 --lua-desync=multisplit:pos=1" },
            { "SimpleFake", "fake:blob=quic_google:repeats=6" },
            { "SimpleFakeAlt", "fake:blob=fake_default_tls:tcp_md5:tcp_seq=-2:repeats=6" },
            { "SimpleFakeAlt2", "fake:blob=fake_default_tls:tcp_md5:repeats=6" },
            { "FakeTLSAuto", "fake:blob=quic_google:repeats=11" },
            { "FakeTLSAutoAlt", "fake:blob=fake_default_tls:tcp_seq=-2:repeats=8:tls_mod=rnd,dupsid,sni=www.google.com --lua-desync=fakedsplit:pos=1" },
            { "FakeTLSAutoAlt2", "fake:blob=fake_default_tls:tcp_seq=-10000000:repeats=8:tls_mod=rnd,dupsid,sni=www.google.com --lua-desync=multisplit:pos=1" },
            { "FakeTLSAutoAlt3", "fake:blob=fake_default_tls:tcp_md5:repeats=8:tls_mod=rnd,dupsid,sni=www.google.com --lua-desync=multisplit:pos=1" }
        };

        // Вспомогательный приватный метод генерации конфига, чтобы код не дублировался
        private static string GenerateConfig()
        {
            StringBuilder configBuilder = new StringBuilder();
            string binFolderPath = Path.Combine(BaseDir, "bin");

            // 1. Глобальные параметры инициализации нового zapret
            configBuilder.AppendLine("--wf-tcp-out=80,443");
            configBuilder.AppendLine($"--lua-init=@\"{Path.Combine(BaseDir, "lua", "zapret-lib.lua")}\"");
            configBuilder.AppendLine($"--lua-init=@\"{Path.Combine(BaseDir, "lua", "zapret-antidpi.lua")}\"");
            configBuilder.AppendLine("--lua-init=\"fake_default_tls = tls_mod(fake_default_tls,'rnd,rndsni')\"");

            // Загрузка внешних блобов из папки bin
            configBuilder.AppendLine($"--blob=quic_google:@\"{Path.Combine(binFolderPath, "quic_initial_www_google_com.bin")}\"");

            // 2. Подключение сырых фильтров трафика windivert
            foreach (var filterFile in GlobalState.ActiveFilters)
            {
                string fullFilterPath = Path.Combine(BaseDir, "windivert.filter", $"{filterFile}.txt");
                if (File.Exists(fullFilterPath))
                {
                    configBuilder.AppendLine($"--wf-raw-part=@\"{fullFilterPath}\"");
                }
            }

            // 3. Динамический выбор активной стратегии из UI через GlobalState
            string strategyKey = "FakeTLSAutoAlt3";
            if (!string.IsNullOrEmpty(GlobalState.SelectedStrategy))
            {
                strategyKey = GlobalState.SelectedStrategy;
            }

            // Извлекаем синтаксис десинка, если не найден — ставим стабильный дефолт
            if (!DpiStrategies.TryGetValue(strategyKey, out string? activeDesync))
            {
                activeDesync = "fake:blob=fake_default_tls:tcp_md5:repeats=8:tls_mod=rnd,dupsid,sni=www.google.com --lua-desync=multisplit:pos=1";
            }

            // 4. Генерация правил фильтрации с переходом на reestr_hostname.txt (rulist)
            // Порт 80 (HTTP)
            configBuilder.AppendLine("--filter-tcp=80 --filter-l7=http --out-range=-d10 --payload=http_req --lua-desync=fake:blob=fake_default_http:ip_autottl=-2,3-20:ip6_autottl=-2,3-20:tcp_md5 --lua-desync=fakedsplit:ip_autottl=-2,3-20:ip6_autottl=-2,3-20:tcp_md5 --new");

            // Порт 443 (HTTPS) с применением выбранной из UI стратегии обхода по всему скачанному rulist
            configBuilder.AppendLine($"--filter-tcp=443 --filter-l7=tls --hostlist=\"{Path.Combine(BaseDir, "files", "reestr_hostname.txt")}\" --out-range=-d10 --payload=tls_client_hello --lua-desync={activeDesync} --new");

            // Общие правила для HTTPS (для сайтов вне списков)
            configBuilder.AppendLine($"--filter-tcp=443 --filter-l7=tls --out-range=-d10 --payload=tls_client_hello --lua-desync=fake:blob=fake_default_tls:tcp_md5:tcp_seq=-10000:repeats=6 --lua-desync=multidisorder:pos=midsld --new");

            // Порт 443 UDP (QUIC обход для ускорения YouTube и ресурсов из reestr_hostname.txt)
            configBuilder.AppendLine($"--filter-udp=443 --filter-l7=quic --hostlist=\"{Path.Combine(BaseDir, "files", "reestr_hostname.txt")}\" --payload=quic_initial --lua-desync=fake:blob=quic_google:repeats=11 --new");
            configBuilder.AppendLine("--filter-udp=443 --filter-l7=quic --payload=quic_initial --lua-desync=fake:blob=fake_default_quic:repeats=11 --new");

            // Мессенджеры и Голосовая связь
            configBuilder.AppendLine("--filter-l7=wireguard,stun,discord --payload=wireguard_initiation,wireguard_cookie,stun,discord_ip_discovery --lua-desync=fake:blob=0x00000000000000000000000000000000:repeats=2");

            return configBuilder.ToString();
        }

        public static void InstallService()
        {
            try
            {
                Stop(); // Полностью глушим процессы перед работой со службой

                string binFolderPath = Path.Combine(BaseDir, "bin");
                string exePath = Path.Combine(binFolderPath, "winws2.exe");
                string configPath = Path.Combine(BaseDir, "generated_config.txt");

                // Генерируем и сохраняем актуальную конфигурацию на диск без запуска бинарника
                string configText = GenerateConfig();
                File.WriteAllText(configPath, configText, Encoding.UTF8);

                // Регистрируем службу winws2 в системе через утилиту sc.exe
                string cmdArgs = $"/c sc create \"winws2\" binPath= \"\\\"{exePath}\\\" --service @\\\"{configPath}\\\"\" start= auto";

                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmdArgs)
                {
                    Verb = "runas", // Запуск от Администратора
                    CreateNoWindow = true,
                    UseShellExecute = true
                };
                Process.Start(psi)?.WaitForExit();

                // Запускаем службу
                Process.Start(new ProcessStartInfo("cmd.exe", "/c sc start \"winws2\"") { Verb = "runas", CreateNoWindow = true, UseShellExecute = true });

                GlobalState.Status = "Статус: Запущено (Служба)";
                OnLogReceived?.Invoke("[System] Системная служба winws2 успешно создана и добавлена в автозапуск Windows.");
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[Service Error] Не удалось установить службу: {ex.Message}");
            }
        }

        public static void UninstallService()
        {
            try
            {
                // Останавливаем и полностью вырезаем службу из системы
                Process.Start(new ProcessStartInfo("cmd.exe", "/c sc stop \"winws2\"") { Verb = "runas", CreateNoWindow = true, UseShellExecute = true })?.WaitForExit();
                Process.Start(new ProcessStartInfo("cmd.exe", "/c sc delete \"winws2\"") { Verb = "runas", CreateNoWindow = true, UseShellExecute = true })?.WaitForExit();

                GlobalState.Status = "Статус: Остановлено";
                OnLogReceived?.Invoke("[System] Системная служба winws2 успешно удалена из операционной системы.");
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[Service Error] Ошибка при удалении службы: {ex.Message}");
            }
        }

        public static void Start()
        {
            Stop();

            string configPath = Path.Combine(BaseDir, "generated_config.txt");
            string binFolderPath = Path.Combine(BaseDir, "bin");

            // Генерируем конфигурацию и пишем на диск
            string configText = GenerateConfig();
            File.WriteAllText(configPath, configText, Encoding.UTF8);

            string exePath = Path.Combine(binFolderPath, "winws2.exe");
            if (File.Exists(exePath))
            {
                _winwsProcess = new Process();
                _winwsProcess.StartInfo.FileName = exePath;
                _winwsProcess.StartInfo.Arguments = $"@\"{configPath}\"";

                // Настройки перехвата логов
                _winwsProcess.StartInfo.UseShellExecute = false;
                _winwsProcess.StartInfo.RedirectStandardOutput = true;
                _winwsProcess.StartInfo.RedirectStandardError = true;
                _winwsProcess.StartInfo.CreateNoWindow = true;

                _winwsProcess.OutputDataReceived += (s, e) => { if (e.Data != null) OnLogReceived?.Invoke(e.Data); };
                _winwsProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) OnLogReceived?.Invoke($"[ERROR] {e.Data}"); };

                _winwsProcess.Start();

                _winwsProcess.BeginOutputReadLine();
                _winwsProcess.BeginErrorReadLine();

                string strategyName = !string.IsNullOrEmpty(GlobalState.SelectedStrategy) ? GlobalState.SelectedStrategy : "FakeTLSAutoAlt3";
                GlobalState.Status = "Статус: Запущено";
                OnLogReceived?.Invoke($"[System] Движок winws2 успешно инициализирован со стратегией: {strategyName}");
            }
            else
            {
                GlobalState.Status = "Ошибка: winws2.exe не найден!";
                OnLogReceived?.Invoke("[System] Критическая ошибка: исполняемый файл bin/winws2.exe отсутствует.");
            }
        }

        public static void Stop()
        {
            try
            {
                if (_winwsProcess != null && !_winwsProcess.HasExited)
                {
                    _winwsProcess.Kill();
                    _winwsProcess.Dispose();
                    _winwsProcess = null;
                }
            }
            catch { }

            // Чистим остаточные зависшие процессы в диспетчере задач
            foreach (var p in Process.GetProcessesByName("winws2"))
            {
                try { p.Kill(); } catch { }
            }

            GlobalState.Status = "Статус: Остановлено";
            OnLogReceived?.Invoke("[System] Движок winws2 остановлен пользователем.");
        }
    }
}