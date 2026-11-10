namespace AssetMarkOA;

public sealed class AccountSummarizer : IAccountSummarizer {

    public IReadOnlyList<AccountSummary> Summarize(IEnumerable<Account> accounts) {
        return accounts
            .OrderBy(a => a.AccountId, StringComparer.Ordinal)
            .Select(a => new AccountSummary(
                a.AccountId,
                a.TotalCredits,
                a.TotalDebits,
                a.EndingBalance,
                a.LargestTransaction,
                a.TransactionCount,
                a.Overdrawn))
            .ToList();
    }

    public string? FindTopSpender(IEnumerable<Account> accounts) {
        return accounts
            .Where(a => a.TotalDebits > 0)
            .OrderByDescending(a => a.TotalDebits)
            .ThenBy(a => a.AccountId, StringComparer.Ordinal)
            .FirstOrDefault()?.AccountId;
    }
}
