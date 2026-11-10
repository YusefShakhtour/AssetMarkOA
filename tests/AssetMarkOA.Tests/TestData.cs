namespace AssetMarkOA.Tests;

internal static class TestData
{
    public static TransactionModel Tx(string accountId, TransactionType type, decimal amount) =>
        new() { AccountId = accountId, Type = type, Amount = amount };

    // Builds an account directly, without the processor, so summarizer tests control the input exactly.
    public static Account AccountWith(string accountId, params (TransactionType Type, decimal Amount)[] transactions)
    {
        var account = new Account(accountId);
        foreach (var (type, amount) in transactions)
        {
            account.Apply(Tx(accountId, type, amount));
        }
        return account;
    }
}
