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

    // The processor validates input before calling this. These guards catch a programming error that would
    // otherwise leave the totals inconsistent. InvalidOperationException, not ArgumentException, so App reports
    // it as an unexpected error rather than as unreadable input.
    public void Apply(TransactionModel tx) {
        if (!Enum.IsDefined(tx.Type)) {
            throw new InvalidOperationException("Cannot apply a transaction with an undefined type.");
        }
        if (tx.Amount <= 0) {
            throw new InvalidOperationException("Cannot apply a transaction whose amount isn't greater than 0.");
        }

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
