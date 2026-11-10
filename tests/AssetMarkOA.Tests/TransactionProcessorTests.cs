using static AssetMarkOA.Tests.TestData;

namespace AssetMarkOA.Tests;

public class TransactionProcessorTests
{
    [Fact]
    public async Task EachAccountIdGetsItsOwnAccount()
    {
        var accounts = await ProcessAsync([
            Tx("B", TransactionType.credit, 50m),
            Tx("A", TransactionType.credit, 100m),
            Tx("A", TransactionType.debit, 30m),
        ]);

        Assert.Equal(2, accounts.Count);
        Assert.Equal(70m, accounts.Single(a => a.AccountId == "A").EndingBalance);
        Assert.Equal(50m, accounts.Single(a => a.AccountId == "B").EndingBalance);
    }

    [Fact]
    public async Task RepeatedAccountIdDoesNotCreateDuplicate()
    {
        var account = Assert.Single(await ProcessAsync([
            Tx("A", TransactionType.credit, 10m),
            Tx("A", TransactionType.credit, 20m),
        ]));

        Assert.Equal(30m, account.TotalCredits);
    }

    [Theory]
    [InlineData("", TransactionType.credit, 10)]
    [InlineData("   ", TransactionType.credit, 10)]
    [InlineData("A", TransactionType.credit, 0)]
    [InlineData("A", TransactionType.debit, -10)]
    [InlineData("A", (TransactionType)0, 10)]
    [InlineData("A", (TransactionType)99, 10)]
    public async Task InvalidRowsAreSkippedAndCreateNoAccount(string accountId, TransactionType type, int amount)
    {
        Assert.Empty(await ProcessAsync([Tx(accountId, type, amount)]));
    }

    [Fact]
    public async Task InvalidRowsAreNotCounted()
    {
        var account = Assert.Single(await ProcessAsync([
            Tx("A", TransactionType.credit, 10m),
            Tx("A", TransactionType.debit, -500m),
        ]));

        Assert.Equal(1, account.TransactionCount);
        Assert.Equal(0m, account.TotalDebits);
        Assert.Equal(10m, account.LargestTransaction);
    }

    [Fact]
    public async Task SmallestPositiveAmountIsValid()
    {
        Assert.Single(await ProcessAsync([Tx("A", TransactionType.credit, 0.01m)]));
    }

    [Fact]
    public async Task EmptyInputProducesNoAccounts()
    {
        Assert.Empty(await ProcessAsync([]));
    }

    [Fact]
    public async Task AccountIdsAreTrimmedAndMatchedCaseInsensitively()
    {
        var account = Assert.Single(await ProcessAsync([
            Tx(" a100 ", TransactionType.credit, 10m),
            Tx("A100", TransactionType.credit, 20m),
            Tx("a100", TransactionType.debit, 5m),
        ]));

        Assert.Equal("A100", account.AccountId);
        Assert.Equal(3, account.TransactionCount);
        Assert.Equal(25m, account.EndingBalance);
    }

    [Fact]
    public async Task NormalizedIdsDecideSortOrderAndTopSpenderTie()
    {
        var accounts = await ProcessAsync([
            Tx("b", TransactionType.debit, 50m),
            Tx(" a", TransactionType.debit, 50m),
        ]);

        Assert.Equal(["A", "B"], Summarizer.Summarize(accounts).Select(s => s.AccountId));
        Assert.Equal("A", Summarizer.FindTopSpender(accounts));
    }

    [Fact]
    public async Task SkippedRowLogsIndexAndReasonWithoutValues()
    {
        var sink = new TestLogSink();
        var processor = new TransactionProcessor(new TestLogger<TransactionProcessor>(sink));

        await processor.ProcessAsync(AsyncOf(
            Tx("SECRET-ID", TransactionType.credit, 10m),
            Tx("SECRET-ID", TransactionType.debit, -987.65m)));

        var entry = Assert.Single(sink.Entries, e => e.Message.StartsWith("Row"));
        Assert.Equal("Row 1 skipped: amount must be greater than 0", entry.Message);
        Assert.DoesNotContain("SECRET", sink.AllText);
        Assert.DoesNotContain("987", sink.AllText);
    }

    [Fact]
    public async Task CancellationStopsProcessing()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Processor.ProcessAsync(AsyncOf(Tx("A", TransactionType.credit, 10m)), cancellation.Token));
    }
}
