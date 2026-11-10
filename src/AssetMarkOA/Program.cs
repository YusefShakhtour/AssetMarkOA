using AssetMarkOA;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Usage: dotnet run -- <input.json>   (reads stdin when no path is given)
IHost host;
App app;
try {
    host = AppHost.Build();
    app = host.Services.GetRequiredService<App>();
} catch (Exception ex) {
    // The logger may not exist yet. Configuration is controlled by whoever runs the program, so its message is safe to show.
    Console.Error.WriteLine($"Could not start: {ex.Message}");
    return 2;
}

// Warn instead of failing: the output is still correct, only the diagnostics are missing.
var logDirectory = AppHost.ResolveLogDirectory(host.Services.GetRequiredService<IConfiguration>());
if (!AppHost.CanWriteTo(logDirectory)) {
    Console.Error.WriteLine($"Warning: logs can't be written to {logDirectory}; set AssetMarkOA__LogDirectory to a writable folder.");
}

// Disposing the host flushes the log file before the program exits.
using (host) {
    return app.Run(args, Console.In, Console.Out, Console.Error);
}
