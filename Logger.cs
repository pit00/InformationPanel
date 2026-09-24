using System;
using System.IO;
using System.Text;

namespace MusicBeePlugin
{
    internal static class Logger
    {
        private static string _logPath;
        private static readonly object _lock = new object();

        public static void Initialize(string path)
        {
            _logPath = path;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Logger] Failed to initialize log directory: {ex}");
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Error(string message, Exception ex = null)
        {
            Write("ERROR", message, ex);
        }

        private static void Write(string level, string message, Exception ex)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}");
                if (ex != null)
                {
                    sb.Append($"{Environment.NewLine}{ex}");
                }
                sb.Append(Environment.NewLine);

                string logEntry = sb.ToString();

                System.Diagnostics.Debug.Write(logEntry);

                if (!string.IsNullOrEmpty(_logPath))
                {
                    lock (_lock)
                    {
                        File.AppendAllText(_logPath, logEntry);
                    }
                }
            }
            catch
            {
                // never let logging crash the host
            }
        }
    }
}
