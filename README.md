# AssetMark OA – Transaction Summary

Reads a JSON array of transactions and prints a summary for each account, plus the account with the highest total debits (`topSpender`).

## Running it

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Run these from the repository root:

```bash
dotnet run --project src/AssetMarkOA -- src/AssetMarkOA/sample-input.json   # from a file
cat input.json | dotnet run --project src/AssetMarkOA                       # from stdin
dotnet test                                                                 # run the tests
```

- **Output:** stdout contains only the JSON result. Errors go to stderr.
- **Exit codes:** `0` success, `1` invalid input, `2` unexpected error, `130` cancelled with Ctrl+C.
- **Logs:** written to `logs/` next to the program (e.g. `src/AssetMarkOA/bin/Debug/net8.0/logs/`).
  - For full detail, set `Serilog__MinimumLevel__Default=Debug`. This writes one line per transaction, so use it only to diagnose a specific run.
  - To write logs somewhere else, set `AssetMarkOA__LogDirectory`.

## Assumptions

- **Batch tool.** Each run processes one input and exits; nothing is stored between runs. Input is UTF-8 JSON with an array at the top level.
- **Money is `decimal`.** Amounts can have decimal places and are always positive; `type` decides credit or debit. Every account starts at a balance of 0.
- **Valid transaction.** It needs a non-empty `accountId`, a `type` that is the word `credit` or `debit` (any letter case), an `amount` greater than 0, and a valid ISO 8601 `timestamp` (e.g. `2026-05-10T10:00:00Z`).
  - The timestamp rule is deliberately stricter than the problem statement's three invalid cases: a transaction without a correct timestamp is suspect financial data, so it isn't counted.
  - Invalid rows are skipped and not counted. That includes rows with a missing or wrongly typed field (e.g. `"amount": "abc"`) and entries that aren't objects, such as `null`.
  - Only broken JSON syntax, or a top level that isn't an array, fails the whole run, with exit code 1 and the line and byte where it failed.
- **Timestamps** are checked but not used in any calculation, so input order doesn't matter.
- **Account IDs.** IDs are trimmed and matched case-insensitively, so `" a100"` and `"A100"` are one account. The output shows them in uppercase.
- **No deduplication.** Identical transactions are both counted, because nothing in the input identifies a transaction.
- **Overdrawn** means the final balance is below 0.
- **Largest transaction** is the largest single valid amount, credit or debit.
- **Top spender** is the account with the highest total debits. A tie goes to the lexicographically smaller ID. It is `null` if there are no debits.
- **Output order.** Summaries are sorted by account ID.
- **Two decimal places.** Amounts are printed with exactly two decimal places (`500.00`).

## Design choices and tradeoffs

- **Parse, process, summarize, serialize.** Each step is its own class. `TransactionProcessor` and `Account` are the only code that changes state; `AccountSummarizer` only reads accounts and returns immutable records. Accounts are kept in a dictionary keyed by normalized ID, which makes lookups O(1) and duplicates impossible.
- **Streamed input, async all the way.** Transactions are parsed and applied as the file is read, so memory grows with the number of accounts, not the size of the input. On a 1-million-row (88 MB) file, peak memory dropped from 519 MB to 66 MB.
  - The cost is that a JSON error is found partway through the run. Output is only written at the end, so the user still sees no partial result.
- **Standard .NET host.** Dependency injection, `appsettings.json` with environment variable overrides, and Serilog writing to a file only, so stdout stays clean.
  - The three services have interfaces because `App` depends on them and tests replace them.
  - `Account` and the records have no interfaces because they're data. Account types, if ever needed, would get a base class or a rule object plus a factory, once a requirement defines what differs.
- **Logs keep financial data out by default.**
  - Account IDs, amounts and balances are logged only at `Debug`.
  - Unexpected errors show the user only a reference ID; the stack trace is in the log under that ID.
  - The program warns at startup if the log folder isn't writable, because Serilog would otherwise drop log entries silently.
- **No repository or manager layers.** There's no database or API, so those layers would add files without adding clarity.
