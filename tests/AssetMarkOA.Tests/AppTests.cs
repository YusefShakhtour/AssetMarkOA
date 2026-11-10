using Microsoft.Extensions.Logging;
using static AssetMarkOA.Tests.TestData;

namespace AssetMarkOA.Tests;

public class AppTests
{
    private const string ValidInput = """[{ "accountId": "A", "type": "credit", "amount": 10 }]""";

    // Distinctive IDs and amounts, so a leak into the logs can't be a coincidence.
    // Balance for ACCT-SECRET-1 is 98765.43 - 1234.56 = 97530.87.
    private const string SensitiveInput = """
        [
          { "accountId": "acct-secret-1", "type": "credit", "amount": 98765.43 },
          { "accountId": "acct-secret-1", "type": "debit", "amount": 1234.56 },
          { "accountId": "acct-secret-2", "type": "debit", "amount": 4321.09 }
        ]
        """;

    private static readonly string[] SensitiveValues = ["SECRET", "secret", "98765", "1234.56", "4321", "97530"];

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string[] args, string stdin = "", TestLogSink? sink = null, ITransactionProcessor? processor = null,
        CancellationToken cancellationToken = default)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await CreateApp(sink, processor).RunAsync(args, Utf8(stdin), output, error, cancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task ValidInputFromStdinWritesSummaryAndReturnsZero()
    {
        var (exitCode, output, error) = await RunAsync([], ValidInput);

        Assert.Equal(0, exitCode);
        Assert.Contains("\"topSpender\": null", output);
        Assert.Empty(error);
    }

    [Fact]
    public async Task ValidInputFromFileReturnsZero()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, ValidInput);

            var (exitCode, output, error) = await RunAsync([path]);

            Assert.Equal(0, exitCode);
            Assert.Contains("\"accountId\": \"A\"", output);
            Assert.Empty(error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("""[{ "accountId": "A", "type": "credit", "amount": "abc" }]""")]
    public async Task InvalidJsonReturnsOne(string stdin)
    {
        var (exitCode, output, error) = await RunAsync([], stdin);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Invalid input at line 1, byte ", error);
        Assert.DoesNotContain("Path:", error); // the JSON path's row index can be wrong when streaming
        Assert.Empty(output);
    }

    [Fact]
    public async Task InvalidJsonReportsLineAndByteCountingFromOne()
    {
        var (_, _, error) = await RunAsync([], "[\n  { \"accountId\": \"A\", oops }\n]");

        Assert.StartsWith("Invalid input at line 2, byte 23: ", error);
    }

    [Fact]
    public async Task BrokenJsonAfterValidRowsReturnsOneWithNoOutput()
    {
        var sink = new TestLogSink { MinimumLevel = LogLevel.Debug };
        // The parser reads in chunks of about 16 KB, so the valid rows must span several chunks
        // for some of them to be applied before it reaches the broken part.
        var validRows = string.Join(",", Enumerable.Repeat("""{ "accountId": "A", "type": "credit", "amount": 10 }""", 2_000));

        var input = $$"""[{{validRows}}, { "accountId": "B", oops""";
        var expectedLocation = $"line 1, byte {input.IndexOf("oops") + 1}";

        var (exitCode, output, error) = await RunAsync([], input, sink);

        Assert.Equal(1, exitCode);
        Assert.Empty(output); // nothing is written until every row is processed
        // Streaming: rows were applied before the parser reached the broken part.
        Assert.Contains(sink.Entries, e => e.Message.StartsWith("Row 0 applied"));
        // The location is exact even though the error is many chunks into the input.
        Assert.StartsWith($"Invalid input at {expectedLocation}: ", error);
        Assert.Contains(sink.Entries, e => e.Level == LogLevel.Warning && e.Message == $"Invalid JSON at {expectedLocation}");
    }

    [Theory]
    [InlineData("does-not-exist.json")]
    [InlineData("")]
    public async Task UnreadableFileReturnsOne(string path)
    {
        var (exitCode, output, error) = await RunAsync([path]);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Could not read input:", error);
        Assert.Empty(output);
    }

    [Fact]
    public async Task CancelledRunReturns130WithNoOutput()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var (exitCode, output, error) = await RunAsync([], ValidInput, cancellationToken: cancellation.Token);

        Assert.Equal(130, exitCode);
        Assert.Equal("Cancelled.", error.Trim());
        Assert.Empty(output);
    }

    [Fact]
    public async Task DefaultLogLevelLogsNoAccountIdsOrAmounts()
    {
        var sink = new TestLogSink { MinimumLevel = LogLevel.Information };

        var (exitCode, output, _) = await RunAsync([], SensitiveInput, sink);

        Assert.Equal(0, exitCode);
        Assert.Contains("ACCT-SECRET-1", output); // the data was really processed
        Assert.Contains(sink.Entries, e => e.Message.StartsWith("Process finished: 3 valid, 0 skipped, 2 accounts"));
        foreach (var value in SensitiveValues)
        {
            Assert.DoesNotContain(value, sink.AllText);
        }
    }

    [Fact]
    public async Task DebugLogLevelLogsFullDetail()
    {
        var sink = new TestLogSink { MinimumLevel = LogLevel.Debug };

        await RunAsync([], SensitiveInput, sink);

        Assert.Contains("ACCT-SECRET-1", sink.AllText);
        Assert.Contains("98765.43", sink.AllText);
    }

    [Fact]
    public async Task UnexpectedErrorReturnsTwoWithReferenceAndNoDetails()
    {
        var sink = new TestLogSink { MinimumLevel = LogLevel.Information };

        var (exitCode, output, error) = await RunAsync([], ValidInput, sink, new ThrowingProcessor());

        Assert.Equal(2, exitCode);
        Assert.Empty(output);
        Assert.Matches(@"^An unexpected error occurred\. Reference: [0-9a-f]{32}\.$", error.Trim());
        Assert.Contains(sink.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("InvalidOperationException"));
        Assert.DoesNotContain("SECRET", sink.AllText); // the exception message stays out of the default log
    }

    [Fact]
    public async Task UnexpectedErrorMessageIsLoggedAtDebug()
    {
        var sink = new TestLogSink { MinimumLevel = LogLevel.Debug };

        var (_, _, error) = await RunAsync([], ValidInput, sink, new ThrowingProcessor());

        Assert.Contains("ACCT-SECRET-1", sink.AllText);
        Assert.DoesNotContain("SECRET", error); // never shown to the user
    }

    private sealed class ThrowingProcessor : ITransactionProcessor
    {
        public Task<IReadOnlyList<Account>> ProcessAsync(IAsyncEnumerable<TransactionModel> transactions, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("failure involving ACCT-SECRET-1");
    }
}
