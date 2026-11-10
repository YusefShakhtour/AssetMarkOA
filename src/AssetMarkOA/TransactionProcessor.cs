namespace AssetMarkOA;

public static class TransactionProcessor {

    public static IReadOnlyList<Account> Process(IEnumerable<TransactionModel> transactions) {
        var accounts = new Dictionary<string, Account>();
        foreach (var tx in transactions.Where(IsValid)) {
            if (!accounts.ContainsKey(tx.AccountId)) {
                accounts[tx.AccountId] = new Account(tx.AccountId);
            }
            accounts[tx.AccountId].Apply(tx);
        }
        return accounts.Values.ToList();
    }

    private static bool IsValid(TransactionModel tx) {
        return !string.IsNullOrEmpty(tx.AccountId) && Enum.IsDefined(tx.Type) && tx.Amount > 0;
    }
}
