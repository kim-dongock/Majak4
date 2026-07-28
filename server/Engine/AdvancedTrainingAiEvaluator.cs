namespace MajakServer.Engine;

/// <summary>
/// Optional non-legacy training policy. It combines shanten, live ukeire,
/// hand value preservation, and basic riichi defence without mutating the game.
/// </summary>
public sealed class AdvancedTrainingAiEvaluator : ITrainingAiEvaluator
{
    private const double ShantenWeight = 100_000;
    private const double UkeireWeight = 1_000;
    private const double ShapeWeight = 40;
    private const double ValueWeight = 120;
    private const double DangerWeight = 350;

    public TrainingAiDecision Evaluate(MajakGameLogic game, int engineOrder, int aiType = 0)
    {
        ArgumentNullException.ThrowIfNull(game);
        if ((uint)engineOrder >= MajakConst.PlayerMaxCount)
            throw new ArgumentOutOfRangeException(nameof(engineOrder));

        EnginePlayer player = game.Player[engineOrder];
        if (player.Tehai.Count == 0)
            throw new InvalidOperationException("Training AI cannot evaluate an empty hand.");

        int[] hand = BuildCounts(player.Tehai);
        int[] remaining = BuildRemainingCounts(game, player);
        int openMeldCount = player.Furo.Count;
        Candidate? best = null;

        foreach (IGrouping<int, PaiCode> group in player.Tehai.GroupBy(tile => tile.GetSerial()))
        {
            int serial = group.Key;
            hand[serial]--;
            int shanten = CalculateShanten(hand, openMeldCount);
            UkeireResult ukeire = CalculateUkeire(hand, remaining, openMeldCount, shanten);
            double danger = CalculateDanger(game, engineOrder, serial, remaining);
            double valueLoss = CalculateValueLoss(game, group, serial);
            double score = -shanten * ShantenWeight
                + ukeire.LiveTileCount * UkeireWeight
                + ukeire.WaitQuality * ShapeWeight
                - valueLoss * ValueWeight
                - danger * DangerWeight;

            PaiCode discard = SelectPhysicalTile(group);
            var candidate = new Candidate(serial, discard.BipaiIndex, shanten, ukeire, score);
            if (best == null || IsBetter(candidate, best.Value))
                best = candidate;
            hand[serial]++;
        }

        if (best == null)
            throw new InvalidOperationException("Advanced training AI found no discard candidate.");

        bool shouldRiichi = best.Value.Shanten == 0
            && ShouldDeclareRiichi(game, player, engineOrder, best.Value, hand, remaining);
        return new TrainingAiDecision(
            best.Value.Serial,
            shouldRiichi,
            best.Value.BipaiIndex);
    }

    private static bool IsBetter(Candidate candidate, Candidate current)
    {
        const double epsilon = 0.000001;
        if (candidate.Score > current.Score + epsilon) return true;
        if (candidate.Score < current.Score - epsilon) return false;
        if (candidate.Shanten != current.Shanten) return candidate.Shanten < current.Shanten;
        if (candidate.Ukeire.LiveTileCount != current.Ukeire.LiveTileCount)
            return candidate.Ukeire.LiveTileCount > current.Ukeire.LiveTileCount;
        return candidate.Serial < current.Serial;
    }

    private static PaiCode SelectPhysicalTile(IEnumerable<PaiCode> tiles)
        => tiles.OrderBy(tile => tile.IsRed).ThenBy(tile => tile.BipaiIndex).First();

    private static int[] BuildCounts(IEnumerable<PaiCode> tiles)
    {
        var counts = new int[34];
        foreach (PaiCode tile in tiles)
            counts[tile.GetSerial()]++;
        return counts;
    }

    private static int[] BuildRemainingCounts(MajakGameLogic game, EnginePlayer player)
    {
        var remaining = Enumerable.Repeat(4, 34).ToArray();
        foreach (PaiCode tile in player.Tehai)
            remaining[tile.GetSerial()]--;

        foreach (EnginePlayer visiblePlayer in game.Player)
        {
            foreach (PaiCode tile in visiblePlayer.Sutehai)
                remaining[tile.GetSerial()]--;

            foreach (FuroBlock furo in visiblePlayer.Furo)
            {
                int firstVisibleTile = furo.Act == Act.Ank ? 0 : 1;
                for (int index = firstVisibleTile; index < furo.Tiles.Count; index++)
                    remaining[furo.Tiles[index].GetSerial()]--;
            }
        }

        for (int serial = 0; serial < remaining.Length; serial++)
            remaining[serial] = Math.Max(0, remaining[serial]);
        return remaining;
    }

    private static UkeireResult CalculateUkeire(
        int[] hand,
        int[] remaining,
        int openMeldCount,
        int currentShanten)
    {
        int liveTileCount = 0;
        double waitQuality = 0;
        var improvingTiles = new List<int>();
        for (int drawSerial = 0; drawSerial < 34; drawSerial++)
        {
            if (remaining[drawSerial] <= 0 || hand[drawSerial] >= 4) continue;
            hand[drawSerial]++;
            int nextShanten = CalculateShanten(hand, openMeldCount);
            hand[drawSerial]--;
            if (nextShanten >= currentShanten) continue;

            improvingTiles.Add(drawSerial);
            liveTileCount += remaining[drawSerial];
            waitQuality += remaining[drawSerial] * GetWaitQuality(hand, drawSerial);
        }
        return new UkeireResult(liveTileCount, waitQuality, improvingTiles);
    }

    private static double GetWaitQuality(int[] hand, int drawSerial)
    {
        if (drawSerial >= 27) return hand[drawSerial] > 0 ? 0.8 : 0.45;
        int number = drawSerial % 9;
        bool left = number > 0 && hand[drawSerial - 1] > 0;
        bool right = number < 8 && hand[drawSerial + 1] > 0;
        bool outerLeft = number > 1 && hand[drawSerial - 2] > 0;
        bool outerRight = number < 7 && hand[drawSerial + 2] > 0;
        if ((left && outerRight) || (right && outerLeft)) return 1.25;
        if (left && right) return 1.0;
        if (left || right || outerLeft || outerRight) return 0.75;
        return hand[drawSerial] > 0 ? 0.6 : 0.25;
    }

    private static double CalculateValueLoss(
        MajakGameLogic game,
        IEnumerable<PaiCode> physicalTiles,
        int serial)
    {
        double loss = physicalTiles.All(tile => tile.IsRed) ? 2.5 : 0;
        PaiCode tile = PaiCode.MakeSerial(serial);
        foreach (PaiCode indicator in game.KyokuInfo.Dora)
        {
            if (indicator.IsValid && tile == indicator.GetNextNumberPai())
                loss += 2;
        }
        if (tile.IsSangenpai) loss += 0.35;
        if (tile.IsFonpai) loss += 0.2;
        return loss;
    }

    private static double CalculateDanger(
        MajakGameLogic game,
        int engineOrder,
        int serial,
        int[] remaining)
    {
        double totalDanger = 0;
        foreach (EnginePlayer opponent in game.Player)
        {
            if (opponent.Order == engineOrder || opponent.RichiType == RichiType.None) continue;
            if (opponent.Sutehai.Any(tile => tile.GetSerial() == serial)) continue;

            if (serial >= 27)
            {
                int visible = 4 - remaining[serial];
                totalDanger += visible switch { >= 3 => 0.15, 2 => 0.8, 1 => 1.8, _ => 2.8 };
                continue;
            }

            int number = serial % 9;
            bool suji = opponent.Sutehai.Any(tile =>
            {
                int discarded = tile.GetSerial();
                if (discarded >= 27 || discarded / 9 != serial / 9) return false;
                int discardedNumber = discarded % 9;
                return Math.Abs(discardedNumber - number) == 3;
            });
            bool terminal = number is 0 or 8;
            totalDanger += suji ? (terminal ? 0.7 : 1.2) : (terminal ? 2.2 : 3.5);
        }
        return totalDanger;
    }

    private static bool ShouldDeclareRiichi(
        MajakGameLogic game,
        EnginePlayer player,
        int engineOrder,
        Candidate candidate,
        int[] originalHand,
        int[] remaining)
    {
        if (!player.IsMenzen
            || player.RichiType != RichiType.None
            || player.GamePoint < 1000
            || game.GetBipaiCount() < MajakConst.PlayerMaxCount)
            return false;

        int[] afterDiscard = (int[])originalHand.Clone();
        afterDiscard[candidate.Serial]--;
        double damaValue = 0;
        double riichiValue = 0;
        int liveWaits = 0;
        foreach (int waitSerial in candidate.Ukeire.ImprovingTiles)
        {
            if (remaining[waitSerial] <= 0) continue;
            afterDiscard[waitSerial]++;
            var points = game.EvaluateTrainingAiHoraPoints(
                engineOrder,
                afterDiscard,
                PaiCode.MakeSerial(waitSerial));
            afterDiscard[waitSerial]--;
            int live = remaining[waitSerial];
            liveWaits += live;
            damaValue += Math.Max(points.Tsumo, points.Ron) * live;
            riichiValue += Math.Max(points.RiichiTsumo, points.RiichiRon) * live;
        }

        if (liveWaits == 0) return false;
        bool opponentRiichi = game.Player.Any(opponent =>
            opponent.Order != engineOrder && opponent.RichiType != RichiType.None);
        double requiredGain = opponentRiichi ? 1.15 : 1.02;
        return riichiValue > damaValue * requiredGain;
    }

    private static int CalculateShanten(int[] counts, int openMeldCount)
    {
        int normal = CalculateNormalShanten(counts, openMeldCount);
        if (openMeldCount != 0) return normal;
        return Math.Min(normal, Math.Min(CalculateChiitoitsuShanten(counts), CalculateKokushiShanten(counts)));
    }

    private static int CalculateNormalShanten(int[] counts, int openMeldCount)
    {
        int minimum = 8;
        SearchNormal(counts, 0, openMeldCount, 0, 0, ref minimum);
        return minimum;
    }

    private static void SearchNormal(
        int[] counts,
        int index,
        int mentsu,
        int taatsu,
        int pair,
        ref int minimum)
    {
        while (index < 34 && counts[index] == 0) index++;
        if (index >= 34)
        {
            int cappedTaatsu = Math.Min(taatsu, 4 - mentsu);
            minimum = Math.Min(minimum, 8 - mentsu * 2 - cappedTaatsu - pair);
            return;
        }

        if (counts[index] >= 3)
        {
            counts[index] -= 3;
            SearchNormal(counts, index, mentsu + 1, taatsu, pair, ref minimum);
            counts[index] += 3;
        }
        if (index < 27 && index % 9 <= 6 && counts[index + 1] > 0 && counts[index + 2] > 0)
        {
            counts[index]--; counts[index + 1]--; counts[index + 2]--;
            SearchNormal(counts, index, mentsu + 1, taatsu, pair, ref minimum);
            counts[index]++; counts[index + 1]++; counts[index + 2]++;
        }
        if (pair == 0 && counts[index] >= 2)
        {
            counts[index] -= 2;
            SearchNormal(counts, index, mentsu, taatsu, 1, ref minimum);
            counts[index] += 2;
        }
        if (counts[index] >= 2)
        {
            counts[index] -= 2;
            SearchNormal(counts, index, mentsu, taatsu + 1, pair, ref minimum);
            counts[index] += 2;
        }
        if (index < 27 && index % 9 <= 7 && counts[index + 1] > 0)
        {
            counts[index]--; counts[index + 1]--;
            SearchNormal(counts, index, mentsu, taatsu + 1, pair, ref minimum);
            counts[index]++; counts[index + 1]++;
        }
        if (index < 27 && index % 9 <= 6 && counts[index + 2] > 0)
        {
            counts[index]--; counts[index + 2]--;
            SearchNormal(counts, index, mentsu, taatsu + 1, pair, ref minimum);
            counts[index]++; counts[index + 2]++;
        }

        counts[index]--;
        SearchNormal(counts, index, mentsu, taatsu, pair, ref minimum);
        counts[index]++;
    }

    private static int CalculateChiitoitsuShanten(int[] counts)
    {
        int pairs = counts.Count(count => count >= 2);
        int distinct = counts.Count(count => count > 0);
        return 6 - pairs + Math.Max(0, 7 - distinct);
    }

    private static int CalculateKokushiShanten(int[] counts)
    {
        int[] terminalsAndHonors = { 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33 };
        int distinct = terminalsAndHonors.Count(serial => counts[serial] > 0);
        int pair = terminalsAndHonors.Any(serial => counts[serial] > 1) ? 1 : 0;
        return 13 - distinct - pair;
    }

    private readonly record struct Candidate(
        int Serial,
        int BipaiIndex,
        int Shanten,
        UkeireResult Ukeire,
        double Score);

    private readonly record struct UkeireResult(
        int LiveTileCount,
        double WaitQuality,
        IReadOnlyList<int> ImprovingTiles);
}
