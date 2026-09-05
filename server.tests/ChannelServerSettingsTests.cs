using MajakServer.Infrastructure;

namespace MajakServer.Tests;

public class ChannelServerSettingsTests
{
    [Fact]
    public void ResolveAssignedUrl_Default_UsesDatabaseAssignment()
    {
        var settings = new ChannelServerSettings
        {
            ServerUrl = "http://localhost:5246",
        };

        var result = settings.ResolveAssignedUrl("https://alpha.example.test/");

        Assert.Equal("https://alpha.example.test", result);
    }

    [Fact]
    public void ResolveAssignedUrl_HostOnlyAssignment_UsesConfiguredScheme()
    {
        var settings = new ChannelServerSettings
        {
            ServerUrl = "https://alpha-app-majak4.hange.jp",
        };

        var result = settings.ResolveAssignedUrl("alpha-app-majak4.hange.jp");

        Assert.Equal("https://alpha-app-majak4.hange.jp", result);
    }

    [Fact]
    public void ResolveAssignedUrl_OverrideEnabled_UsesConfiguredServerUrl()
    {
        var settings = new ChannelServerSettings
        {
            ServerUrl = "http://localhost:5246/",
            UseConfiguredServerUrl = true,
        };

        var result = settings.ResolveAssignedUrl("https://alpha.example.test");

        Assert.Equal("http://localhost:5246", result);
    }
}