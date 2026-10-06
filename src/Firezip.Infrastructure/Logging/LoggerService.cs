using System.Text.RegularExpressions;
using Firezip.Core.Interfaces;

namespace Firezip.Infrastructure.Logging;

public class LoggerService : ILoggingService
{
    private static readonly Regex PasswordMaskRegex = new(
        @"(password|pwd|secret|key)\s*[:=]\s*([^\s,]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _logDirectory;
    private readonly object _lock = new();

    public LoggerService(string? customLogDirectory = null)
    {
        if (!string.IsNullOrEmpty(customLogDirectory))
        {
            _logDirectory = customLogDirectory;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logDirectory = Path.Combine(appData, "Firezip", "Logs");
        }

        try
        {
            Directory.CreateDirectory(_logDirectory);
        }
        catch
        {
            // Fall back to temp path if localappdata cannot be accessed
            _logDirectory = Path.Combine(Path.GetTempPath(), "Firezip_Logs");
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public string GetLogDirectory() => _logDirectory;

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Debug(string message) => Write("DEBUG", message);

    public void Error(string message, Exception? ex = null)
    {
        var formatted = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}" : message;
        Write("ERROR", formatted);
    }

    private void Write(string level, string rawMessage)
    {
        try
        {
            // Sanitize sensitive credentials
            var sanitizedMessage = PasswordMaskRegex.Replace(rawMessage, "$1: [REDACTED]");
            var logLine = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {sanitizedMessage}{Environment.NewLine}";

            var fileName = $"firezip_{DateTime.UtcNow:yyyyMMdd}.log";
            var logPath = Path.Combine(_logDirectory, fileName);

            lock (_lock)
            {
                File.AppendAllText(logPath, logLine);
            }
        }
        catch
        {
            // Logging should never crash the host application
        }
    }
}
