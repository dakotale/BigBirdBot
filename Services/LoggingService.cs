using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Services
{
    public class LoggingService
    {
        public enum FilterSeverity
        {
            All,
            NoDebug,
            Extended,
            Production,
            None
        }

        public enum OutputType
        {
            None,
            Console,
            LogFile,
            All
        }

        public enum Severity : int
        {
            Debug = ConsoleColor.DarkBlue,
            Info = ConsoleColor.DarkGreen,
            Warning = ConsoleColor.DarkYellow,
            Error = ConsoleColor.DarkRed
        }

        private readonly OutputType _outputType;
        private readonly FilterSeverity _filterSeverity;
        private readonly string? _logPath;
        private readonly LoggingService _self; // For event handlers

        public LoggingService(IServiceProvider services)
            : this(services, OutputType.Console, FilterSeverity.All, null)
        {
        }

        public LoggingService(IServiceProvider services, OutputType outputType, FilterSeverity filterSeverity)
            : this(services, outputType, filterSeverity, null)
        {
        }

        public LoggingService(IServiceProvider services, OutputType outputType, FilterSeverity filterSeverity, string? logPath)
        {
            _outputType = outputType;
            _filterSeverity = filterSeverity;
            _logPath = logPath;

            var commandService = services.GetRequiredService<CommandService>();
            var client = services.GetRequiredService<DiscordSocketClient>();
            var interactionService = services.GetService<InteractionService>();

            commandService.Log += OnDiscordLogAsync;
            client.Log += OnDiscordLogAsync;
            if (interactionService != null)
                interactionService.Log += OnDiscordLogAsync;
        }

        public Task DebugAsync(string message, [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            LogAsync(Severity.Debug, message, caller, file, line);

        public Task InfoAsync(string message, [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            string timestampedMessage = $"{DateTime.Now:G} - {message}";
            return LogAsync(Severity.Info, timestampedMessage, caller, file, line);
        }

        public Task WarningAsync(string message, [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            LogAsync(Severity.Warning, message, caller, file, line);

        public Task ErrorAsync(Exception? ex)
        {
            if (ex == null) return Task.CompletedTask;

            var st = new StackTrace(ex, true);
            var sf = st.GetFrame(st.FrameCount - 1);

            return LogAsync(
                Severity.Error,
                $"{ex.GetType().FullName} - {ex.Message}{Environment.NewLine}{ex.StackTrace}",
                sf?.GetMethod()?.Name ?? "UnknownMethod",
                sf?.GetFileName() ?? "UnknownFile",
                sf?.GetFileLineNumber() ?? 0);
        }

        private static bool ShouldLog(Severity severity, FilterSeverity filterSeverity) => filterSeverity switch
        {
            FilterSeverity.All => true,
            FilterSeverity.NoDebug => severity != Severity.Debug,
            FilterSeverity.Extended => severity == Severity.Warning || severity == Severity.Error,
            FilterSeverity.Production => severity == Severity.Error,
            FilterSeverity.None => false,
            _ => throw new ArgumentOutOfRangeException(nameof(filterSeverity), filterSeverity, null)
        };

        private Task LogAsync(Severity severity, string message,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (string.IsNullOrWhiteSpace(message) || _outputType == OutputType.None || !ShouldLog(severity, _filterSeverity))
                return Task.CompletedTask;

            string prefix = $"{DateTime.Now:HH:mm:ss} [{Path.GetFileNameWithoutExtension(file)}->{caller} L{line}] ";

            if (_outputType == OutputType.Console || _outputType == OutputType.All)
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = (ConsoleColor)severity;
                Console.Write(prefix);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(message);
                Console.ForegroundColor = originalColor;
            }

            if (!string.IsNullOrEmpty(_logPath) && (_outputType == OutputType.LogFile || _outputType == OutputType.All))
            {
                try
                {
                    // Append text instead of overwriting file
                    File.AppendAllText(_logPath, prefix + message + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    // Optionally handle file IO exceptions, maybe fallback to console
                    Console.WriteLine($"Logging to file failed: {ex.Message}");
                }
            }

            return Task.CompletedTask;
        }

        private Task OnDiscordLogAsync(LogMessage log)
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDir);

                string fileName = Path.Combine(logDir, $"ExceptionLog_{DateTime.Now:yyyy_MM_dd}.txt");
                var output = new StringBuilder();

                if (!string.IsNullOrEmpty(log.Message))
                    output.AppendLine($"{DateTime.Now:HH:mm:ss}: {log.Message}");

                if (log.Exception != null)
                    output.AppendLine(log.Exception.ToString());

                if (output.Length > 0)
                    File.AppendAllText(fileName, output + Environment.NewLine);

                return Task.CompletedTask;
            }
            catch
            {
                // Swallow exceptions from logging to avoid crash loops
                return Task.CompletedTask;
            }
        }
    }
}
