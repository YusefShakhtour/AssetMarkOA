using AssetMarkOA;
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

// Disposing the host flushes the log file before the program exits.
using (host) {
    return app.Run(args, Console.In, Console.Out, Console.Error);
}
