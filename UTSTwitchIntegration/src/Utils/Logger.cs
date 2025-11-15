#nullable disable
using System;
using MelonLoader;

namespace UTSTwitchIntegration.Utils
{
    /// <summary>
    /// Log levels for controlling log verbosity
    /// </summary>
    public enum LogLevel
    {
        Error = 0,
        Warning = 1,
        Info = 2,
        Debug = 3
    }

    public static class Logger
    {
        private static MelonLogger.Instance _logger;
        private static LogLevel _currentLogLevel = LogLevel.Info;

        public static void Initialize(MelonLogger.Instance logger)
        {
            _logger = logger;
        }

        public static void SetLogLevel(LogLevel level)
        {
            _currentLogLevel = level;
        }

        public static LogLevel GetLogLevel()
        {
            return _currentLogLevel;
        }

        public static void Debug(string message)
        {
            if (_currentLogLevel >= LogLevel.Debug)
            {
                _logger?.Msg($"[DEBUG] {message}");
            }
        }

        public static void Info(string message)
        {
            if (_currentLogLevel >= LogLevel.Info)
            {
                _logger?.Msg($"[INFO] {message}");
            }
        }

        public static void Warning(string message)
        {
            if (_currentLogLevel >= LogLevel.Warning)
            {
                _logger?.Warning($"[WARNING] {message}");
            }
        }

        public static void Error(string message)
        {
            _logger?.Error($"[ERROR] {message}");
        }

        public static void Success(string message)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                _logger?.Msg($"[✓ SUCCESS] {message}");
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
    }
}

