using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MajakServer.Infrastructure;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;

namespace MajakServer.Tests;

internal static class TestMasterCacheFactory
{
    public static RedisService CreateRedisService()
        => new(new ConfigurationBuilder().Build());

    public static MasterCacheService Create(
        PlayerRepository? playerRepo = null,
        ItemRepository? itemRepo = null,
        ChannelRepository? channelRepo = null)
    {
        var services = new ServiceCollection();
        if (channelRepo is null)
        {
            var channelRepoMock = new Mock<ChannelRepository>(
                MockBehavior.Loose, (GameDataContextFactory)null!, CreateRedisService());
            channelRepoMock.Setup(repo => repo.GetChannelListAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<ChannelInfo>());
            channelRepo = channelRepoMock.Object;
        }
        services.AddSingleton(playerRepo ?? new Mock<PlayerRepository>(MockBehavior.Loose).Object);
        services.AddSingleton(itemRepo ?? new Mock<ItemRepository>(MockBehavior.Loose).Object);
        services.AddSingleton(channelRepo);

        var provider = services.BuildServiceProvider();
        var redis = CreateRedisService();
        return new MasterCacheService(
            redis,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MasterCacheService>.Instance);
    }
}