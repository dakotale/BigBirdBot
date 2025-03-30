using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

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

        public enum Severity
        {
            Debug = ConsoleColor.DarkBlue,
            Info = ConsoleColor.DarkGreen,
            Warning = ConsoleColor.DarkYellow,
            Error = ConsoleColor.DarkRed
        }

        private readonly OutputType _debugType = OutputType.Console;

        private readonly FilterSeverity _filterSeverity = FilterSeverity.All;

        private readonly string? _logPath;

        public LoggingService(IServiceProvider services)
        {
            services.GetRequiredService<CommandService>().Log += LogAsync;
            services.GetRequiredService<DiscordSocketClient>().Log += LogAsync;
            services.GetRequiredService<InteractionService>().Log += LogAsync;
        }

        public LoggingService(IServiceProvider services, OutputType outputType, FilterSeverity filterSeverity)
        {
            _debugType = outputType;
            _filterSeverity = filterSeverity;

            services.GetRequiredService<CommandService>().Log += LogAsync;
            services.GetRequiredService<DiscordSocketClient>().Log += LogAsync;
        }

        public LoggingService(IServiceProvider services, OutputType outputType, FilterSeverity filterSeverity, string logPath)
        {
            _debugType = outputType;
            _logPath = logPath;
            _filterSeverity = filterSeverity;

            services.GetRequiredService<CommandService>().Log += LogAsync;
            services.GetRequiredService<DiscordSocketClient>().Log += LogAsync;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining & MethodImplOptions.AggressiveOptimization)]
        public Task DebugAsync(string message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return LogAsync(Severity.Debug, message, caller, file, line);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining & MethodImplOptions.AggressiveOptimization)]
        public Task InfoAsync(string message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(DateTime.Now.ToLongDateString());
            sb.Append("-");
            sb.Append(message);
            return LogAsync(Severity.Info, sb.ToString(), caller, file, line);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining & MethodImplOptions.AggressiveOptimization)]
        public Task WarningAsync(string message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            return LogAsync(Severity.Warning, message, caller, file, line);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining & MethodImplOptions.AggressiveOptimization)]
        public Task ErrorAsync(Exception? ex)
        {
            if (ex == null)
                return Task.CompletedTask;

            var st = new StackTrace(ex, true);
            var sf = st.GetFrame(st.FrameCount - 1);

            return LogAsync(Severity.Error, $"{ex.GetType().FullName} - {ex.Message}{Environment.NewLine}{ex.StackTrace}", sf!.GetMethod()!.Name, sf.GetFileName()!, sf.GetFileLineNumber());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining & MethodImplOptions.AggressiveOptimization)]
        private static bool ShouldLog(in Severity severity, in FilterSeverity filterSeverity)
        {
            return filterSeverity switch
            {
                FilterSeverity.All => true,
                FilterSeverity.NoDebug => severity is not Severity.Debug,
                FilterSeverity.Extended => severity is Severity.Warning or Severity.Error,
                FilterSeverity.Production => severity is Severity.Error,
                FilterSeverity.None => false,
                _ => throw new ArgumentOutOfRangeException(nameof(filterSeverity), filterSeverity, null)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining & MethodImplOptions.AggressiveOptimization)]
        private Task LogAsync(Severity severity, string message = "", [CallerMemberName] string caller = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (string.IsNullOrEmpty(message) || _debugType == OutputType.None || !ShouldLog(in severity, in _filterSeverity))
                return Task.CompletedTask;

            if (_debugType is OutputType.Console or OutputType.All)
            {
                Console.ForegroundColor = (ConsoleColor)severity;
                Console.Write($@"{DateTime.Now.ToLongTimeString()} [{Path.GetFileNameWithoutExtension(file)}->{caller} L{line}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($@"{message}{Environment.NewLine}");
            }

            if (_logPath != null && _debugType is OutputType.LogFile or OutputType.All)
                File.WriteAllText(_logPath, $@"[{Path.GetFileNameWithoutExtension(file)}->{caller} L{line}] {message}");

            return Task.CompletedTask;
        }

        private static Task LogAsync(LogMessage log)
        {
            string fileName = @"Logs\ExceptionLog_" + DateTime.Now.Date.ToString("yyyy_MM_dd") + ".txt";
            string severity = "", source = "", message = "", exception = "", output = "";

            if (!string.IsNullOrEmpty(log.Message))
                message = log.Message;

            if (log.Exception != null)
            {
                severity = log.Severity.ToString() ?? "";
                source = string.IsNullOrEmpty(log.Source) ? "" : log.Source;
                message = string.IsNullOrEmpty(log.Message) ? "" : log.Message;
                exception = log.Exception.ToString() ?? "";

                output = DateTime.Now.ToString("HH:mm:ss") + ": " + message;
                output += Environment.NewLine + exception + Environment.NewLine + Environment.NewLine;
            }

            if (!File.Exists(fileName))
            {
                using (StreamWriter sw = File.CreateText(fileName))
                {
                    sw.WriteLine($"Bot Exceptions for {DateTime.Now.ToString("yyyy-MM-dd")}");
                }
            }
            using (StreamWriter sw = File.AppendText(fileName))
            {
                sw.WriteLine(output);
            }

            return Task.CompletedTask;
        }
    }
}
