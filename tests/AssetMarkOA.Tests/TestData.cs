using System.Text;
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

    // A valid timestamp by default, since a transaction without one is invalid.
    public static TransactionModel Tx(string accountId, TransactionType type, decimal amount) =>
        new() { AccountId = accountId, Type = type, Amount = amount, Timestamp = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc) };

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

    // Runs the shared processor over in-memory transactions.
    public static Task<IReadOnlyList<Account>> ProcessAsync(TransactionModel[] transactions) =>
        Processor.ProcessAsync(AsyncOf(transactions));

    public static MemoryStream Utf8(string json) => new(Encoding.UTF8.GetBytes(json));

    // .NET 8 has no built-in way to turn an array into an IAsyncEnumerable, or back into a list.
    public static async IAsyncEnumerable<TransactionModel> AsyncOf(params TransactionModel[] transactions)
    {
        foreach (var tx in transactions)
        {
            yield return tx;
        }
        await Task.CompletedTask;
    }

    public static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }
}
