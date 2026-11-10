namespace AssetMarkOA;

public interface ITransactionProcessor {
    Task<IReadOnlyList<Account>> ProcessAsync(IAsyncEnumerable<TransactionModel> transactions, CancellationToken cancellationToken = default);
}
