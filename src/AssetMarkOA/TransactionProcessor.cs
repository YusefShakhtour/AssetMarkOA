using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AssetMarkOA;

// Logging rule: anything that identifies an account or contains money is logged at Debug only.
public sealed class TransactionProcessor : ITransactionProcessor {

    private readonly ILogger<TransactionProcessor> logger;

    public TransactionProcessor(ILogger<TransactionProcessor> logger) {
        this.logger = logger;
    }

    public async Task<IReadOnlyList<Account>> ProcessAsync(IAsyncEnumerable<TransactionModel> transactions, CancellationToken cancellationToken = default) {
        logger.LogInformation("Process started");
        var stopwatch = Stopwatch.StartNew();

        var accounts = new Dictionary<string, Account>();
        var rowIndex = 0;
        var validCount = 0;
        var skippedCount = 0;

        await foreach (var tx in transactions.WithCancellation(cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            var accountId = NormalizeAccountId(tx.AccountId);
            var validationError = GetValidationError(accountId, tx);

            if (validationError is not null) {
                skippedCount++;
                // Row index is 0-based, matching $[index] in the input JSON. The reason never contains values.
                logger.LogInformation("Row {RowIndex} skipped: {Reason}", rowIndex, validationError);
            } else {
                if (!accounts.ContainsKey(accountId)) {
                    accounts[accountId] = new Account(accountId);
                }
                accounts[accountId].Apply(tx);
                validCount++;
                logger.LogDebug("Row {RowIndex} applied: account {AccountId}, {Type} {Amount}", rowIndex, accountId, tx.Type.ToString(), tx.Amount);
            }
            rowIndex++;
        }

        logger.LogInformation(
            // Parsing is streamed into this loop, so the elapsed time includes it.
            "Process finished: {ValidCount} valid, {SkippedCount} skipped, {AccountCount} accounts in {ElapsedMs} ms (including parsing)",
            validCount, skippedCount, accounts.Count, stopwatch.ElapsedMilliseconds);
        return accounts.Values.ToList();
    }

    // " a100 " and "A100" are the same account; the output shows the uppercase form.
    private static string NormalizeAccountId(string accountId) => accountId.Trim().ToUpperInvariant();

    private static string? GetValidationError(string normalizedAccountId, TransactionModel tx) {
        if (normalizedAccountId.Length == 0) {
            return "account ID is missing, empty or not a string";
        }
        if (!Enum.IsDefined(tx.Type)) {
            return "type is missing or not credit/debit";
        }
        if (tx.Amount <= 0) {
            return "amount is missing, not a number, or not greater than 0";
        }        return null;
    }
}
