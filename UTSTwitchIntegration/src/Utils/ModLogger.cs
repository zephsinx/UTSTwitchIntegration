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
        Debug = 3,
    }

    public static class ModLogger
    {
        private static MelonLogger.Instance logger;
        private static LogLevel currentLogLevel = LogLevel.Info;

        public static void Initialize(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
        }

        public static void SetLogLevel(LogLevel level)
        {
            currentLogLevel = level;
        }

        public static void Debug(string message)
        {
            if (currentLogLevel >= LogLevel.Debug)
            {
                logger?.Msg($"[DEBUG] {message}");
            }
        }

        public static void Info(string message)
        {
            if (currentLogLevel >= LogLevel.Info)
            {
                logger?.Msg($"[INFO] {message}");
            }
        }

        public static void Warning(string message)
        {
            if (currentLogLevel >= LogLevel.Warning)
            {
                logger?.Warning($"[WARNING] {message}");
            }
        }

        public static void Error(string message)
        {
            logger?.Error($"[ERROR] {message}");
        }

        public static void Success(string message)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                logger?.Msg($"[✓ SUCCESS] {message}");
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
    }
}

