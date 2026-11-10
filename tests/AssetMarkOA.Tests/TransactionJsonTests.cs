using System.Text.Json.Nodes;

namespace AssetMarkOA.Tests;

public class TransactionJsonTests
{
    private static string Run(string inputJson)
    {
        var accounts = TransactionProcessor.Process(TransactionJson.Parse(inputJson));
        return TransactionJson.Serialize(
            AccountSummarizer.Summarize(accounts),
            AccountSummarizer.FindTopSpender(accounts));
    }

    [Fact]
    public void SampleInputProducesSampleOutput()
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

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(Run(input))));
    }

    [Theory]
    [InlineData("credit", TransactionType.credit)]
    [InlineData("DEBIT", TransactionType.debit)]
    [InlineData("refund", (TransactionType)0)]
    [InlineData(null, (TransactionType)0)]
    public void TypeIsParsedCaseInsensitivelyAndUnknownBecomesInvalid(string? type, TransactionType expected)
    {
        var typeJson = type is null ? "null" : $"\"{type}\"";
        var tx = Assert.Single(TransactionJson.Parse($$"""[{ "accountId": "A", "type": {{typeJson}}, "amount": 10 }]"""));
        Assert.Equal(expected, tx.Type);
    }

    [Fact]
    public void UnknownTypeIsSkippedWithoutFailingOtherRows()
    {
        var output = JsonNode.Parse(Run("""
            [
              { "accountId": "A", "type": "refund", "amount": 10 },
              { "accountId": "A", "type": "credit", "amount": 5 }
            ]
            """))!;

        Assert.Equal(1, (int)output["summaries"]![0]!["transactionCount"]!);
    }

    [Fact]
    public void FractionalAmountsRoundTripThroughJson()
    {
        var summary = JsonNode.Parse(Run("""
            [
              { "accountId": "A", "type": "credit", "amount": 19.99 },
              { "accountId": "A", "type": "credit", "amount": 0.01 },
              { "accountId": "A", "type": "debit", "amount": 25.5 }
            ]
            """))!["summaries"]![0]!;

        Assert.Equal(20.00m, (decimal)summary["totalCredits"]!);
        Assert.Equal(25.5m, (decimal)summary["totalDebits"]!);
        Assert.Equal(-5.50m, (decimal)summary["endingBalance"]!);
        Assert.Equal(25.5m, (decimal)summary["largestTransaction"]!);
        Assert.True((bool)summary["overdrawn"]!);
    }

    [Fact]
    public void AmountsAreWrittenWithTwoDecimalPlaces()
    {
        var output = Run("""
            [
              { "accountId": "A", "type": "credit", "amount": 500 },
              { "accountId": "A", "type": "debit", "amount": 25.5 },
              { "accountId": "A", "type": "debit", "amount": 0.005 }
            ]
            """);

        Assert.Contains("\"totalCredits\": 500.00,", output);
        Assert.Contains("\"totalDebits\": 25.51,", output);     // 25.505 rounds half away from zero
        Assert.Contains("\"endingBalance\": 474.50,", output);  // 474.495 rounds half away from zero
        Assert.Contains("\"largestTransaction\": 500.00,", output);
        Assert.Contains("\"transactionCount\": 3,", output);    // ints are unaffected
    }
}
