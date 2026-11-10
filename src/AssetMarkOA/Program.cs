using AssetMarkOA;

// Usage: dotnet run -- <input.json>   (reads stdin when no path is given)
var app = new App(new TransactionJsonSerializer(), new TransactionProcessor(), new AccountSummarizer());
return app.Run(args, Console.In, Console.Out, Console.Error);
