namespace AssetMarkOA;

public sealed class TransactionProcessor : ITransactionProcessor {

    public IReadOnlyList<Account> Process(IEnumerable<TransactionModel> transactions) {
        var accounts = new Dictionary<string, Account>();
        foreach (var tx in transactions) {
            var accountId = NormalizeAccountId(tx.AccountId);
            if (!IsValid(accountId, tx)) {
                continue;
            }
            if (!accounts.ContainsKey(accountId)) {
                accounts[accountId] = new Account(accountId);
            }
            accounts[accountId].Apply(tx);
        }
        return accounts.Values.ToList();
    }

    // " a100 " and "A100" are the same account; the output shows the uppercase form.
    private static string NormalizeAccountId(string accountId) => accountId.Trim().ToUpperInvariant();

    private static bool IsValid(string normalizedAccountId, TransactionModel tx) {
        return normalizedAccountId.Length > 0 && Enum.IsDefined(tx.Type) && tx.Amount > 0;
    }
}
