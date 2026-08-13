using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Services;

/// <summary>
/// Structured logging service supporting console output, file output, or both.
/// Integrates with Discord.Net's <see cref="LogMessage"/> event pipeline.
/// Thread-safe for concurrent log calls.
/// </summary>
public sealed class LoggingService
{

    public enum FilterSeverity { All, NoDebug, Extended, Production, None }
    public enum OutputType { None, Console, LogFile, All }

    /// <summary>
    /// Integer value maps directly to <see cref="ConsoleColor"/> for zero-cost
    /// casting when writing coloured output.
    /// </summary>
    public enum Severity
    {
        Debug = ConsoleColor.DarkBlue,
        Info = ConsoleColor.DarkGreen,
        Warning = ConsoleColor.DarkYellow,
        Error = ConsoleColor.DarkRed
    }


    private readonly OutputType _outputType;
    private readonly FilterSeverity _filterSeverity;
    private readonly string? _logPath;
    private readonly string _discordLogDir;

    /// <summary>Serialises all console writes to prevent interleaved colour changes.</summary>
    private static readonly Lock _consoleLock = new();

    /// <summary>Serialises file appends to prevent torn writes under concurrency.</summary>
    private static readonly Lock _fileLock = new();


    /// <summary>Creates a console-only logger that logs everything — the default used when the bot is registered via DI.</summary>
    public LoggingService(IServiceProvider services)
        : this(services, OutputType.Console, FilterSeverity.All, null) { }

    /// <summary>Creates a logger with a custom output type and severity filter, but no file output path.</summary>
    public LoggingService(IServiceProvider services, OutputType outputType, FilterSeverity filterSeverity)
        : this(services, outputType, filterSeverity, null) { }

    /// <summary>Creates a fully-configured logger and subscribes it to Discord.NET's CommandService/DiscordSocketClient/InteractionService log events.</summary>
    public LoggingService(
        IServiceProvider services,
        OutputType outputType,
        FilterSeverity filterSeverity,
        string? logPath)
    {
        _outputType = outputType;
        _filterSeverity = filterSeverity;
        _logPath = logPath;
        _discordLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        services.GetRequiredService<CommandService>().Log += OnDiscordLogAsync;
        services.GetRequiredService<DiscordSocketClient>().Log += OnDiscordLogAsync;
        services.GetService<InteractionService>()?.Log += OnDiscordLogAsync;
    }


    /// <summary>Logs a Debug-severity message. Caller/file/line are captured automatically via compiler attributes.</summary>
    public Task DebugAsync(string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0) =>
        LogAsync(Severity.Debug, message, caller, file, line);

    /// <summary>Logs an Info-severity message. Caller/file/line are captured automatically via compiler attributes.</summary>
    public Task InfoAsync(string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0) =>
        LogAsync(Severity.Info, message, caller, file, line);

    /// <summary>Logs a Warning-severity message. Caller/file/line are captured automatically via compiler attributes.</summary>
    public Task WarningAsync(string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0) =>
        LogAsync(Severity.Warning, message, caller, file, line);

    /// <summary>Logs an exception at Error severity, using its innermost stack frame as the caller/file/line context.</summary>
    public Task ErrorAsync(Exception? ex)
    {
        if (ex is null) return Task.CompletedTask;

        var st = new StackTrace(ex, fNeedFileInfo: true);
        var frame = st.GetFrame(st.FrameCount - 1);
        string msg = $"{ex.GetType().FullName} - {ex.Message}{Environment.NewLine}{ex.StackTrace}";

        return LogAsync(
            Severity.Error, msg,
            frame?.GetMethod()?.Name ?? "UnknownMethod",
            frame?.GetFileName() ?? "UnknownFile",
            frame?.GetFileLineNumber() ?? 0);
    }


    /// <summary>Formats and dispatches a log line to console and/or file, subject to the configured output type and severity filter.</summary>
    private Task LogAsync(
        Severity severity,
        string message,
        [CallerMemberName] string caller = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        if (string.IsNullOrWhiteSpace(message)
            || _outputType == OutputType.None
            || !ShouldLog(severity, _filterSeverity))
            return Task.CompletedTask;

        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string context = Path.GetFileNameWithoutExtension(file);
        string prefix = $"{timestamp} [{context}->{caller} L{line}] ";
        string line_ = prefix + message;

        if (_outputType is OutputType.Console or OutputType.All)
            WriteToConsole(severity, prefix, message);

        if (_outputType is OutputType.LogFile or OutputType.All
            && !string.IsNullOrEmpty(_logPath))
            AppendToFile(_logPath, line_ + Environment.NewLine);

        return Task.CompletedTask;
    }


    /// <summary>
    /// Fires on Discord.NET's own Log event (from the command/socket/interaction services).
    /// Writes exceptions and messages to a dated exception-log file, and mirrors
    /// Warning/Error severity into the console as well.
    /// </summary>
    private Task OnDiscordLogAsync(LogMessage log)
    {
        try
        {
            Directory.CreateDirectory(_discordLogDir);

            string path = Path.Combine(_discordLogDir,
                $"ExceptionLog_{DateTime.Now:yyyy_MM_dd}.txt");

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(log.Message))
                sb.AppendLine($"{DateTime.Now:HH:mm:ss} [{log.Severity}] {log.Source}: {log.Message}");

            if (log.Exception is not null)
                sb.AppendLine(log.Exception.ToString());

            if (sb.Length > 0)
                AppendToFile(path, sb.ToString());

            // Also mirror Warning/Error into the console for visibility.
            if (log.Severity is LogSeverity.Warning or LogSeverity.Error)
            {
                var severity = log.Severity is LogSeverity.Error ? Severity.Error : Severity.Warning;
                WriteToConsole(severity, $"{DateTime.Now:HH:mm:ss} [Discord] ", log.Message ?? "");
            }
        }
        catch
        {
            // Swallow to prevent crash loops in the logging path.
        }

        return Task.CompletedTask;
    }


    /// <summary>True if a message at the given severity passes the configured filter.</summary>
    private static bool ShouldLog(Severity severity, FilterSeverity filter) => filter switch
    {
        FilterSeverity.All => true,
        FilterSeverity.NoDebug => severity is not Severity.Debug,
        FilterSeverity.Extended => severity is Severity.Warning or Severity.Error,
        FilterSeverity.Production => severity is Severity.Error,
        FilterSeverity.None => false,
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
    };

    /// <summary>
    /// Writes a coloured log line to stdout. The prefix uses the severity colour;
    /// the message body is always white. Uses a lock to prevent interleaved output
    /// when multiple threads log simultaneously.
    /// </summary>
    private static void WriteToConsole(Severity severity, string prefix, string message)
    {
        lock (_consoleLock)
        {
            var original = Console.ForegroundColor;
            Console.ForegroundColor = (ConsoleColor)severity;
            Console.Write(prefix);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(message);
            Console.ForegroundColor = original;
        }
    }

    /// <summary>
    /// Appends <paramref name="content"/> to <paramref name="path"/> under a lock.
    /// Falls back to a console warning on I/O failure.
    /// </summary>
    private static void AppendToFile(string path, string content)
    {
        lock (_fileLock)
        {
            try { File.AppendAllText(path, content); }
            catch (Exception ex) { Console.WriteLine($"[LoggingService] File write failed: {ex.Message}"); }
        }
    }
}
