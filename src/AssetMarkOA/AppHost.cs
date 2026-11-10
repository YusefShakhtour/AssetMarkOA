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

        // Relative to the program, not the current directory, so logs never land in the user's folder.
        var logDirectory = Path.Combine(AppContext.BaseDirectory, builder.Configuration["AssetMarkOA:LogDirectory"] ?? "logs");
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
}
