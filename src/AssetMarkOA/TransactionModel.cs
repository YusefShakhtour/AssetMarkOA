namespace AssetMarkOA;

public class TransactionModel {
    public string AccountId {get; set;} = "";
    public TransactionType Type {get; set;}
    public decimal Amount {get; set;}
    public DateTime Timestamp {get; set;}
}
