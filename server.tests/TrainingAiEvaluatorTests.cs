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

    [Fact]
    public void EvaluateTrainingAiHoraPoints_PreservesLegacyRedTileState()
    {
        int[] hand = { 0, 1, 2, 12, 13, 14, 24, 25, 26, 31, 31, 3, 4, 33 };
        MajakGameLogic plainGame = CreateGame(hand, lastSerial: 33);
        MajakGameLogic redGame = CreateGame(hand, lastSerial: 33);
        EnginePlayer redPlayer = redGame.Player[0];
        int redIndex = redPlayer.Tehai.FindIndex(tile => tile.GetSerial() == 4);
        PaiCode redTile = redPlayer.Tehai[redIndex];
        redTile.IsRed = true;
        redPlayer.Tehai[redIndex] = redTile;

        var winningCounts = new int[34];
        foreach (int serial in hand.Where(serial => serial != 33)) winningCounts[serial]++;
        winningCounts[5]++;
        var pointMethod = typeof(MajakGameLogic).GetMethod(
            "EvaluateTrainingAiHoraPoints",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var plainPoints = ((int Tsumo, int Ron, int RiichiTsumo, int RiichiRon))pointMethod.Invoke(
            plainGame,
            new object[] { 0, winningCounts, PaiCode.MakeSerial(5) })!;
        var redPoints = ((int Tsumo, int Ron, int RiichiTsumo, int RiichiRon))pointMethod.Invoke(
            redGame,
            new object[] { 0, winningCounts, PaiCode.MakeSerial(5) })!;

        Assert.True(redPoints.Tsumo > plainPoints.Tsumo);
        Assert.True(redPoints.Ron > plainPoints.Ron);

        MajakGameLogic redWinningGame = CreateGame(
            hand.Where(serial => serial != 33).Append(5),
            lastSerial: 5);
        EnginePlayer redWinningPlayer = redWinningGame.Player[0];
        int redWinningIndex = redWinningPlayer.Tehai.FindIndex(tile => tile.GetSerial() == 5);
        PaiCode redWinningTile = redWinningPlayer.Tehai[redWinningIndex];
        redWinningTile.IsRed = true;
        redWinningPlayer.Tehai[redWinningIndex] = redWinningTile;
        var redWinningPoints = ((int Tsumo, int Ron, int RiichiTsumo, int RiichiRon))pointMethod.Invoke(
            redWinningGame,
            new object[] { 0, winningCounts, PaiCode.MakeSerial(5) })!;

        Assert.True(redWinningPoints.Tsumo > plainPoints.Tsumo);
        Assert.Equal(plainPoints.Ron, redWinningPoints.Ron);
    }

    [Fact]
    public void EvaluateTrainingAiHoraPoints_UsesLegacyOriginalHandForRedDora()
    {
        int[] sourceHand = { 4, 0, 1, 2, 12, 13, 14, 24, 25, 26, 31, 31, 3, 33 };
        MajakGameLogic plainGame = CreateGame(sourceHand, lastSerial: 33);
        MajakGameLogic redGame = CreateGame(sourceHand, lastSerial: 33);
        int redIndex = redGame.Player[0].Tehai.FindIndex(tile => tile.GetSerial() == 4);
        PaiCode redTile = redGame.Player[0].Tehai[redIndex];
        redTile.IsRed = true;
        redGame.Player[0].Tehai[redIndex] = redTile;

        int[] candidateHand = { 9,10,11, 12,13,14, 15,16,17, 18,18,18, 27,27 };
        var candidateCounts = new int[34];
        foreach (int serial in candidateHand) candidateCounts[serial]++;
        var pointMethod = typeof(MajakGameLogic).GetMethod(
            "EvaluateTrainingAiHoraPoints",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var plainPoints = ((int Tsumo, int Ron, int RiichiTsumo, int RiichiRon))pointMethod.Invoke(
            plainGame,
            new object[] { 0, candidateCounts, PaiCode.MakeSerial(17) })!;
        var redPoints = ((int Tsumo, int Ron, int RiichiTsumo, int RiichiRon))pointMethod.Invoke(
            redGame,
            new object[] { 0, candidateCounts, PaiCode.MakeSerial(17) })!;

        Assert.True(redPoints.Tsumo > plainPoints.Tsumo);
        Assert.True(redPoints.Ron > plainPoints.Ron);
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