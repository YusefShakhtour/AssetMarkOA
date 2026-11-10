using static AssetMarkOA.Tests.TestData;

namespace AssetMarkOA.Tests;

public class AccountTests
{
    [Fact]
    public void NewAccountStartsEmpty()
    {
        var account = new Account("A");

        Assert.Equal("A", account.AccountId);
        Assert.Equal(0m, account.TotalCredits);
        Assert.Equal(0m, account.TotalDebits);
        Assert.Equal(0m, account.EndingBalance);
        Assert.Equal(0m, account.LargestTransaction);
        Assert.Equal(0, account.TransactionCount);
        Assert.False(account.Overdrawn);
    }

    [Fact]
    public void ApplyUpdatesTotalsCountAndLargest()
    {
        var account = new Account("A");

        account.Apply(Tx("A", TransactionType.credit, 100m));
        account.Apply(Tx("A", TransactionType.debit, 150m));
        account.Apply(Tx("A", TransactionType.credit, 20m));

        Assert.Equal(120m, account.TotalCredits);
        Assert.Equal(150m, account.TotalDebits);
        Assert.Equal(-30m, account.EndingBalance);
        Assert.Equal(150m, account.LargestTransaction); // largest counts debits too
        Assert.Equal(3, account.TransactionCount);
    }

    [Fact]
    public void AccountIsOverdrawnWhenFinalBalanceIsNegative()
    {
        var account = AccountWith("A", (TransactionType.credit, 50m), (TransactionType.debit, 80m));

        Assert.Equal(-30m, account.EndingBalance);
        Assert.True(account.Overdrawn);
    }

    [Fact]
    public void ZeroBalanceIsNotOverdrawn()
    {
        Assert.False(AccountWith("A", (TransactionType.credit, 50m), (TransactionType.debit, 50m)).Overdrawn);
    }

    [Fact]
    public void OneCentBelowZeroIsOverdrawn()
    {
        var account = AccountWith("A", (TransactionType.credit, 50.00m), (TransactionType.debit, 50.01m));

        Assert.Equal(-0.01m, account.EndingBalance);
        Assert.True(account.Overdrawn);
    }

    [Fact]
    public void FractionalAmountsSumExactly()
    {
        // 0.1 + 0.2 is 0.30000000000000004 with double; decimal must give exactly 0.3.
        var account = AccountWith("A",
            (TransactionType.credit, 0.1m),
            (TransactionType.credit, 0.2m),
            (TransactionType.debit, 0.3m));

        Assert.Equal(0.3m, account.TotalCredits);
        Assert.Equal(0m, account.EndingBalance);
    }

    [Fact]
    public void ManyCentsAddUpExactly()
    {
        var account = new Account("A");
        for (var i = 0; i < 1000; i++)
        {
            account.Apply(Tx("A", TransactionType.credit, 0.01m));
        }

        Assert.Equal(10m, account.TotalCredits);
    }

    [Fact]
    public void LargestTransactionComparesFractionalAmounts()
    {
        var account = AccountWith("A",
            (TransactionType.credit, 99.99m),
            (TransactionType.debit, 100.01m),
            (TransactionType.credit, 100.00m));

        Assert.Equal(100.01m, account.LargestTransaction);
    }

    [Theory]
    [InlineData(TransactionType.credit, 0)]
    [InlineData(TransactionType.debit, -5)]
    [InlineData((TransactionType)0, 10)]
    public void ApplyRejectsAnInvalidTransactionWithoutChangingTheAccount(TransactionType type, int amount)
    {
        var account = AccountWith("A", (TransactionType.credit, 100m), (TransactionType.debit, 30m));

        Assert.Throws<InvalidOperationException>(() => account.Apply(Tx("A", type, amount)));

        Assert.Equal(100m, account.TotalCredits);
        Assert.Equal(30m, account.TotalDebits);
        Assert.Equal(100m, account.LargestTransaction);
        Assert.Equal(2, account.TransactionCount);
    }
}
