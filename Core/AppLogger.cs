using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SimRacingHub.Core
{
    public class AppLogger : ObservableObject
    {
        private const int MaxQueuedEntries = 5000;
        private const int FlushIntervalMs = 250;
        private const long MaxLogFileBytes = 5 * 1024 * 1024;
        private const int RetentionDays = 7;
        private const int MaxWriteFailures = 10;
        private const int MaxLinesPerFlush = 2000;

        public static AppLogger Instance { get; } = new AppLogger();

        private readonly ConcurrentQueue<string> _queue = new();
        private readonly AutoResetEvent _signal = new(false);
        private readonly object _fileLock = new();

        private int _droppedEntries;
        private int _writeFailures;
        private bool _fileLoggingDisabled;
        private bool _headerWritten;
        private volatile string? _currentLogFilePath;

        public string CurrentLogFilePath
        {
            get
            {
                string? actual = _currentLogFilePath;
                if (actual != null) return actual;
                try { return BuildLogFilePath(StoragePaths.LogsDirectory); }
                catch { return string.Empty; }
            }
        }

        private string? _lastStatusMessage;
        public string? LastStatusMessage
        {
            get => _lastStatusMessage;
            set => SetProperty(ref _lastStatusMessage, value);
        }

        private AppLogger()
        {
            var writer = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "AppLogger.Writer",
                Priority = ThreadPriority.BelowNormal
            };
            writer.Start();
        }

        public void LogInfo(string message, bool showInStatusBar = true)
        {
            Debug.WriteLine($"[INFO] {message}");
            Enqueue("INFO", message, null);
            if (showInStatusBar) SetStatus(message);
        }

        public void LogWarning(string message)
        {
            Debug.WriteLine($"[WARN] {message}");
            Enqueue("WARN", message, null);
            SetStatus(message);
        }

        public void LogError(string message, Exception? ex = null)
        {
            Debug.WriteLine($"[ERROR] {message}{(ex != null ? ": " + ex.Message : string.Empty)}");
            Enqueue("ERROR", message, ex);
            SetStatus(message);
        }

        public void LogCritical(string message, Exception? ex = null)
        {
            Debug.WriteLine($"[CRITICAL] {message}{(ex != null ? ": " + ex.Message : string.Empty)}");
            Enqueue("CRIT", message, ex);
            Flush();
        }

        public void Flush()
        {
            try { Drain(drainAll: true); }
            catch (Exception ex) { Debug.WriteLine($"[AppLogger] flush failed: {ex.Message}"); }
        }

        public void ClearStatus()
        {
            SetStatus(null);
        }

        private void Enqueue(string level, string message, Exception? ex)
        {
            if (_queue.Count >= MaxQueuedEntries)
            {
                Interlocked.Increment(ref _droppedEntries);
                return;
            }

            _queue.Enqueue(Format(level, message, ex));
            try { _signal.Set(); } catch (ObjectDisposedException) { }
        }

        private void SetStatus(string? message)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            try
            {
                app.Dispatcher.InvokeAsync(() => LastStatusMessage = message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLogger] status dispatch failed: {ex.Message}");
            }
        }

        private void WriterLoop()
        {
            WriteToFile(string.Empty);

            while (true)
            {
                try
                {
                    _signal.WaitOne(FlushIntervalMs);
                    Drain(drainAll: false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AppLogger] writer loop error: {ex.Message}");
                    Thread.Sleep(1000);
                }
            }
        }

        private void Drain(bool drainAll)
        {
            if (_fileLoggingDisabled)
            {
                while (_queue.TryDequeue(out _)) { }
                return;
            }

            int dropped = Interlocked.Exchange(ref _droppedEntries, 0);
            if (dropped == 0 && _queue.IsEmpty) return;

            var sb = new StringBuilder();
            if (dropped > 0)
            {
                sb.Append(Format("WARN", $"{dropped} log entries dropped (log writer could not keep up)", null));
            }

            int limit = drainAll ? MaxQueuedEntries * 4 : MaxLinesPerFlush;
            int count = 0;
            while (count < limit && _queue.TryDequeue(out string? line))
            {
                sb.Append(line);
                count++;
            }

            if (sb.Length > 0) WriteToFile(sb.ToString());
        }

        private void WriteToFile(string text)
        {
            lock (_fileLock)
            {
                if (_fileLoggingDisabled) return;

                try
                {
                    string dir = StoragePaths.LogsDirectory;
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    if (!_headerWritten)
                    {
                        _headerWritten = true;
                        PruneOldLogs(dir);
                        text = BuildSessionHeader() + text;
                    }

                    string path = BuildLogFilePath(dir);
                    RollIfTooLarge(path);
                    _currentLogFilePath = path;

                    using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(fs, new UTF8Encoding(false));
                    writer.Write(text);

                    _writeFailures = 0;
                }
                catch (Exception ex)
                {
                    _writeFailures++;
                    Debug.WriteLine($"[AppLogger] write failed ({_writeFailures}): {ex.Message}");

                    if (_writeFailures >= MaxWriteFailures)
                    {
                        _fileLoggingDisabled = true;
                        Debug.WriteLine("[AppLogger] file logging disabled after repeated failures");
                    }
                }
            }
        }

        private static string BuildLogFilePath(string dir)
        {
            return Path.Combine(dir, $"vwheelhub-{DateTime.Now:yyyy-MM-dd}.log");
        }

        private static void RollIfTooLarge(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < MaxLogFileBytes) return;

            string dir = Path.GetDirectoryName(path) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(path);

            for (int i = 1; i < 100; i++)
            {
                string candidate = Path.Combine(dir, $"{name}.{i}.log");
                if (!File.Exists(candidate))
                {
                    File.Move(path, candidate);
                    return;
                }
            }
        }

        private static void PruneOldLogs(string dir)
        {
            try
            {
                DateTime cutoff = DateTime.Now.AddDays(-RetentionDays);
                foreach (string file in Directory.GetFiles(dir, "vwheelhub-*.log"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) < cutoff) File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AppLogger] could not prune '{file}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLogger] prune failed: {ex.Message}");
            }
        }

        private static string BuildSessionHeader()
        {
            string version;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                          ?? asm.GetName().Version?.ToString()
                          ?? "unknown";
            }
            catch { version = "unknown"; }

            var sb = new StringBuilder();
            sb.AppendLine("============================================================");
            sb.AppendLine($" vWheel Hub session started {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($" Version : {version}");
            sb.AppendLine($" OS      : {Environment.OSVersion.VersionString} (64-bit OS: {Environment.Is64BitOperatingSystem}, 64-bit process: {Environment.Is64BitProcess})");
            sb.AppendLine($" Runtime : .NET {Environment.Version}");
            sb.AppendLine($" Machine : {Environment.ProcessorCount} CPU cores, PID {Environment.ProcessId}");
            sb.AppendLine("============================================================");
            return sb.ToString();
        }

        private static string Format(string level, string message, Exception? ex)
        {
            var sb = new StringBuilder(192);
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append(" [").Append(level.PadRight(5)).Append("] [T");
            sb.Append(Environment.CurrentManagedThreadId).Append("] ");
            sb.AppendLine(message);

            if (ex != null) sb.AppendLine(ex.ToString());

            return sb.ToString();
        }
    }
}
