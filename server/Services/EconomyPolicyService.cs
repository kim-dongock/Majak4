using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;

namespace MajakServer.Services;

public sealed record GameEconomyPolicy(
    long InitialGp,
    long FreeReplenishTargetGp,
    int FreeReplenishDailyLimit,
    DateTime UpdatedAt);

public interface IGameEconomyPolicyService
{
    Task<GameEconomyPolicy> GetCurrentAsync();
}

public sealed class GameEconomyPolicyService : IGameEconomyPolicyService
{
    private readonly EconomyPolicyRepository _repository;

    public GameEconomyPolicyService(EconomyPolicyRepository repository) => _repository = repository;

    public async Task<GameEconomyPolicy> GetCurrentAsync() =>
        await _repository.GetAsync() ?? new GameEconomyPolicy(
            GameConst.DefaultMoney,
            GameConst.AllinMoney,
            GameConst.AllinCountMax,
            DateTime.MinValue);
}