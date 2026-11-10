namespace AssetMarkOA;

// An account's running total exceeded decimal's range (about 7.9e28): each amount fits, but their sum doesn't.
public sealed class AccountTotalOutOfRangeException : Exception {
    public AccountTotalOutOfRangeException(Exception innerException)
        : base("An account's totals exceed the largest supported amount.", innerException) {
    }
}
