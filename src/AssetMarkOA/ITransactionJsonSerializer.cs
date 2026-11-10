namespace AssetMarkOA;

public interface ITransactionJsonSerializer {
    IReadOnlyList<TransactionModel> Parse(string json);
    string Serialize(IReadOnlyList<AccountSummary> summaries, string? topSpender);
}
