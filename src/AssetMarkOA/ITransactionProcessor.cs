namespace AssetMarkOA;

public interface ITransactionProcessor {
    IReadOnlyList<Account> Process(IEnumerable<TransactionModel> transactions);
}
