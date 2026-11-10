using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AssetMarkOA;

// Logging rule: anything that identifies an account or contains money is logged at Debug only.
public sealed class App {

    private readonly ITransactionJsonSerializer serializer;
    private readonly ITransactionProcessor processor;
    private readonly IAccountSummarizer summarizer;
    private readonly ILogger<App> logger;

    public App(ITransactionJsonSerializer serializer, ITransactionProcessor processor, IAccountSummarizer summarizer, ILogger<App> logger) {
        this.serializer = serializer;
        this.processor = processor;
        this.summarizer = summarizer;
        this.logger = logger;
    }

    // Exit codes: 0 success, 1 invalid input the user can fix, 2 unexpected error.
    // Takes its streams as parameters so tests can run it without the real console.
    public int Run(string[] args, TextReader input, TextWriter output, TextWriter error) {
        var runId = Guid.NewGuid().ToString("N");
        // Every log line written during this run carries the RunId.
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId });
        var stopwatch = Stopwatch.StartNew();

        var exitCode = Execute(args, input, output, error, runId);

        logger.LogInformation("Run finished with exit code {ExitCode} in {ElapsedMs} ms", exitCode, stopwatch.ElapsedMilliseconds);
        return exitCode;
    }

    private int Execute(string[] args, TextReader input, TextWriter output, TextWriter error, string runId) {
        try {
            var fromFile = args.Length > 0;
            logger.LogInformation("Run started, reading input from {InputSource}", fromFile ? "file" : "stdin");
            if (fromFile) {
                // The full path can contain a username.
                logger.LogDebug("Input file: {InputPath}", args[0]);
            }

            var json = fromFile ? File.ReadAllText(args[0]) : input.ReadToEnd();

            var accounts = processor.Process(serializer.Parse(json));

            output.WriteLine(serializer.Serialize(
                summarizer.Summarize(accounts),
                summarizer.FindTopSpender(accounts)));
            return 0;
        } catch (JsonException ex) {
            // The message can repeat part of the input, so only its location is logged by default.
            logger.LogWarning("Invalid JSON at path {JsonPath}, line {LineNumber}, byte {BytePosition}", ex.Path, ex.LineNumber, ex.BytePositionInLine);
            logger.LogDebug("JSON error: {JsonError}", ex.Message);
            error.WriteLine($"Invalid input: {ex.Message}");
            return 1;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) {
            // The message contains the file path, which can contain a username.
            logger.LogWarning("Could not read input: {ExceptionType}", ex.GetType().Name);
            logger.LogDebug("Read error: {ReadError}", ex.Message);
            error.WriteLine($"Could not read input: {ex.Message}");
            return 1;
        } catch (Exception ex) {
            // Type and stack trace only by default: exception messages can contain data (e.g. a dictionary key).
            logger.LogError("Unexpected {ExceptionType}{NewLine}{StackTrace}", ex.GetType().FullName, Environment.NewLine, ex.StackTrace);
            logger.LogDebug(ex, "Unexpected error detail");
            // The user gets no details, only a reference to find the full error in the log.
            error.WriteLine($"An unexpected error occurred. Reference: {runId}.");
            return 2;
        }
    }
}
