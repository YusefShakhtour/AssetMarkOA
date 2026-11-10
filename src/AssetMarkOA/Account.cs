namespace AssetMarkOA;

public class Account {
    public string AccountId {get;}
    public decimal TotalCredits {get; private set;}
    public decimal TotalDebits {get; private set;}
    public decimal LargestTransaction {get; private set;}
    public int TransactionCount {get; private set;}
    public decimal EndingBalance => TotalCredits - TotalDebits;
    public bool Overdrawn => EndingBalance < 0;

    public Account(string accountId) {
        AccountId = accountId;
    }

    public void Apply(TransactionModel tx) {
        if (tx.Type == TransactionType.credit) {
            TotalCredits += tx.Amount;
        } else if (tx.Type == TransactionType.debit) {
            TotalDebits += tx.Amount;
        }
        if (tx.Amount > LargestTransaction) {
            LargestTransaction = tx.Amount;
        }
        TransactionCount++;
    }
}
