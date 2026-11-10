using static AssetMarkOA.Tests.TestData;

namespace AssetMarkOA.Tests;

public class AppTests
{
    private const string ValidInput = """[{ "accountId": "A", "type": "credit", "amount": 10 }]""";

    private static (int ExitCode, string Output, string Error) Run(
        string[] args, string stdin = "", ITransactionProcessor? processor = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = CreateApp(processor).Run(args, new StringReader(stdin), output, error);
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
    public void UnexpectedErrorReturnsTwoWithoutDetails()
    {
        var (exitCode, output, error) = Run([], ValidInput, new ThrowingProcessor());

        Assert.Equal(2, exitCode);
        Assert.Equal("An unexpected error occurred.", error.Trim());
        Assert.Empty(output);
    }

    private sealed class ThrowingProcessor : ITransactionProcessor
    {
        public IReadOnlyList<Account> Process(IEnumerable<TransactionModel> transactions) =>
            throw new InvalidOperationException("failure involving ACCT-SECRET-1");
    }
}
