using static AssetMarkOA.Tests.TestData;

namespace AssetMarkOA.Tests;

// Accounts are built directly with AccountWith, not through the processor, so these test the summarizer alone.
public class AccountSummarizerTests
{
    [Fact]
    public void SummarizeCopiesEveryAccountField()
    {
        var account = AccountWith("A", (TransactionType.credit, 100m), (TransactionType.debit, 130m));

        var summary = Assert.Single(AccountSummarizer.Summarize([account]));

        Assert.Equal(new AccountSummary("A", 100m, 130m, -30m, 130m, 2, true), summary);
    }

    [Fact]
    public void SummarizeSortsByAccountIdOrdinal()
    {
        var summaries = AccountSummarizer.Summarize([new Account("b"), new Account("B"), new Account("A")]);

        Assert.Equal(["A", "B", "b"], summaries.Select(s => s.AccountId));
    }

    [Fact]
    public void SummarizeWithNoAccountsIsEmpty()
    {
        Assert.Empty(AccountSummarizer.Summarize([]));
    }

    [Fact]
    public void TopSpenderHasHighestTotalDebits()
    {
        var topSpender = AccountSummarizer.FindTopSpender([
            AccountWith("A", (TransactionType.debit, 60m)),
            AccountWith("B", (TransactionType.debit, 40m), (TransactionType.debit, 40m)),
        ]);

        Assert.Equal("B", topSpender);
    }

    [Fact]
    public void TopSpenderIgnoresCredits()
    {
        var topSpender = AccountSummarizer.FindTopSpender([
            AccountWith("A", (TransactionType.credit, 1000m), (TransactionType.debit, 10m)),
            AccountWith("B", (TransactionType.debit, 20m)),
        ]);

        Assert.Equal("B", topSpender);
    }

    [Fact]
    public void TopSpenderTieReturnsLexicographicallySmallerId()
    {
        var topSpender = AccountSummarizer.FindTopSpender([
            AccountWith("B", (TransactionType.debit, 50m)),
            AccountWith("A", (TransactionType.debit, 50m)),
        ]);

        Assert.Equal("A", topSpender);
    }

    [Fact]
    public void TopSpenderDecidedByOneCent()
    {
        var topSpender = AccountSummarizer.FindTopSpender([
            AccountWith("A", (TransactionType.debit, 100.00m)),
            AccountWith("B", (TransactionType.debit, 100.01m)),
        ]);

        Assert.Equal("B", topSpender);
    }

    [Fact]
    public void TopSpenderIsNullWithNoDebits()
    {
        Assert.Null(AccountSummarizer.FindTopSpender([AccountWith("A", (TransactionType.credit, 50m))]));
    }

    [Fact]
    public void TopSpenderIsNullWithNoAccounts()
    {
        Assert.Null(AccountSummarizer.FindTopSpender([]));
    }
}
