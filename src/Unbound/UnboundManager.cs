using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Unbound
{
    public static class UnboundManager
    {
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static Process? _winwsProcess;
        private static Process? _tgProcess;

        public static event Action<string>? OnLogReceived;

        // --- МОДУЛЬ ZAPRET ---
        public static void StartZapret()
        {
            StopZapret();
            string configPath = Path.Combine(BaseDir, "generated_config.txt");
            string binPath = Path.Combine(BaseDir, "bin");

            // Здесь должна быть логика генерации конфига (как мы писали ранее)
            // ... (GenerateConfig) ...

            _winwsProcess = new Process();
            _winwsProcess.StartInfo.FileName = Path.Combine(binPath, "winws2.exe");
            _winwsProcess.StartInfo.Arguments = $"@\"{configPath}\"";
            _winwsProcess.StartInfo.UseShellExecute = false;
            _winwsProcess.StartInfo.RedirectStandardOutput = true;
            _winwsProcess.StartInfo.CreateNoWindow = true;
            _winwsProcess.OutputDataReceived += (s, e) => { if (e.Data != null) OnLogReceived?.Invoke($"[Zapret] {e.Data}"); };
            _winwsProcess.Start();
            _winwsProcess.BeginOutputReadLine();
        }

        public static void StopZapret()
        {
            _winwsProcess?.Kill();
            foreach (var p in Process.GetProcessesByName("winws2")) p.Kill();
        }

        // --- МОДУЛЬ TELEGRAM PROXY ---
        public static void StartTgProxy(string port, string fakeTlsDomain, bool useCf, string customWorker, string poolSize)
        {
            StopTgProxy();
            string exePath = Path.Combine(BaseDir, "bin", "tg_ws_proxy.exe");

            if (!File.Exists(exePath)) { OnLogReceived?.Invoke("[Error] tg_ws_proxy.exe не найден!"); return; }

            _tgProcess = new Process();
            _tgProcess.StartInfo.FileName = exePath;
            _tgProcess.StartInfo.Arguments = $"--port {port} --fake-tls-domain {fakeTlsDomain} --pool-size {poolSize} " +
                                             (useCf ? "" : "--no-cfproxy ") +
                                             (!string.IsNullOrEmpty(customWorker) ? $"--cfproxy-worker-domain {customWorker}" : "");

            _tgProcess.StartInfo.UseShellExecute = false;
            _tgProcess.StartInfo.RedirectStandardOutput = true;
            _tgProcess.StartInfo.CreateNoWindow = true;
            _tgProcess.OutputDataReceived += (s, e) => { if (e.Data != null) OnLogReceived?.Invoke($"[TG] {e.Data}"); };
            _tgProcess.Start();
            _tgProcess.BeginOutputReadLine();
        }

        public static void StopTgProxy()
        {
            _tgProcess?.Kill();
            foreach (var p in Process.GetProcessesByName("tg_ws_proxy")) p.Kill();
        }
    }
}