using System;
using System.Diagnostics;
using System.IO;

namespace Unbound
{
    public static class TgProxyManager
    {
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static Process? _tgProcess;
        public static event Action<string>? OnLogReceived;

        public static void Start(string port, string customWorker)
        {
            Stop();
            string exePath = Path.Combine(BaseDir, "bin", "TgWsProxy_windows.exe");

            if (!File.Exists(exePath))
            {
                OnLogReceived?.Invoke("[Error] TgWsProxy_windows.exe не найден!");
                return;
            }

            _tgProcess = new Process();
            _tgProcess.StartInfo.FileName = exePath;

            // Запуск без Fake TLS маскировки
            string args = $"--port {port} --no-cfproxy";
            if (!string.IsNullOrWhiteSpace(customWorker))
                args += $" --cfproxy-worker-domain {customWorker}";

            _tgProcess.StartInfo.UseShellExecute = false;
            _tgProcess.StartInfo.RedirectStandardOutput = true;
            _tgProcess.StartInfo.RedirectStandardError = true;
            _tgProcess.StartInfo.CreateNoWindow = true;
            _tgProcess.OutputDataReceived += (s, e) => { if (e.Data != null) OnLogReceived?.Invoke($"[TG] {e.Data}"); };
            _tgProcess.Start();
            _tgProcess.BeginOutputReadLine();

            OnLogReceived?.Invoke($"[System] Telegram-прокси запущен на порту {port} (без маскировки).");
        }

        public static void Stop()
        {
            try { _tgProcess?.Kill(); } catch { }
            foreach (var p in Process.GetProcessesByName("TgWsProxy_windows"))
            { try { p.Kill(); } catch { } }
        }
    }
}