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

    [Fact]
    public void RelativeLogDirectoryIsUnderProgramFolderAndAbsoluteIsUsedAsIs()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "assetmarkoa-logs");

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "logs"), AppHost.ResolveLogDirectory(Config("logs")));
        Assert.Equal(absolute, AppHost.ResolveLogDirectory(Config(absolute)));
    }

    [Fact]
    public void CanWriteToWritableFolderLeavesNoTestFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"assetmarkoa-test-{Guid.NewGuid():N}");
        try
        {
            Assert.True(AppHost.CanWriteTo(directory));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CanWriteToReturnsFalseWhenFolderCannotBeCreated()
    {
        // A regular file with the folder's name blocks creating the folder on every OS, without relying on permissions.
        var file = Path.GetTempFileName();
        try
        {
            Assert.False(AppHost.CanWriteTo(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static IConfiguration Config(string logDirectory) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AssetMarkOA:LogDirectory"] = logDirectory })
            .Build();
}
