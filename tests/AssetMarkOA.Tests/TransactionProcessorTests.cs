using static AssetMarkOA.Tests.TestData;

namespace AssetMarkOA.Tests;

public class TransactionProcessorTests
{
    [Fact]
    public void EachAccountIdGetsItsOwnAccount()
    {
        var accounts = TransactionProcessor.Process([
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
        var account = Assert.Single(TransactionProcessor.Process([
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
        Assert.Empty(TransactionProcessor.Process([Tx(accountId, type, amount)]));
    }

    [Fact]
    public void InvalidRowsAreNotCounted()
    {
        var account = Assert.Single(TransactionProcessor.Process([
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
        Assert.Single(TransactionProcessor.Process([Tx("A", TransactionType.credit, 0.01m)]));
    }

    [Fact]
    public void EmptyInputProducesNoAccounts()
    {
        Assert.Empty(TransactionProcessor.Process([]));
    }

    [Fact]
    public void AccountIdsAreTrimmedAndMatchedCaseInsensitively()
    {
        var account = Assert.Single(TransactionProcessor.Process([
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
        var accounts = TransactionProcessor.Process([
            Tx("b", TransactionType.debit, 50m),
            Tx(" a", TransactionType.debit, 50m),
        ]);

        Assert.Equal(["A", "B"], AccountSummarizer.Summarize(accounts).Select(s => s.AccountId));
        Assert.Equal("A", AccountSummarizer.FindTopSpender(accounts));
    }
}
