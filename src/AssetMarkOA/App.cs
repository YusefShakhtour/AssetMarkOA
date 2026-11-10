using System.Text.Json;

namespace AssetMarkOA;

public sealed class App {

    private readonly ITransactionJsonSerializer serializer;
    private readonly ITransactionProcessor processor;
    private readonly IAccountSummarizer summarizer;

    public App(ITransactionJsonSerializer serializer, ITransactionProcessor processor, IAccountSummarizer summarizer) {
        this.serializer = serializer;
        this.processor = processor;
        this.summarizer = summarizer;
    }

    // Exit codes: 0 success, 1 invalid input the user can fix, 2 unexpected error.
    // Takes its streams as parameters so tests can run it without the real console.
    public int Run(string[] args, TextReader input, TextWriter output, TextWriter error) {
        try {
            var json = args.Length > 0 ? File.ReadAllText(args[0]) : input.ReadToEnd();

            var accounts = processor.Process(serializer.Parse(json));

            output.WriteLine(serializer.Serialize(
                summarizer.Summarize(accounts),
                summarizer.FindTopSpender(accounts)));
            return 0;
        } catch (JsonException ex) {
            error.WriteLine($"Invalid input: {ex.Message}");
            return 1;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) {
            error.WriteLine($"Could not read input: {ex.Message}");
            return 1;
        } catch (Exception) {
            // Deliberately no details: unexpected errors must not expose internals or transaction data to the user.
            error.WriteLine("An unexpected error occurred.");
            return 2;
        }
    }
}
