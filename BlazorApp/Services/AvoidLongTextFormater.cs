using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

public class AvoidLongTextFormatter : ConsoleFormatter
{
    private const int MaxMessageLength = 10000;

    public AvoidLongTextFormatter() : base("AvoidLongTextFormatter")
    {
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter? textWriter)
    {
        try
        {
            var originalMessage = logEntry.Formatter(logEntry.State, logEntry.Exception);
            var truncatedMessage = originalMessage.Length > MaxMessageLength
                ? originalMessage.Substring(0, MaxMessageLength) + "..."
                : originalMessage;

            textWriter?.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logEntry.LogLevel}] {truncatedMessage}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.Error.WriteLine("Last exception line should not have crash the application.");
        }
    }
}