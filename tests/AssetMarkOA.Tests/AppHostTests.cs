using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AssetMarkOA.Tests;

public class AppHostTests
{
    [Fact]
    public void HostResolvesAppAndLoadsSettingsFromProgramFolder()
    {
        using var host = AppHost.Build();

        Assert.NotNull(host.Services.GetRequiredService<App>());
        Assert.Equal(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
            host.Services.GetRequiredService<IHostEnvironment>().ContentRootPath.TrimEnd(Path.DirectorySeparatorChar));
        // Only set in appsettings.json, so this proves the file was found.
        Assert.Equal("logs", host.Services.GetRequiredService<IConfiguration>()["AssetMarkOA:LogDirectory"]);
    }
}
