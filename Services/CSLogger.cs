using System;
using System.Diagnostics;
using System.IO;
using Bannerlord.ButterLib.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaleWorlds.MountAndBlade;

namespace CulturedStartReloaded.Services
{
    public static class CSLogger
    {
        private const string ModName = "CulturedStartReloaded";
        private const int MaxLogSizeBytes = 10 * 1024 * 1024;

        private static ILogger? _butterLibLogger;
        private static string? _logFilePath;
        private static readonly object _lock = new();
        private static bool _isInitialized;
        private static bool? _loadedByRunningGame;

        public static void Initialize(MBSubModuleBase subModule)
        {
            if (_isInitialized) return;

            SetupFileLogging();

            try
            {
                subModule.AddSerilogLoggerProvider("cultured_start_reloaded.log", new[] { "CulturedStartReloaded.*" });
            }
            catch (Exception)
            {
                // ButterLib Serilog unavailable: file logging is already set up
            }

            _isInitialized = true;
            Log("INFO", "Logger initialized.");
        }

        public static void ResolveLogger(MBSubModuleBase subModule)
        {
            try
            {
                var provider = subModule.GetServiceProvider();
                if (provider != null)
                {
                    _butterLibLogger = provider.GetService<ILoggerFactory>()?.CreateLogger("CulturedStartReloaded");
                    Log("INFO", "ButterLib logger resolved successfully.");
                }
            }
            catch (Exception)
            {
                Log("WARN", "Failed to resolve ButterLib logger. Continuing with file logging.");
            }
        }

        public static void Info(string message) => Log("INFO", message);
        public static void Warn(string message) => Log("WARN", message);
        public static void Error(string message) => Log("ERROR", message);
        public static void Debug(string message) => Log("DEBUG", message);

        public static void Error(string message, Exception ex)
        {
            if (_butterLibLogger != null)
            {
                _butterLibLogger.LogError(ex, message);
            }

            WriteToFile("ERROR", $"{message} | Exception: {ex}");
        }

        private static void Log(string level, string message)
        {
            if (_butterLibLogger != null)
            {
                switch (level)
                {
                    case "INFO": _butterLibLogger.LogInformation(message); break;
                    case "WARN": _butterLibLogger.LogWarning(message); break;
                    case "ERROR": _butterLibLogger.LogError(message); break;
                    case "DEBUG": _butterLibLogger.LogDebug(message); break;
                }
            }

            WriteToFile(level, message);
        }

        private static void WriteToFile(string level, string message)
        {
            if (_logFilePath == null) SetupFileLogging();
            if (_logFilePath == null) return;

            lock (_lock)
            {
                try
                {
                    var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(_logFilePath, line);
                }
                catch
                {
                    // Cannot log if file writing fails: avoid infinite recursion
                }
            }
        }

        private static void SetupFileLogging()
        {
            try
            {
                _loadedByRunningGame ??= IsLoadedByRunningGame();
                if (_loadedByRunningGame != true) return;

                var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var modLogsPath = Path.Combine(docsPath, "Mount and Blade II Bannerlord", "Configs", "ModLogs");

                if (!Directory.Exists(modLogsPath))
                    Directory.CreateDirectory(modLogsPath);

                var fileName = $"{ModName}{DateTime.Now:yyyyMMdd}.log";
                _logFilePath = Path.Combine(modLogsPath, fileName);

                if (File.Exists(_logFilePath) && new FileInfo(_logFilePath).Length > MaxLogSizeBytes)
                {
                    File.Delete(_logFilePath);
                }
            }
            catch
            {
                // If we can't set up file logging, _logFilePath stays null
            }
        }

        // The Documents log folder is a junction into the running instance only while Bannerlord
        // Environment Manager is driving a launch. Loaded by anything else it is the resting
        // install's own store, so the fallback stays silent unless a game process is running this
        // assembly out of that same install's Modules folder.
        private static bool IsLoadedByRunningGame()
        {
            try
            {
                var gameFolder = FindGameFolder(Path.GetDirectoryName(typeof(CSLogger).Assembly.Location));
                if (gameFolder == null) return false;

                using var process = Process.GetCurrentProcess();
                var executablePath = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(executablePath)) return false;

                return IsUnder(executablePath!, Path.Combine(gameFolder, "bin"));
            }
            catch
            {
                return false;
            }
        }

        private static string? FindGameFolder(string? startDirectory)
        {
            var directory = string.IsNullOrEmpty(startDirectory) ? null : new DirectoryInfo(startDirectory!);

            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Modules", "Native", "SubModule.xml")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            return null;
        }

        private static bool IsUnder(string path, string directory)
        {
            var full = Path.GetFullPath(path);
            var prefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
