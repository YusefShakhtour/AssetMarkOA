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

    private static (int ExitCode, string Output, string Error) Run(
        string[] args, string stdin = "", TestLogSink? sink = null, ITransactionProcessor? processor = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = CreateApp(sink, processor).Run(args, new StringReader(stdin), output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    [Fact]
    public void ValidInputFromStdinWritesSummaryAndReturnsZero()
    {
        var (exitCode, output, error) = Run([], ValidInput);

        Assert.Equal(0, exitCode);
        Assert.Contains("\"topSpender\": null", output);
        Assert.Empty(error);
    }

    [Fact]
    public void ValidInputFromFileReturnsZero()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, ValidInput);

            var (exitCode, output, error) = Run([path]);

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
    [InlineData("""[{ "accountId": "A", "type": "credit", "amount": "abc" }]""")]
    public void InvalidJsonReturnsOne(string stdin)
    {
        var (exitCode, output, error) = Run([], stdin);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Invalid input:", error);
        Assert.Empty(output);
    }

    [Theory]
    [InlineData("does-not-exist.json")]
    [InlineData("")]
    public void UnreadableFileReturnsOne(string path)
    {
        var (exitCode, output, error) = Run([path]);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Could not read input:", error);
        Assert.Empty(output);
    }

    [Fact]
    public void DefaultLogLevelLogsNoAccountIdsOrAmounts()
    {
        var sink = new TestLogSink { MinimumLevel = LogLevel.Information };

        var (exitCode, output, _) = Run([], SensitiveInput, sink);

        Assert.Equal(0, exitCode);
        Assert.Contains("ACCT-SECRET-1", output); // the data was really processed
        Assert.Contains(sink.Entries, e => e.Message.StartsWith("Process finished: 3 valid, 0 skipped, 2 accounts"));
        foreach (var value in SensitiveValues)
        {
            Assert.DoesNotContain(value, sink.AllText);
        }
    }

    [Fact]
    public void DebugLogLevelLogsFullDetail()
    {
        var sink = new TestLogSink { MinimumLevel = LogLevel.Debug };

        Run([], SensitiveInput, sink);

        Assert.Contains("ACCT-SECRET-1", sink.AllText);
        Assert.Contains("98765.43", sink.AllText);
    }

    [Fact]
    public void UnexpectedErrorReturnsTwoWithReferenceAndNoDetails()
    {
        var sink = new TestLogSink { MinimumLevel = LogLevel.Information };

        var (exitCode, output, error) = Run([], ValidInput, sink, new ThrowingProcessor());

        Assert.Equal(2, exitCode);
        Assert.Empty(output);
        Assert.Matches(@"^An unexpected error occurred\. Reference: [0-9a-f]{32}\.$", error.Trim());
        Assert.Contains(sink.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("InvalidOperationException"));
        Assert.DoesNotContain("SECRET", sink.AllText); // the exception message stays out of the default log
    }

    [Fact]
    public void UnexpectedErrorMessageIsLoggedAtDebug()
    {
        var sink = new TestLogSink { MinimumLevel = LogLevel.Debug };

        var (_, _, error) = Run([], ValidInput, sink, new ThrowingProcessor());

        Assert.Contains("ACCT-SECRET-1", sink.AllText);
        Assert.DoesNotContain("SECRET", error); // never shown to the user
    }

    private sealed class ThrowingProcessor : ITransactionProcessor
    {
        public IReadOnlyList<Account> Process(IEnumerable<TransactionModel> transactions) =>
            throw new InvalidOperationException("failure involving ACCT-SECRET-1");
    }
}
