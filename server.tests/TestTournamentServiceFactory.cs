using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MajakServer.Hubs;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Tests;

internal static class TestTournamentServiceFactory
{
    public static TournamentService Create(
        TournamentRepository tournamentRepo,
        ILogger<TournamentService>? logger = null,
        PlayerSessionService? session = null,
        IHubContext<MajakGameHub>? hub = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(tournamentRepo);
        services.AddSingleton(new GameMoneyService(
            new Mock<PlayerRepository>(MockBehavior.Loose).Object,
            new RatingService()));
        var provider = services.BuildServiceProvider();

        return new TournamentService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            session ?? new PlayerSessionService(),
            hub ?? new Mock<IHubContext<MajakGameHub>>().Object,
            logger ?? new Mock<ILogger<TournamentService>>().Object);
    }
}