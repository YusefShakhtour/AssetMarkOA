using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static AssetMarkOA.Tests.TestData;

namespace AssetMarkOA.Tests;

public class TransactionJsonTests
{
    private static async Task<string> RunAsync(string inputJson)
    {
        var accounts = await Processor.ProcessAsync(Serializer.ParseAsync(Utf8(inputJson)));
        return Serializer.Serialize(
            Summarizer.Summarize(accounts),
            Summarizer.FindTopSpender(accounts));
    }

    [Fact]
    public async Task SampleInputProducesSampleOutput()
    {
        const string input = """
            [
              { "accountId": "A100", "type": "credit", "amount": 500, "timestamp": "2026-05-10T10:00:00Z" },
              { "accountId": "A100", "type": "debit", "amount": 200, "timestamp": "2026-05-10T12:00:00Z" },
              { "accountId": "B200", "type": "debit", "amount": 50, "timestamp": "2026-05-10T11:00:00Z" },
              { "accountId": "B200", "type": "credit", "amount": 20, "timestamp": "2026-05-10T13:00:00Z" },
              { "accountId": "", "type": "credit", "amount": 100, "timestamp": "2026-05-10T14:00:00Z" }
            ]
            """;
        const string expected = """
            {
              "summaries": [
                { "accountId": "A100", "totalCredits": 500.00, "totalDebits": 200.00, "endingBalance": 300.00,
                  "largestTransaction": 500.00, "transactionCount": 2, "overdrawn": false },
                { "accountId": "B200", "totalCredits": 20.00, "totalDebits": 50.00, "endingBalance": -30.00,
                  "largestTransaction": 50.00, "transactionCount": 2, "overdrawn": true }
              ],
              "topSpender": "A100"
            }
            """;

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(await RunAsync(input))));
    }

    [Theory]
    [InlineData("credit", TransactionType.credit)]
    [InlineData("DEBIT", TransactionType.debit)]
    [InlineData("refund", (TransactionType)0)]
    [InlineData("1", (TransactionType)0)] // a number string is not a type name
    [InlineData(null, (TransactionType)0)]
    public async Task TypeIsParsedCaseInsensitivelyAndUnknownBecomesInvalid(string? type, TransactionType expected)
    {
        var typeJson = type is null ? "null" : $"\"{type}\"";
        var tx = Assert.Single(await ToListAsync(Serializer.ParseAsync(Utf8($$"""[{ "accountId": "A", "type": {{typeJson}}, "amount": 10 }]"""))));
        Assert.Equal(expected, tx.Type);
    }

    [Fact]
    public async Task UnknownTypeIsSkippedWithoutFailingOtherRows()
    {
        var output = JsonNode.Parse(await RunAsync("""
            [
              { "accountId": "A", "type": "refund", "amount": 10, "timestamp": "2026-05-10T10:00:00Z" },
              { "accountId": "A", "type": "credit", "amount": 5, "timestamp": "2026-05-10T11:00:00Z" }
            ]
            """))!;

        Assert.Equal(1, (int)output["summaries"]![0]!["transactionCount"]!);
    }

    [Fact]
    public async Task FractionalAmountsRoundTripThroughJson()
    {
        var summary = JsonNode.Parse(await RunAsync("""
            [
              { "accountId": "A", "type": "credit", "amount": 19.99, "timestamp": "2026-05-10T10:00:00Z" },
              { "accountId": "A", "type": "credit", "amount": 0.01, "timestamp": "2026-05-10T11:00:00Z" },
              { "accountId": "A", "type": "debit", "amount": 25.5, "timestamp": "2026-05-10T12:00:00Z" }
            ]
            """))!["summaries"]![0]!;

        Assert.Equal(20.00m, (decimal)summary["totalCredits"]!);
        Assert.Equal(25.5m, (decimal)summary["totalDebits"]!);
        Assert.Equal(-5.50m, (decimal)summary["endingBalance"]!);
        Assert.Equal(25.5m, (decimal)summary["largestTransaction"]!);
        Assert.True((bool)summary["overdrawn"]!);
    }

    [Fact]
    public async Task AmountsAreWrittenWithTwoDecimalPlaces()
    {
        var output = await RunAsync("""
            [
              { "accountId": "A", "type": "credit", "amount": 500, "timestamp": "2026-05-10T10:00:00Z" },
              { "accountId": "A", "type": "debit", "amount": 25.5, "timestamp": "2026-05-10T11:00:00Z" },
              { "accountId": "A", "type": "debit", "amount": 0.005, "timestamp": "2026-05-10T12:00:00Z" }
            ]
            """);

        Assert.Contains("\"totalCredits\": 500.00,", output);
        Assert.Contains("\"totalDebits\": 25.51,", output);     // 25.505 rounds half away from zero
        Assert.Contains("\"endingBalance\": 474.50,", output);  // 474.495 rounds half away from zero
        Assert.Contains("\"largestTransaction\": 500.00,", output);
        Assert.Contains("\"transactionCount\": 3,", output);    // ints are unaffected
    }

    [Fact]
    public async Task ParsingStreamsInsteadOfReadingTheWholeInputFirst()
    {
        var rows = Enumerable.Range(0, 10_000).Select(i => $$"""{ "accountId": "A{{i}}", "type": "credit", "amount": 1 }""");
        var bytes = Encoding.UTF8.GetBytes($"[{string.Join(",", rows)}]");
        using var input = new CountingStream(new MemoryStream(bytes));

        await using var transactions = Serializer.ParseAsync(input).GetAsyncEnumerator();
        Assert.True(await transactions.MoveNextAsync());

        // The first transaction is available after reading only a small part of the input.
        Assert.True(input.BytesRead < bytes.Length / 10, $"read {input.BytesRead} of {bytes.Length} bytes");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("[null]")]
    public async Task EmptyOrNullTopLevelProducesNoAccounts(string json)
    {
        Assert.Empty(await Processor.ProcessAsync(Serializer.ParseAsync(Utf8(json))));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("")]
    public async Task NonArrayTopLevelIsInvalidJson(string json)
    {
        await Assert.ThrowsAsync<JsonException>(() => ToListAsync(Serializer.ParseAsync(Utf8(json))));
    }

    // Each test below puts this valid row next to the row under test, to show a bad row is skipped without stopping the run.
    private const string ValidRow = """{ "accountId": "GOOD", "type": "credit", "amount": 10, "timestamp": "2026-05-10T10:00:00Z" }""";

    private static Task<IReadOnlyList<Account>> ProcessWithValidRowAsync(string row) =>
        Processor.ProcessAsync(Serializer.ParseAsync(Utf8($"[{ValidRow}, {row}]")));

    // Each bad row has a valid timestamp, so it's skipped for the field under test and nothing else.
    [Theory]
    [InlineData("""{ "accountId": 123, "type": "credit", "amount": 10, "timestamp": "2026-05-10T10:00:00Z" }""")]
    [InlineData("""{ "accountId": {}, "type": "credit", "amount": 10, "timestamp": "2026-05-10T10:00:00Z" }""")]
    [InlineData("""{ "accountId": "B", "type": 1, "amount": 10, "timestamp": "2026-05-10T10:00:00Z" }""")]
    [InlineData("""{ "accountId": "B", "type": "1", "amount": 10, "timestamp": "2026-05-10T10:00:00Z" }""")]
    [InlineData("""{ "accountId": "B", "type": "refund", "amount": 10, "timestamp": "2026-05-10T10:00:00Z" }""")]
    [InlineData("""{ "accountId": "B", "type": "credit", "amount": "abc", "timestamp": "2026-05-10T10:00:00Z" }""")]
    [InlineData("""{ "accountId": "B", "type": "credit", "amount": {}, "timestamp": "2026-05-10T10:00:00Z" }""")]
    [InlineData("""{ "accountId": "B", "type": "credit", "amount": true, "timestamp": "2026-05-10T10:00:00Z" }""")]
    [InlineData("""{ "accountId": "B", "type": "credit", "amount": 1e400, "timestamp": "2026-05-10T10:00:00Z" }""")] // too large for decimal
    [InlineData("1")]
    [InlineData("\"x\"")]
    [InlineData("true")]
    [InlineData("[]")]
    public async Task WronglyTypedRowIsSkippedWithoutStoppingTheRun(string badRow)
    {
        var account = Assert.Single(await ProcessWithValidRowAsync(badRow));

        Assert.Equal("GOOD", account.AccountId);
        Assert.Equal(1, account.TransactionCount);
    }

    [Theory]
    [InlineData("""{ "type": "credit", "amount": 10, "timestamp": "2026-05-10T10:00:00Z" }""")]
    [InlineData("""{ "accountId": "B", "amount": 10, "timestamp": "2026-05-10T10:00:00Z" }""")]
    [InlineData("""{ "accountId": "B", "type": "credit", "timestamp": "2026-05-10T10:00:00Z" }""")]
    public async Task RowMissingAccountIdTypeOrAmountIsSkipped(string row)
    {
        Assert.Equal("GOOD", Assert.Single(await ProcessWithValidRowAsync(row)).AccountId);
    }

    // Stricter than the problem statement's three invalid cases: a missing or invalid timestamp also makes a row invalid.
    [Theory]
    [InlineData("""{ "accountId": "B", "type": "credit", "amount": 10 }""")]
    [InlineData("""{ "accountId": "B", "type": "credit", "amount": 10, "timestamp": "abc" }""")]
    [InlineData("""{ "accountId": "B", "type": "credit", "amount": 10, "timestamp": 123 }""")]
    [InlineData("""{ "accountId": "B", "type": "credit", "amount": 10, "timestamp": null }""")]
    [InlineData("""{ "accountId": "B", "type": "credit", "amount": 10, "timestamp": "2026-13-45T00:00:00Z" }""")]
    [InlineData("""{ "accountId": "B", "type": "credit", "amount": 10, "timestamp": "05/10/2026" }""")] // not ISO 8601
    public async Task MissingOrBadTimestampIsSkipped(string row)
    {
        Assert.Equal("GOOD", Assert.Single(await ProcessWithValidRowAsync(row)).AccountId);
    }

    [Theory]
    [InlineData("2026-05-10T10:00:00Z")]
    [InlineData("2026-05-10T10:00:00.123+02:00")]
    [InlineData("2026-05-10")]
    public async Task IsoTimestampsAreAccepted(string timestamp)
    {
        var accounts = await ProcessWithValidRowAsync($$"""{ "accountId": "B", "type": "credit", "amount": 10, "timestamp": "{{timestamp}}" }""");

        Assert.Equal(2, accounts.Count);
    }

    [Fact]
    public async Task QuotedNumericAmountAndAnyCasePropertyNamesAreAccepted()
    {
        var account = Assert.Single(await Processor.ProcessAsync(Serializer.ParseAsync(Utf8("""
            [{ "AccountId": "A", "TYPE": "credit", "amount": "10.50", "timestamp": "2026-05-10T10:00:00Z" }]
            """))));

        Assert.Equal(10.50m, account.TotalCredits);
    }

    [Fact]
    public async Task InputOrderDoesNotChangeTheOutput()
    {
        // Timestamps are deliberately out of order, as the problem statement allows.
        string[] rows =
        [
            """{ "accountId": "A100", "type": "credit", "amount": 500, "timestamp": "2026-05-10T10:00:00Z" }""",
            """{ "accountId": "A100", "type": "debit", "amount": 200, "timestamp": "2026-05-10T12:00:00Z" }""",
            """{ "accountId": "B200", "type": "debit", "amount": 50, "timestamp": "2026-05-10T11:00:00Z" }""",
            """{ "accountId": "B200", "type": "credit", "amount": 20, "timestamp": "2026-05-10T13:00:00Z" }""",
            """{ "accountId": "", "type": "credit", "amount": 100, "timestamp": "2026-05-10T14:00:00Z" }""",
        ];

        var original = await RunAsync($"[{string.Join(",", rows)}]");
        var reversed = await RunAsync($"[{string.Join(",", rows.Reverse())}]");
        var rotated = await RunAsync($"[{string.Join(",", rows.Skip(2).Concat(rows.Take(2)))}]");

        Assert.Equal(original, reversed);
        Assert.Equal(original, rotated);
    }

    [Fact]
    public async Task Utf8ByteOrderMarkIsIgnored()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("""[{ "accountId": "A", "type": "credit", "amount": 10 }]""")).ToArray();

        var tx = Assert.Single(await ToListAsync(Serializer.ParseAsync(new MemoryStream(bytes))));
        Assert.Equal("A", tx.AccountId);
    }

    // Wraps a stream and counts how many bytes have been read from it.
    private sealed class CountingStream(Stream inner) : Stream
    {
        public long BytesRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));

        private int Count(int read)
        {
            BytesRead += read;
            return read;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
