using static AssetMarkOA.Tests.TestData;

namespace AssetMarkOA.Tests;

public class TransactionProcessorTests
{
    [Fact]
    public void EachAccountIdGetsItsOwnAccount()
    {
        var accounts = Processor.Process([
            Tx("B", TransactionType.credit, 50m),
            Tx("A", TransactionType.credit, 100m),
            Tx("A", TransactionType.debit, 30m),
        ]);

        Assert.Equal(2, accounts.Count);
        Assert.Equal(70m, accounts.Single(a => a.AccountId == "A").EndingBalance);
        Assert.Equal(50m, accounts.Single(a => a.AccountId == "B").EndingBalance);
    }

    [Fact]
    public void RepeatedAccountIdDoesNotCreateDuplicate()
    {
        var account = Assert.Single(Processor.Process([
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
    public void InvalidRowsAreSkippedAndCreateNoAccount(string accountId, TransactionType type, int amount)
    {
        Assert.Empty(Processor.Process([Tx(accountId, type, amount)]));
    }

    [Fact]
    public void InvalidRowsAreNotCounted()
    {
        var account = Assert.Single(Processor.Process([
            Tx("A", TransactionType.credit, 10m),
            Tx("A", TransactionType.debit, -500m),
        ]));

        Assert.Equal(1, account.TransactionCount);
        Assert.Equal(0m, account.TotalDebits);
        Assert.Equal(10m, account.LargestTransaction);
    }

    [Fact]
    public void SmallestPositiveAmountIsValid()
    {
        Assert.Single(Processor.Process([Tx("A", TransactionType.credit, 0.01m)]));
    }

    [Fact]
    public void EmptyInputProducesNoAccounts()
    {
        Assert.Empty(Processor.Process([]));
    }

    [Fact]
    public void AccountIdsAreTrimmedAndMatchedCaseInsensitively()
    {
        var account = Assert.Single(Processor.Process([
            Tx(" a100 ", TransactionType.credit, 10m),
            Tx("A100", TransactionType.credit, 20m),
            Tx("a100", TransactionType.debit, 5m),
        ]));

        Assert.Equal("A100", account.AccountId);
        Assert.Equal(3, account.TransactionCount);
        Assert.Equal(25m, account.EndingBalance);
    }

    [Fact]
    public void NormalizedIdsDecideSortOrderAndTopSpenderTie()
    {
        var accounts = Processor.Process([
            Tx("b", TransactionType.debit, 50m),
            Tx(" a", TransactionType.debit, 50m),
        ]);

        Assert.Equal(["A", "B"], Summarizer.Summarize(accounts).Select(s => s.AccountId));
        Assert.Equal("A", Summarizer.FindTopSpender(accounts));
    }

    [Fact]
    public void SkippedRowLogsIndexAndReasonWithoutValues()
    {
        var sink = new TestLogSink();
        var processor = new TransactionProcessor(new TestLogger<TransactionProcessor>(sink));

        processor.Process([
            Tx("SECRET-ID", TransactionType.credit, 10m),
            Tx("SECRET-ID", TransactionType.debit, -987.65m),
        ]);

        var entry = Assert.Single(sink.Entries, e => e.Message.StartsWith("Row"));
        Assert.Equal("Row 1 skipped: amount must be greater than 0", entry.Message);
        Assert.DoesNotContain("SECRET", sink.AllText);
        Assert.DoesNotContain("987", sink.AllText);
    }
}
