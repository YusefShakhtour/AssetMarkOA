namespace AssetMarkOA;

public sealed record AccountSummary(
    string AccountId,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal EndingBalance,
    decimal LargestTransaction,
    int TransactionCount,
    bool Overdrawn);
