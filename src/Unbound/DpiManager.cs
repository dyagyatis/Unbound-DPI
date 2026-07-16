using System;
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

        public static void Start()
        {
            Stop();

            string configPath = Path.Combine(BaseDir, "generated_config.txt");
            StringBuilder configBuilder = new StringBuilder();

            configBuilder.AppendLine("--wf-tcp-out=80,443");
            configBuilder.AppendLine($"--lua-init=@\"{Path.Combine(BaseDir, "lua", "zapret-lib.lua")}\"");
            configBuilder.AppendLine($"--lua-init=@\"{Path.Combine(BaseDir, "lua", "zapret-antidpi.lua")}\"");
            configBuilder.AppendLine("--lua-init=\"fake_default_tls = tls_mod(fake_default_tls,'rnd,rndsni')\"");
            configBuilder.AppendLine($"--blob=quic_google:@\"{Path.Combine(BaseDir, "files", "quic_initial_www_google_com.bin")}\"");

            foreach (var filterFile in GlobalState.ActiveFilters)
            {
                string fullFilterPath = Path.Combine(BaseDir, "windivert.filter", $"{filterFile}.txt");
                if (File.Exists(fullFilterPath))
                {
                    configBuilder.AppendLine($"--wf-raw-part=@\"{fullFilterPath}\"");
                }
            }

            configBuilder.AppendLine("--filter-tcp=80 --filter-l7=http --out-range=-d10 --payload=http_req --lua-desync=fake:blob=fake_default_http:ip_autottl=-2,3-20:ip6_autottl=-2,3-20:tcp_md5 --lua-desync=fakedsplit:ip_autottl=-2,3-20:ip6_autottl=-2,3-20:tcp_md5 --new");
            configBuilder.AppendLine($"--filter-tcp=443 --filter-l7=tls --hostlist=\"{Path.Combine(BaseDir, "files", "list-youtube.txt")}\" --out-range=-d10 --payload=tls_client_hello --lua-desync=fake:blob=fake_default_tls:tcp_md5:repeats=11:tls_mod=rnd,dupsid,sni=www.google.com --lua-desync=multidisorder:pos=1,midsld --new");
            configBuilder.AppendLine("--filter-tcp=443 --filter-l7=tls --out-range=-d10 --payload=tls_client_hello --lua-desync=fake:blob=fake_default_tls:tcp_md5:tcp_seq=-10000:repeats=6 --lua-desync=multidisorder:pos=midsld --new");
            configBuilder.AppendLine($"--filter-udp=443 --filter-l7=quic --hostlist=\"{Path.Combine(BaseDir, "files", "list-youtube.txt")}\" --payload=quic_initial --lua-desync=fake:blob=quic_google:repeats=11 --new");
            configBuilder.AppendLine("--filter-udp=443 --filter-l7=quic --payload=quic_initial --lua-desync=fake:blob=fake_default_quic:repeats=11 --new");
            configBuilder.AppendLine("--filter-l7=wireguard,stun,discord --payload=wireguard_initiation,wireguard_cookie,stun,discord_ip_discovery --lua-desync=fake:blob=0x00000000000000000000000000000000:repeats=2");

            File.WriteAllText(configPath, configBuilder.ToString(), Encoding.UTF8);

            string exePath = Path.Combine(BaseDir, "bin", "winws2.exe");
            if (File.Exists(exePath))
            {
                // Для перенаправления вывода Verb="runas" (UAC окно) использовать НЕЛЬЗЯ.
                // Поэтому лаунчер Unbound САМ должен быть запущен от админа.
                _winwsProcess = new Process();
                _winwsProcess.StartInfo.FileName = exePath;
                _winwsProcess.StartInfo.Arguments = $"@\"{configPath}\"";

                // Настройки перехвата потока консоли
                _winwsProcess.StartInfo.UseShellExecute = false;
                _winwsProcess.StartInfo.RedirectStandardOutput = true;
                _winwsProcess.StartInfo.RedirectStandardError = true;
                _winwsProcess.StartInfo.CreateNoWindow = true;

                // Подписываемся на события вывода текста
                _winwsProcess.OutputDataReceived += (s, e) => { if (e.Data != null) OnLogReceived?.Invoke(e.Data); };
                _winwsProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) OnLogReceived?.Invoke($"[ERROR] {e.Data}"); };

                _winwsProcess.Start();

                // Начинаем асинхронное чтение
                _winwsProcess.BeginOutputReadLine();
                _winwsProcess.BeginErrorReadLine();

                GlobalState.Status = "Статус: Запущено";
                OnLogReceived?.Invoke("[System] Движок winws2 успешно инициализирован.");
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

            // На всякий случай подчищаем дубликаты в системе
            foreach (var p in Process.GetProcessesByName("winws2"))
            {
                try { p.Kill(); } catch { }
            }

            GlobalState.Status = "Статус: Остановлено";
            OnLogReceived?.Invoke("[System] Движок winws2 остановлен користувачем.");
        }
    }
}