using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AssetMarkOA;

// Logging rule: anything that identifies an account or contains money is logged at Debug only.
public sealed class TransactionJsonSerializer : ITransactionJsonSerializer {
    // Web defaults: camelCase names when writing, case-insensitive names when reading.
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    // Separate options so the money format only affects output; input parsing keeps the default number handling.
    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web) {
        WriteIndented = true,
        Converters = { new MoneyConverter() },
    };

    private readonly ILogger<TransactionJsonSerializer> logger;

    public TransactionJsonSerializer(ILogger<TransactionJsonSerializer> logger) {
        this.logger = logger;
    }

    // Every decimal in the output is money, so always write at least two decimal places (500 -> 500.00),
    // but never round away precision (0.006 -> 0.006): a rounded balance of 0.00 next to "overdrawn": true would contradict itself.
    private sealed class MoneyConverter : JsonConverter<decimal> {
        // Two fixed places, then up to 26 optional ones: decimal holds at most 28.
        private const string Format = "0.00##########################";

        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDecimal();

        // InvariantCulture so a machine set to e.g. German doesn't write "500,00", which is invalid JSON.
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }

    // Every field is read as a raw JSON value, so a missing or wrongly typed field can't fail the whole input.
    // ToTransaction converts each one; a value that can't be converted makes only that row invalid.
    private sealed record RawTransaction(JsonElement AccountId, JsonElement Type, JsonElement Amount, JsonElement Timestamp);

    // Rows are read and returned one at a time, so memory doesn't grow with the size of the input.
    // Row count and timing are logged by the processor, because parsing happens inside its loop.
    public async IAsyncEnumerable<TransactionModel> ParseAsync(Stream input, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        logger.LogInformation("Parse started");

        await foreach (var row in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(input, ReadOptions, cancellationToken)) {
            yield return ToTransaction(row);
        }
    }

    private static TransactionModel ToTransaction(JsonElement row) {
        // A row that isn't an object (null, a number, a string...) becomes an empty transaction,
        // so the processor rejects it and row numbers still match the input.
        if (row.ValueKind != JsonValueKind.Object) {
            return new TransactionModel();
        }

        // Deserializing (rather than reading properties by hand) keeps property names case-insensitive.
        var raw = row.Deserialize<RawTransaction>(ReadOptions)!;
        return new TransactionModel {
            AccountId = raw.AccountId.ValueKind == JsonValueKind.String ? raw.AccountId.GetString()! : "",
            Type = ReadType(raw.Type),
            Amount = ReadAmount(raw.Amount),
            // ISO 8601 (e.g. 2026-05-10T10:00:00Z). Missing, not a string, or not a valid date leaves null, which validation rejects.
            Timestamp = raw.Timestamp.ValueKind == JsonValueKind.String && raw.Timestamp.TryGetDateTime(out var timestamp) ? timestamp : null,
        };
    }

    // Only the words "credit" and "debit", in any letter case; anything else leaves default (0), which validation rejects.
    // Enum.TryParse isn't used because it also accepts numbers, so "1" would count as a credit.
    private static TransactionType ReadType(JsonElement value) {
        if (value.ValueKind != JsonValueKind.String) {
            return default;
        }
        var text = value.GetString();
        if (string.Equals(text, "credit", StringComparison.OrdinalIgnoreCase)) {
            return TransactionType.credit;
        }
        if (string.Equals(text, "debit", StringComparison.OrdinalIgnoreCase)) {
            return TransactionType.debit;
        }
        return default;
    }

    // A JSON number, or a numeric string (accepted as before). Anything else, including a number too large for decimal,
    // becomes 0, which validation rejects.
    private static decimal ReadAmount(JsonElement value) => value.ValueKind switch {
        JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
        JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => 0,
    };

    public string Serialize(IReadOnlyList<AccountSummary> summaries, string? topSpender) {
        logger.LogInformation("Serialize started");
        var stopwatch = Stopwatch.StartNew();

        var json = JsonSerializer.Serialize(new { summaries, topSpender }, WriteOptions);

        logger.LogInformation("Serialize finished: {SummaryCount} summaries in {ElapsedMs} ms", summaries.Count, stopwatch.ElapsedMilliseconds);
        return json;
    }
}
