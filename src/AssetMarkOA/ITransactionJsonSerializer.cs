namespace AssetMarkOA;

public interface ITransactionJsonSerializer {
    // Streams transactions one at a time as the input is read, instead of loading the whole input first.
    IAsyncEnumerable<TransactionModel> ParseAsync(Stream input, CancellationToken cancellationToken = default);
    string Serialize(IReadOnlyList<AccountSummary> summaries, string? topSpender);
}
