using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AssetMarkOA;

public static class AppHost {

    private const string LogOutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{RunId}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    public static IHost Build() {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings {
            // Args are deliberately not passed: the host would read a path like /tmp/in.json as a setting.
            // Look for appsettings.json next to the program, whatever the current directory is.
            ContentRootPath = AppContext.BaseDirectory,
        });

        var logDirectory = ResolveLogDirectory(builder.Configuration);
        var serilog = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDirectory, "assetmarkoa-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: LogOutputTemplate)
            .CreateLogger();

        // The default console provider would write log lines into stdout and corrupt the JSON output.
        builder.Logging.ClearProviders();
        // Pass everything to Serilog; its configured minimum level decides what is written.
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddSerilog(serilog, dispose: true);

        builder.Services.AddSingleton<ITransactionJsonSerializer, TransactionJsonSerializer>();
        builder.Services.AddSingleton<ITransactionProcessor, TransactionProcessor>();
        builder.Services.AddSingleton<IAccountSummarizer, AccountSummarizer>();
        builder.Services.AddSingleton<App>();

        return builder.Build();
    }

    // A relative setting is resolved against the program's folder, not the current directory,
    // so logs never land in the user's folder. An absolute setting is used as is.
    public static string ResolveLogDirectory(IConfiguration configuration) =>
        Path.Combine(AppContext.BaseDirectory, configuration["AssetMarkOA:LogDirectory"] ?? "logs");

    // Serilog silently drops log entries when it can't write, so this is checked once at startup.
    public static bool CanWriteTo(string directory) {
        try {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) {
            return false;
        }
    }
}
