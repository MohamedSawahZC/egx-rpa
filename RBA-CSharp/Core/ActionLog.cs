// =============================================================================
//  ActionLog.cs - optional detailed session log (every action + issue).
//  Files land in logs\ so the operator can share them for diagnosis.
// =============================================================================
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace RbaCore
{
    public static class ActionLog
    {
        private static readonly object Gate = new object();
        private static string _sessionPath = "";
        private static string _logsDir = "";
        private static string _installDir = "";
        private static bool _enabled = true;
        private static int _actionSeq;

        // Folder that contains RPA-V1.exe (USB / C:\RPA-V1-Setup), not AppData.
        public static string InstallDir()
        {
            lock (Gate)
            {
                if (!string.IsNullOrEmpty(_installDir) && Directory.Exists(_installDir))
                {
                    return _installDir;
                }
                try
                {
                    string loc = Assembly.GetExecutingAssembly().Location;
                    if (!string.IsNullOrEmpty(loc))
                    {
                        _installDir = Path.GetDirectoryName(loc) ?? "";
                    }
                }
                catch { _installDir = ""; }
                if (string.IsNullOrEmpty(_installDir))
                {
                    _installDir = Environment.CurrentDirectory ?? "";
                }
                return _installDir;
            }
        }

        // logs\ next to the EXE we launched. Fallback to LocalAppData only if the
        // install folder is not writable (rare on the coding PC / USB).
        public static string BesideExeLogsDir()
        {
            string install = InstallDir();
            if (!string.IsNullOrEmpty(install))
            {
                string dir = Path.Combine(install, "logs");
                if (TryEnsureWritable(dir)) { return dir; }
            }
            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RPA-V1", "logs");
            TryEnsureWritable(fallback);
            return fallback;
        }

        private static bool TryEnsureWritable(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
                string probe = Path.Combine(dir, ".write-ok");
                File.WriteAllText(probe, "ok", new UTF8Encoding(true));
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        public static bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public static string LogsDir
        {
            get { return _logsDir ?? ""; }
        }

        public static string SessionPath
        {
            get { return _sessionPath ?? ""; }
        }

        public static void Configure(string logsDir, bool enabled)
        {
            lock (Gate)
            {
                _logsDir = logsDir ?? "";
                _enabled = enabled;
                if (!string.IsNullOrEmpty(_logsDir) && !Directory.Exists(_logsDir))
                {
                    try { Directory.CreateDirectory(_logsDir); } catch { }
                }
            }
        }

        // Start a new session file for one run (entry test / cycle / save).
        public static string BeginSession(string kind)
        {
            lock (Gate)
            {
                _actionSeq = 0;
                if (string.IsNullOrEmpty(_logsDir)) { return ""; }
                try
                {
                    if (!Directory.Exists(_logsDir)) { Directory.CreateDirectory(_logsDir); }
                    string safe = string.IsNullOrWhiteSpace(kind) ? "run" : kind;
                    safe = safe.Replace(" ", "-");
                    string name = "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + safe + ".log";
                    _sessionPath = Path.Combine(_logsDir, name);
                    StringBuilder hdr = new StringBuilder();
                    hdr.AppendLine("============================================================");
                    hdr.AppendLine("RPA-V1 detailed action log");
                    hdr.AppendLine("Started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    hdr.AppendLine("Machine: " + Environment.MachineName);
                    hdr.AppendLine("User:    " + Environment.UserName);
                    hdr.AppendLine("Kind:    " + kind);
                    hdr.AppendLine("Enabled: " + _enabled);
                    hdr.AppendLine("============================================================");
                    hdr.AppendLine();
                    File.WriteAllText(_sessionPath, hdr.ToString(), new UTF8Encoding(true));
                    return _sessionPath;
                }
                catch
                {
                    _sessionPath = "";
                    return "";
                }
            }
        }

        public static void EndSession(string summary)
        {
            Write("SESSION_END", string.IsNullOrEmpty(summary) ? "finished" : summary);
            lock (Gate)
            {
                if (string.IsNullOrEmpty(_sessionPath) || !File.Exists(_sessionPath)) { return; }
                try
                {
                    File.AppendAllText(_sessionPath,
                        Environment.NewLine + "---- end " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ----" + Environment.NewLine,
                        new UTF8Encoding(true));
                }
                catch { }
            }
        }

        public static void Write(string level, string message)
        {
            if (string.IsNullOrEmpty(message)) { return; }
            if (string.IsNullOrEmpty(level)) { level = "INFO"; }

            string stamp = DateTime.Now.ToString("HH:mm:ss.fff");
            int seq = Interlocked.Increment(ref _actionSeq);
            string line = stamp + " #" + seq.ToString("0000") + " [" + level.ToUpperInvariant() + "] " + message;

            // Always append to daily rollup (lightweight history).
            AppendDaily(line);

            // Detailed session file when enabled (or always for ERROR/WARN).
            bool force = string.Equals(level, "ERROR", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(level, "WARN", StringComparison.OrdinalIgnoreCase);
            if (_enabled || force)
            {
                AppendSession(line);
            }
        }

        public static string FindLatestSession()
        {
            lock (Gate)
            {
                if (!string.IsNullOrEmpty(_sessionPath) && File.Exists(_sessionPath))
                {
                    return _sessionPath;
                }
                if (string.IsNullOrEmpty(_logsDir) || !Directory.Exists(_logsDir)) { return ""; }
                string[] files = Directory.GetFiles(_logsDir, "session_*.log");
                if (files == null || files.Length == 0) { return ""; }
                Array.Sort(files);
                return files[files.Length - 1];
            }
        }

        private static void AppendDaily(string line)
        {
            string dir = _logsDir;
            if (string.IsNullOrEmpty(dir)) { return; }
            try
            {
                if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
                string path = Path.Combine(dir, "rba_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(true));
            }
            catch { }
        }

        private static void AppendSession(string line)
        {
            string path = _sessionPath;
            if (string.IsNullOrEmpty(path)) { return; }
            try
            {
                File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(true));
            }
            catch { }
        }
    }
}
