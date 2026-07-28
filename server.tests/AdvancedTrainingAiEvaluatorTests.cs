using MajakServer.Engine;
using System.Reflection;

namespace MajakServer.Tests;

public class AdvancedTrainingAiEvaluatorTests
{
    private static readonly int[] EqualHonorChoices =
    {
        0, 1, 2,
        3, 4, 5,
        6, 7, 8,
        9, 10, 11,
        27, 31,
    };

    [Fact]
    public void Evaluate_EqualAttackValueUsesLowerSerial()
    {
        MajakGameLogic game = CreateGame(EqualHonorChoices);

        TrainingAiDecision decision = new AdvancedTrainingAiEvaluator().Evaluate(game, 0);

        Assert.Equal(27, decision.DiscardSerial);
    }

    [Fact]
    public void Evaluate_PreservesDoraWhenEquivalentDiscardExists()
    {
        MajakGameLogic game = CreateGame(EqualHonorChoices);
        game.KyokuInfo.Dora[0] = PaiCode.MakeSerial(30);

        TrainingAiDecision decision = new AdvancedTrainingAiEvaluator().Evaluate(game, 0);

        Assert.Equal(31, decision.DiscardSerial);
    }

    [Fact]
    public void Evaluate_AgainstRiichiPrefersGenbutsu()
    {
        MajakGameLogic game = CreateGame(EqualHonorChoices);
        EnginePlayer opponent = game.Player[1];
        opponent.RichiType = RichiType.Richi;
        opponent.Sutehai.Add(PaiCode.MakeSerial(31));

        TrainingAiDecision decision = new AdvancedTrainingAiEvaluator().Evaluate(game, 0);

        Assert.Equal(31, decision.DiscardSerial);
    }

    [Fact]
    public void Evaluate_UsesVisibleTilesForLiveUkeire()
    {
        MajakGameLogic game = CreateGame(EqualHonorChoices);
        game.Player[1].Sutehai.Add(PaiCode.MakeSerial(31));
        game.Player[2].Sutehai.Add(PaiCode.MakeSerial(31));
        game.Player[3].Sutehai.Add(PaiCode.MakeSerial(31));

        TrainingAiDecision decision = new AdvancedTrainingAiEvaluator().Evaluate(game, 0);

        Assert.Equal(31, decision.DiscardSerial);
    }

    [Fact]
    public void SelectPhysicalTile_DiscardsNormalCopyBeforeRedFive()
    {
        PaiCode normalFive = PaiCode.MakeSerial(4);
        normalFive.BipaiIndex = 10;
        PaiCode redFive = PaiCode.MakeSerial(4);
        redFive.BipaiIndex = 11;
        redFive.IsRed = true;
        MethodInfo method = typeof(AdvancedTrainingAiEvaluator).GetMethod(
            "SelectPhysicalTile",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        PaiCode selected = (PaiCode)method.Invoke(null, new object[] { new[] { redFive, normalFive } })!;

        Assert.Equal(normalFive.BipaiIndex, selected.BipaiIndex);
        Assert.False(selected.IsRed);
    }

    [Fact]
    public void Evaluate_NoYakuTenpaiDeclaresRiichi()
    {
        int[] tenpaiHand = { 0, 1, 2, 12, 13, 14, 24, 25, 26, 31, 31, 3, 4, 33 };
        MajakGameLogic game = CreateGame(tenpaiHand);

        TrainingAiDecision decision = new AdvancedTrainingAiEvaluator().Evaluate(game, 0);

        Assert.Equal(33, decision.DiscardSerial);
        Assert.True(decision.ShouldRiichi);
    }

    [Fact]
    public void Evaluate_DoesNotMutateLiveState()
    {
        MajakGameLogic game = CreateGame(EqualHonorChoices);
        EnginePlayer player = game.Player[0];
        (int Code, int Serial, int BipaiIndex)[] before = player.Tehai
            .Select(tile => (tile.Code, tile.GetSerial(), tile.BipaiIndex))
            .ToArray();

        _ = new AdvancedTrainingAiEvaluator().Evaluate(game, 0);

        Assert.Equal(before, player.Tehai
            .Select(tile => (tile.Code, tile.GetSerial(), tile.BipaiIndex))
            .ToArray());
    }

    private static MajakGameLogic CreateGame(IEnumerable<int> serials)
    {
        var game = new MajakGameLogic();
        game.InitHanchan(new RuleInfo
        {
            Hanchan = true,
            Kuitan = true,
            Contest = 0,
            AkaDora = 0,
        });

        foreach (EnginePlayer player in game.Player)
        {
            player.Tehai.Clear();
            player.Sutehai.Clear();
            player.Furo.Clear();
            player.RichiType = RichiType.None;
        }
        Array.Fill(game.KyokuInfo.Dora, PaiCode.Invalid);
        Array.Fill(game.KyokuInfo.UraDora, PaiCode.Invalid);

        int bipaiIndex = 0;
        foreach (int serial in serials)
        {
            PaiCode tile = PaiCode.MakeSerial(serial);
            tile.BipaiIndex = bipaiIndex++;
            game.Player[0].Tehai.Add(tile);
        }
        return game;
    }
}
