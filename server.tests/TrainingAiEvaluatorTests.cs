using MajakServer.Engine;
using System.Reflection;

namespace MajakServer.Tests;

public class TrainingAiEvaluatorTests
{
    private static readonly int[] EquivalentDragonDiscards =
    {
        0, 1, 2,
        3, 4, 5,
        9, 10, 11,
        18, 19, 20,
        31, 32,
    };

    [Fact]
    public void Evaluate_SelectedTileCanDifferFromLastDrawnTile()
    {
        MajakGameLogic game = CreateGame(EquivalentDragonDiscards, lastSerial: 4);

        TrainingAiDecision decision = new LegacyTrainingAiEvaluator().Evaluate(game, engineOrder: 0);

        Assert.NotEqual(game.Player[0].Tehai.Last().GetSerial(), decision.DiscardSerial);
    }

    [Fact]
    public void Evaluate_EqualValuesUseLowerSerialDeterministically()
    {
        MajakGameLogic game = CreateGame(EquivalentDragonDiscards, lastSerial: 4);
        var evaluator = new LegacyTrainingAiEvaluator();

        int[] selected = Enumerable.Range(0, 3)
            .Select(_ => evaluator.Evaluate(game, engineOrder: 0).DiscardSerial)
            .ToArray();

        Assert.All(selected, serial => Assert.Equal(31, serial));
    }

    [Fact]
    public void Evaluate_DoesNotMutateLiveHand()
    {
        MajakGameLogic game = CreateGame(EquivalentDragonDiscards, lastSerial: 4);
        EnginePlayer player = game.Player[0];
        (int Code, int Serial, int BipaiIndex)[] before = player.Tehai
            .Select(tile => (tile.Code, tile.GetSerial(), tile.BipaiIndex))
            .ToArray();

        _ = new LegacyTrainingAiEvaluator().Evaluate(game, engineOrder: 0);

        Assert.Equal(before, player.Tehai
            .Select(tile => (tile.Code, tile.GetSerial(), tile.BipaiIndex))
            .ToArray());
    }

    [Fact]
    public void Evaluate_ClosedTenpaiDeclaresRiichi()
    {
        int[] tenpaiHand = { 0, 1, 2, 12, 13, 14, 24, 25, 26, 31, 31, 3, 4, 33 };
        MajakGameLogic game = CreateGame(tenpaiHand, lastSerial: 33);

        Assert.True(game.GetBipaiCount() >= MajakConst.PlayerMaxCount);
        Assert.True(game.Player[0].GamePoint >= 1000);
        Assert.True(game.Player[0].IsMenzen);

        var winningCounts = new int[34];
        foreach (int serial in tenpaiHand.Where(serial => serial != 33))
            winningCounts[serial]++;
        winningCounts[5]++;
        var pointMethod = typeof(MajakGameLogic).GetMethod(
            "EvaluateTrainingAiHoraPoints",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var points = ((int Tsumo, int Ron, int RiichiTsumo, int RiichiRon))pointMethod.Invoke(
            game,
            new object[] { 0, winningCounts, PaiCode.MakeSerial(5) })!;
        Assert.True(points.RiichiTsumo > points.Tsumo);
        Assert.True(points.RiichiRon > points.Ron);

        TrainingAiDecision decision = new LegacyTrainingAiEvaluator().Evaluate(game, engineOrder: 0);

        Assert.Equal(33, decision.DiscardSerial);
        Assert.True(decision.ShouldRiichi);
    }

    private static MajakGameLogic CreateGame(IEnumerable<int> serials, int lastSerial)
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
        }
        Array.Fill(game.KyokuInfo.Dora, PaiCode.Invalid);
        Array.Fill(game.KyokuInfo.UraDora, PaiCode.Invalid);

        int bipaiIndex = 0;
        foreach (int serial in serials.Where(serial => serial != lastSerial))
            AddTile(game.Player[0], serial, bipaiIndex++);
        AddTile(game.Player[0], lastSerial, bipaiIndex);
        return game;
    }

    private static void AddTile(EnginePlayer player, int serial, int bipaiIndex)
    {
        PaiCode tile = PaiCode.MakeSerial(serial);
        tile.BipaiIndex = bipaiIndex;
        player.Tehai.Add(tile);
    }
}