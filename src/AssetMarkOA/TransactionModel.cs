namespace AssetMarkOA;

public class TransactionModel {
    public string AccountId {get; set;} = "";
    public TransactionType Type {get; set;}
    public decimal Amount {get; set;}
    // Null when the input's timestamp is missing or invalid, which makes the transaction invalid.
    public DateTime? Timestamp {get; set;}
}
