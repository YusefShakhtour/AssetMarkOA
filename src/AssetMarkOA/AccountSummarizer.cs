using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AssetMarkOA;

// Logging rule: anything that identifies an account or contains money is logged at Debug only.
public sealed class AccountSummarizer : IAccountSummarizer {

    private readonly ILogger<AccountSummarizer> logger;

    public AccountSummarizer(ILogger<AccountSummarizer> logger) {
        this.logger = logger;
    }

    public IReadOnlyList<AccountSummary> Summarize(IEnumerable<Account> accounts) {
        logger.LogInformation("Summarize started");
        var stopwatch = Stopwatch.StartNew();

        var summaries = accounts
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

        foreach (var summary in summaries) {
            logger.LogDebug("Summary: {Summary}", summary);
        }
        logger.LogInformation("Summarize finished: {SummaryCount} summaries in {ElapsedMs} ms", summaries.Count, stopwatch.ElapsedMilliseconds);
        return summaries;
    }

    public string? FindTopSpender(IEnumerable<Account> accounts) {
        logger.LogInformation("FindTopSpender started");

        var topSpender = accounts
            .Where(a => a.TotalDebits > 0)
            .OrderByDescending(a => a.TotalDebits)
            .ThenBy(a => a.AccountId, StringComparer.Ordinal)
            .FirstOrDefault()?.AccountId;

        logger.LogDebug("Top spender: {TopSpender}", topSpender);
        logger.LogInformation("FindTopSpender finished: top spender found = {Found}", topSpender is not null);
        return topSpender;
    }
}
