namespace AssetMarkOA;

// Starts at 1 so an unset Type (default 0) fails validation instead of counting as a credit.
public enum TransactionType {
    credit = 1,
    debit = 2
}
