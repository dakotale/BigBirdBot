using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DiscordBot.Constants;
using System.Data.SqlClient;
using System;

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
            return LogAsync(Severity.Info, message, caller, file, line);
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
            Console.ForegroundColor = log.Exception != null ? (ConsoleColor)Severity.Error : ConsoleColor.Cyan;
            Console.Write($"{DateTime.Now.ToLongTimeString()} [{log.Source}] ");
            Console.ForegroundColor = ConsoleColor.White;

            if (!string.IsNullOrEmpty(log.Message))
                Console.WriteLine($"{log.Message}");
            if (log.Exception != null)
            {
                Console.WriteLine(log.Exception.ToString());

                StoredProcedure stored = new StoredProcedure();
                string connStr = Constants.Constants.discordBotConnStr;
                List<SqlParameter> parameters = new List<SqlParameter>();

                string severity = log.Severity.ToString() ?? "";
                string source = string.IsNullOrEmpty(log.Source) ? "" : log.Source;
                string message = string.IsNullOrEmpty(log.Message) ? "" : log.Message;
                string exception = log.Exception.ToString() ?? "";

                parameters.Add(new SqlParameter("@Severity", severity));
                parameters.Add(new SqlParameter("@Source", source));
                parameters.Add(new SqlParameter("@Message", message));
                parameters.Add(new SqlParameter("@Exception", exception));

                stored.UpdateCreate(connStr, "AddLog", parameters);
            }

            return Task.CompletedTask;
        }
    }
}
