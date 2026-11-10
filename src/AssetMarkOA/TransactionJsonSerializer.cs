using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetMarkOA;

public sealed class TransactionJsonSerializer : ITransactionJsonSerializer {
    // Web defaults: camelCase names when writing, case-insensitive names when reading.
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    // Separate options so the money format only affects output; input parsing keeps the default number handling.
    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web) {
        WriteIndented = true,
        Converters = { new TwoDecimalPlacesConverter() },
    };

    // Every decimal in the output is money, so always write two decimal places (500 -> 500.00).
    private sealed class TwoDecimalPlacesConverter : JsonConverter<decimal> {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDecimal();

        // InvariantCulture so a machine set to e.g. German doesn't write "500,00", which is invalid JSON.
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(value.ToString("0.00", CultureInfo.InvariantCulture));
    }

    // Type is read as a string so an unknown value becomes one invalid row instead of failing the whole input.
    private sealed record RawTransaction(string? AccountId, string? Type, decimal Amount, DateTime Timestamp);

    public IReadOnlyList<TransactionModel> Parse(string json) {
        var raw = JsonSerializer.Deserialize<List<RawTransaction?>>(json, ReadOptions) ?? [];
        return raw
            .Where(r => r is not null)
            .Select(r => new TransactionModel {
                AccountId = r!.AccountId ?? "",
                // An unknown type leaves default (0), which IsValid rejects.
                Type = Enum.TryParse<TransactionType>(r.Type, ignoreCase: true, out var type) ? type : default,
                Amount = r.Amount,
                Timestamp = r.Timestamp,
            })
            .ToList();
    }

    public string Serialize(IReadOnlyList<AccountSummary> summaries, string? topSpender) =>
        JsonSerializer.Serialize(new { summaries, topSpender }, WriteOptions);
}
