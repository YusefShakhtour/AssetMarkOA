using Microsoft.Extensions.Logging;
using static AssetMarkOA.Tests.TestData;

namespace AssetMarkOA.Tests;

public class AppTests
{
    private const string ValidInput = """[{ "accountId": "A", "type": "credit", "amount": 10, "timestamp": "2026-05-10T10:00:00Z" }]""";

    // Distinctive IDs and amounts, so a leak into the logs can't be a coincidence.
    // Balance for ACCT-SECRET-1 is 98765.43 - 1234.56 = 97530.87.
    private const string SensitiveInput = """
        [
          { "accountId": "acct-secret-1", "type": "credit", "amount": 98765.43, "timestamp": "2026-05-10T10:00:00Z" },
          { "accountId": "acct-secret-1", "type": "debit", "amount": 1234.56, "timestamp": "2026-05-10T11:00:00Z" },
          { "accountId": "acct-secret-2", "type": "debit", "amount": 4321.09, "timestamp": "2026-05-10T12:00:00Z" }
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
        Assert.Contains("\"accountId\": \"A\"", output);
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
    public async Task InvalidJsonReturnsOne(string stdin)
    {
        var (exitCode, output, error) = await RunAsync([], stdin);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Invalid input at line 1, byte ", error);
        Assert.DoesNotContain("Path:", error); // the JSON path's row index can be wrong when streaming
        Assert.Empty(output);
    }

    [Fact]
    public async Task BadRowsAreSkippedAndGoodRowsStillSummarized()
    {
        var sink = new TestLogSink();

        var (exitCode, output, error) = await RunAsync([], """
            [
              { "accountId": "A", "type": "credit", "amount": 10, "timestamp": "2026-05-10T10:00:00Z" },
              { "accountId": "A", "type": "credit", "amount": "abc", "timestamp": "2026-05-10T10:00:00Z" },
              { "accountId": 123, "type": "debit", "amount": 5, "timestamp": "2026-05-10T10:00:00Z" },
              1,
              { "accountId": "B", "type": "debit", "amount": 5, "timestamp": "abc" },
              { "accountId": "C", "type": "debit", "amount": 5, "timestamp": "2026-05-10T10:00:00Z" }
            ]
            """, sink);

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        Assert.Contains("\"accountId\": \"A\"", output);
        Assert.DoesNotContain("\"accountId\": \"B\"", output); // a bad timestamp makes the row invalid
        Assert.Contains("\"accountId\": \"C\"", output);
        Assert.Equal(
            [
                "Row 1 skipped: amount is missing, not a number, or not greater than 0",
                "Row 2 skipped: account ID is missing, empty or not a string",
                "Row 3 skipped: account ID is missing, empty or not a string",
                "Row 4 skipped: timestamp is missing or not a valid ISO 8601 date",
            ],
            sink.Entries.Where(e => e.Message.StartsWith("Row")).Select(e => e.Message));
    }

    [Fact]
    public async Task TotalsTooLargeForDecimalReturnOneWithAClearMessage()
    {
        var sink = new TestLogSink();
        // Each amount fits in a decimal; their sum doesn't.
        const string hugeCredit = """{ "accountId": "A", "type": "credit", "amount": 79000000000000000000000000000, "timestamp": "2026-05-10T10:00:00Z" }""";

        var (exitCode, output, error) = await RunAsync([], $"[{hugeCredit}, {hugeCredit}]", sink);

        Assert.Equal(1, exitCode);
        Assert.Empty(output);
        Assert.Equal("Invalid input: an account's totals exceed the largest amount this program supports.", error.Trim());
        Assert.Contains(sink.Entries, e => e.Level == LogLevel.Warning && e.Message == "Account totals exceeded the supported range");
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
        var validRows = string.Join(",", Enumerable.Repeat("""{ "accountId": "A", "type": "credit", "amount": 10, "timestamp": "2026-05-10T10:00:00Z" }""", 2_000));

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

    [Fact]
    public async Task FolderInsteadOfFileReturnsOne()
    {
        var (exitCode, output, error) = await RunAsync([Path.GetTempPath()]);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Could not read input:", error);
        Assert.Empty(output);
    }

    [Fact]
    public async Task ArgumentExceptionOutsideOpeningTheFileIsAnUnexpectedError()
    {
        var (exitCode, _, error) = await RunAsync([], ValidInput, processor: new ThrowingProcessor(new ArgumentException("bug")));

        Assert.Equal(2, exitCode);
        Assert.StartsWith("An unexpected error occurred. Reference: ", error);
    }

    [Fact]
    public async Task OverflowFromAnywhereElseIsAnUnexpectedError()
    {
        var (exitCode, _, error) = await RunAsync([], ValidInput, processor: new ThrowingProcessor(new OverflowException()));

        Assert.Equal(2, exitCode);
        Assert.StartsWith("An unexpected error occurred. Reference: ", error);
    }

    private sealed class ThrowingProcessor(Exception? exception = null) : ITransactionProcessor
    {
        public Task<IReadOnlyList<Account>> ProcessAsync(IAsyncEnumerable<TransactionModel> transactions, CancellationToken cancellationToken = default) =>
            throw exception ?? new InvalidOperationException("failure involving ACCT-SECRET-1");
    }
}
