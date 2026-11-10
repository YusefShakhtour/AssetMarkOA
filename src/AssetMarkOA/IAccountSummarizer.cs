namespace AssetMarkOA;

public interface IAccountSummarizer {
    IReadOnlyList<AccountSummary> Summarize(IEnumerable<Account> accounts);
    string? FindTopSpender(IEnumerable<Account> accounts);
}
