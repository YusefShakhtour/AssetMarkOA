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
        Converters = { new TwoDecimalPlacesConverter() },
    };

    private readonly ILogger<TransactionJsonSerializer> logger;

    public TransactionJsonSerializer(ILogger<TransactionJsonSerializer> logger) {
        this.logger = logger;
    }

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

    // Rows are read and returned one at a time, so memory doesn't grow with the size of the input.
    // Row count and timing are logged by the processor, because parsing happens inside its loop.
    public async IAsyncEnumerable<TransactionModel> ParseAsync(Stream input, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        logger.LogInformation("Parse started");

        await foreach (var r in JsonSerializer.DeserializeAsyncEnumerable<RawTransaction?>(input, ReadOptions, cancellationToken)) {
            // A null row becomes an empty transaction, so the processor rejects it and row numbers still match the input.
            yield return r is null ? new TransactionModel() : new TransactionModel {
                AccountId = r.AccountId ?? "",
                // An unknown type leaves default (0), which validation rejects.
                Type = Enum.TryParse<TransactionType>(r.Type, ignoreCase: true, out var type) ? type : default,
                Amount = r.Amount,
                Timestamp = r.Timestamp,
            };
        }
    }

    public string Serialize(IReadOnlyList<AccountSummary> summaries, string? topSpender) {
        logger.LogInformation("Serialize started");
        var stopwatch = Stopwatch.StartNew();

        var json = JsonSerializer.Serialize(new { summaries, topSpender }, WriteOptions);

        logger.LogInformation("Serialize finished: {SummaryCount} summaries in {ElapsedMs} ms", summaries.Count, stopwatch.ElapsedMilliseconds);
        return json;
    }
}
