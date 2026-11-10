using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssetMarkOA.Tests;

internal static class TestData
{
    public static readonly TransactionJsonSerializer Serializer = new(NullLogger<TransactionJsonSerializer>.Instance);
    public static readonly TransactionProcessor Processor = new(NullLogger<TransactionProcessor>.Instance);
    public static readonly AccountSummarizer Summarizer = new(NullLogger<AccountSummarizer>.Instance);

    // The real App wiring; pass a sink to capture logs, or a processor to replace the real one.
    public static App CreateApp(TestLogSink? sink = null, ITransactionProcessor? processor = null) => new(
        new TransactionJsonSerializer(Logger<TransactionJsonSerializer>(sink)),
        processor ?? new TransactionProcessor(Logger<TransactionProcessor>(sink)),
        new AccountSummarizer(Logger<AccountSummarizer>(sink)),
        Logger<App>(sink));

    private static ILogger<T> Logger<T>(TestLogSink? sink) =>
        sink is null ? NullLogger<T>.Instance : new TestLogger<T>(sink);

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
