namespace MajakServer.Engine;

public enum TrainingAiLevel
{
    Legacy,
    Advanced,
}

public readonly record struct TrainingAiDecision(
    int DiscardSerial,
    bool ShouldRiichi,
    int? DiscardBipaiIndex = null);

public interface ITrainingAiEvaluator
{
    TrainingAiDecision Evaluate(MajakGameLogic game, int engineOrder, int aiType = 0);
}

/// <summary>
/// Pure port of the legacy training dummy CEval used by CMJTblUser::ComTurn.
/// </summary>
public sealed class LegacyTrainingAiEvaluator : ITrainingAiEvaluator
{
    public const double MNTVAL = 64.0;
    public const double PAIVAL = 0.008;
    public const double JNTVAL = 0.80;

    public TrainingAiDecision Evaluate(MajakGameLogic game, int engineOrder, int aiType = 0)
    {
        ArgumentNullException.ThrowIfNull(game);
        if ((uint)engineOrder >= MajakConst.PlayerMaxCount)
            throw new ArgumentOutOfRangeException(nameof(engineOrder));
        if ((uint)aiType >= MajakConst.PlayerMaxCount)
            throw new ArgumentOutOfRangeException(nameof(aiType));

        EnginePlayer player = game.Player[engineOrder];
        if (player.Tehai.Count == 0)
            throw new InvalidOperationException("Training AI cannot evaluate an empty hand.");

        var have = new int[34];
        var rest = Enumerable.Repeat(4, 34).ToArray();
        var disc = new int[34];

        foreach (PaiCode tile in player.Tehai)
        {
            int serial = tile.GetSerial();
            have[serial]++;
            rest[serial]--;
        }

        foreach (EnginePlayer visiblePlayer in game.Player)
        {
            foreach (FuroBlock furo in visiblePlayer.Furo)
            {
                // Open furo stores the called discard at index 0. That tile is
                // already present in Sutehai, matching ComTurn's ptr=1 policy.
                int firstVisibleTile = furo.Act == Act.Ank ? 0 : 1;
                for (int index = firstVisibleTile; index < furo.Tiles.Count; index++)
                    rest[furo.Tiles[index].GetSerial()]--;
            }

            foreach (PaiCode tile in visiblePlayer.Sutehai)
                rest[tile.GetSerial()]--;
        }

        foreach (PaiCode tile in player.Sutehai)
            disc[tile.GetSerial()]++;

        var evaluator = new LegacyEvaluator(
            aiType,
            game,
            engineOrder,
            have,
            rest,
            need: null,
            disc,
            player.Tehai.Count,
            player.Tehai[^1]);

        int discardSerial = evaluator.Evaluate();
        return new TrainingAiDecision(discardSerial, evaluator.ShouldRiichi);
    }

    private sealed class LegacyEvaluator
    {
        private readonly record struct AiParam(
            double TenronProbability,
            double RiichiRonProbability,
            double ChangeProbability,
            double Randomize);

        private static readonly AiParam[] AiParameters =
        {
            new(3.000, 0.500, 0.059, 0.000),
            new(3.000, 0.500, 0.059, 0.000),
            new(3.000, 0.500, 0.059, 0.000),
            new(3.000, 0.500, 0.059, 0.000),
        };

        private readonly MajakGameLogic _game;
        private readonly int _engineOrder;
        private readonly int _type;
        private readonly int[] _have;
        private readonly int[] _rest;
        private readonly int[] _disc;
        private readonly int[] _need = new int[34];
        private readonly PaiCode _pai;
        private readonly double[] _tapValue = new double[34];
        private readonly double[,] _mentsuValue = new double[4, 5];
        private readonly double[,] _jantsuValue = new double[4, 5];
        private readonly int[] _pieceCount = new int[4];
        private readonly int[] _mentsuCount = new int[4];
        private readonly int[] _jantsuCount = new int[4];

        private int _maxSerial;
        private int _pieceTotal;
        private int _level;
        private int _sub;
        private int _men;
        private int _jnt;
        private double _tenTsumo;
        private double _tenRon;
        private double _riichiTsumo;
        private double _riichiRon;

        public bool ShouldRiichi { get; private set; }

        public LegacyEvaluator(
            int type,
            MajakGameLogic game,
            int engineOrder,
            int[] have,
            int[] rest,
            int[]? need,
            int[] disc,
            int pieceTotal,
            PaiCode pai)
        {
            _type = type;
            _game = game;
            _engineOrder = engineOrder;
            _have = have;
            _rest = rest;
            _disc = disc;
            _pieceTotal = pieceTotal;
            _pai = pai;
            _men = (14 - pieceTotal) / 3;

            if (need != null)
                Array.Copy(need, _need, _need.Length);

            int remainingPieces = pieceTotal;
            for (int serial = 0; remainingPieces > 0; serial++)
            {
                if (_have[serial] == 0) continue;
                remainingPieces -= _have[serial];
                _pieceCount[serial / 9] += _have[serial];
            }
        }

        public int Evaluate()
        {
            ShouldRiichi = false;
            int shanten = GetShanten();
            if (shanten > 6)
            {
                _maxSerial = -1;
                GetValSim(shanten < 9 ? shanten : 8);
            }
            else
            {
                Array.Clear(_tapValue);
                GetValMen();
                double maxValue = 0;
                for (int serial = 0; serial < 34; serial++)
                {
                    _tapValue[serial] = ApplyRandomization(_tapValue[serial]);
                    if (_tapValue[serial] > maxValue)
                    {
                        maxValue = _tapValue[serial];
                        _maxSerial = serial;
                    }
                }
            }

            if (_maxSerial < 0)
                throw new InvalidOperationException("Legacy CEval found no discard candidate.");
            return _maxSerial;
        }

        private double ApplyRandomization(double value)
        {
            double randomize = AiParameters[_type].Randomize;
            if (randomize == 0) return value;
            return value * (1.0 + Random.Shared.Next(256) * randomize / 256.0);
        }

        private int GetMntCnt(int top, int pieceCount)
        {
            if (pieceCount == 0) return 0;
            while (_have[top] == 0) top++;

            int max = _need[top] != 0 ? -9999 : 0;
            if (top < 27 && top % 9 < 7 && _have[top + 1] != 0 && _have[top + 2] != 0)
            {
                _have[top]--;
                _have[top + 1]--;
                _have[top + 2]--;
                int value = GetMntCnt(top, pieceCount - 3);
                if (value >= max) max = value + 1;
                _have[top]++;
                _have[top + 1]++;
                _have[top + 2]++;
            }

            if (_have[top] >= 3)
            {
                int saved = _have[top];
                int value = GetMntCnt(top + 1, pieceCount - saved);
                if (value >= max) max = value + 1;
            }

            if (_need[top] == 0 && top < 33)
            {
                int value = GetMntCnt(top + 1, pieceCount - _have[top]);
                if (value > max) max = value;
            }
            return max;
        }

        private bool ChkMntCnt(int mentsuCount)
        {
            for (int suit = 0; suit < 4; suit++)
            {
                int value = GetMntCnt(9 * suit, _pieceCount[suit]);
                if (value < 0) return false;
                mentsuCount -= value;
            }
            return mentsuCount <= 0;
        }

        private bool ChkMntCnt(int mentsuCount, int top, int pieceCount)
        {
            if (mentsuCount == 0) return true;
            if (mentsuCount * 3 > pieceCount) return false;
            int value = ChkMntCntSub(mentsuCount, top, pieceCount);
            return value >= mentsuCount;
        }

        private int ChkMntCntSub(int mentsuCount, int top, int pieceCount)
        {
            if (pieceCount == 0) return 0;
            while (_have[top] == 0) top++;

            int max = _need[top] != 0 ? -9999 : 0;
            if (top < 27 && top % 9 < 7 && _have[top + 1] != 0 && _have[top + 2] != 0)
            {
                _have[top]--;
                _have[top + 1]--;
                _have[top + 2]--;
                int value = ChkMntCntSub(mentsuCount - 1, top, pieceCount - 3);
                if (value >= max) max = value + 1;
                _have[top]++;
                _have[top + 1]++;
                _have[top + 2]++;
            }

            if (_have[top] >= 3)
            {
                int saved = _have[top];
                int value = ChkMntCntSub(mentsuCount - 1, top + 1, pieceCount - saved);
                if (value >= max) max = value + 1;
            }

            if (_need[top] == 0 && top < 33)
            {
                int nextPieceCount = pieceCount - _have[top];
                if (nextPieceCount >= mentsuCount * 3)
                {
                    int value = ChkMntCntSub(mentsuCount, top + 1, nextPieceCount);
                    if (value > max) max = value;
                }
            }
            return max;
        }

        private int GetJntCnt(int top, int pieceCount)
        {
            if (pieceCount < 2) return -9999;
            while (_have[top] == 0) top++;

            int max = -9999;
            if (top < 27 && top % 9 < 7 && _have[top + 1] != 0 && _have[top + 2] != 0)
            {
                _have[top]--;
                _have[top + 1]--;
                _have[top + 2]--;
                int value = GetJntCnt(top, pieceCount - 3);
                if (value >= max) max = value + 1;
                _have[top]++;
                _have[top + 1]++;
                _have[top + 2]++;
            }

            if (_have[top] >= 3)
            {
                int saved = _have[top];
                int value = GetJntCnt(top + 1, pieceCount - saved);
                if (value >= max) max = value + 1;
            }

            if (_have[top] >= 2)
            {
                int saved = _have[top];
                int value = GetMntCnt(top + 1, pieceCount - saved);
                if (value > max) max = value;
            }

            if (_need[top] == 0 && top < 33)
            {
                int value = GetJntCnt(top + 1, pieceCount - _have[top]);
                if (value > max) max = value;
            }
            return max;
        }

        private bool ChkJntCnt(int jantsuCount, int top, int pieceCount)
        {
            if (jantsuCount < 0) return true;
            if (jantsuCount * 3 + 2 > pieceCount) return false;

            int end = top < 27 ? top / 9 * 9 + 9 : 34;
            bool hasPair = false;
            for (int serial = top; serial < end; serial++)
            {
                if (_have[serial] < 2) continue;
                hasPair = true;
                break;
            }
            if (!hasPair) return false;

            int value = ChkJntCntSub(jantsuCount, top, pieceCount);
            return value >= jantsuCount;
        }

        private int ChkJntCntSub(int jantsuCount, int top, int pieceCount)
        {
            if (pieceCount < 2) return -9999;
            while (_have[top] == 0) top++;

            int max = -9999;
            if (top < 27 && top % 9 < 7 && _have[top + 1] != 0 && _have[top + 2] != 0)
            {
                _have[top]--;
                _have[top + 1]--;
                _have[top + 2]--;
                int value = ChkJntCntSub(jantsuCount - 1, top, pieceCount - 3);
                if (value >= max) max = value + 1;
                _have[top]++;
                _have[top + 1]++;
                _have[top + 2]++;
            }

            if (_have[top] >= 3)
            {
                int saved = _have[top];
                int value = ChkJntCntSub(jantsuCount - 1, top + 1, pieceCount - saved);
                if (value >= max) max = value + 1;
            }

            if (_have[top] >= 2)
            {
                int saved = _have[top];
                int value = ChkMntCntSub(jantsuCount, top + 1, pieceCount - saved);
                if (value > max) max = value;
            }

            if (_need[top] == 0 && top < 33)
            {
                int nextPieceCount = pieceCount - _have[top];
                if (nextPieceCount >= jantsuCount * 3 + 2)
                {
                    int value = ChkJntCntSub(jantsuCount, top + 1, nextPieceCount);
                    if (value > max) max = value;
                }
            }
            return max;
        }

        private int GetShanten(int top, int count, int shanten)
        {
            if (count < 2) return shanten;
            while (_have[top] == 0) top++;

            int max = shanten;
            bool completeMentsuFound = false;
            if (top < 27)
            {
                if (top % 9 < 7)
                {
                    if (_have[top + 1] != 0 && _have[top + 2] != 0)
                    {
                        if (_men >= 4) return 0;
                        _have[top]--;
                        _have[top + 1]--;
                        _have[top + 2]--;
                        _men++;
                        int value = GetShanten(top, count - 3, shanten + 2);
                        if (value > max) max = value;
                        _men--;
                        _have[top]++;
                        _have[top + 1]++;
                        _have[top + 2]++;
                        completeMentsuFound = true;
                    }

                    if (!completeMentsuFound && _have[top + 1] != 0 && _rest[top + 2] != 0 && _men < 4)
                    {
                        _have[top]--;
                        _have[top + 1]--;
                        _rest[top + 2]--;
                        _men++;
                        int value = GetShanten(top, count - 2, shanten + 1);
                        if (value > max) max = value;
                        _men--;
                        _have[top]++;
                        _have[top + 1]++;
                        _rest[top + 2]++;
                    }

                    if (!completeMentsuFound && _rest[top + 1] != 0 && _have[top + 2] != 0 && _men < 4)
                    {
                        _have[top]--;
                        _rest[top + 1]--;
                        _have[top + 2]--;
                        _men++;
                        int value = GetShanten(top, count - 2, shanten + 1);
                        if (value > max) max = value;
                        _men--;
                        _have[top]++;
                        _rest[top + 1]++;
                        _have[top + 2]++;
                    }
                }

                if (!completeMentsuFound && top % 9 > 0 && top % 9 < 8
                    && _rest[top - 1] != 0 && _have[top + 1] != 0 && _men < 4)
                {
                    _rest[top - 1]--;
                    _have[top]--;
                    _have[top + 1]--;
                    _men++;
                    int value = GetShanten(top, count - 2, shanten + 1);
                    if (value > max) max = value;
                    _men--;
                    _rest[top - 1]++;
                    _have[top]++;
                    _have[top + 1]++;
                }
            }

            if (_have[top] >= 3)
            {
                if (_men >= 4) return 0;
                _men++;
                int value = GetShanten(top + 1, count - _have[top], shanten + 2);
                if (value > max) max = value;
                _men--;
                return max;
            }

            if (_have[top] >= 2)
            {
                if (_jnt < 1)
                {
                    _jnt++;
                    int value = GetShanten(top + 1, count - _have[top], shanten + 1);
                    if (value > max) max = value;
                    _jnt--;
                }

                if (_rest[top] != 0 && _men < 4)
                {
                    _rest[top]--;
                    _men++;
                    int value = GetShanten(top + 1, count - _have[top], shanten + 1);
                    if (value > max) max = value;
                    _men--;
                    _rest[top]++;
                }
            }

            if (_need[top] == 0 && top < 33)
            {
                int saved = _rest[top];
                _rest[top] = 0;
                int value = GetShanten(top + 1, count - _have[top], shanten);
                if (value > max) max = value;
                _rest[top] = saved;
            }

            if (_jnt == 0 && _need[top] != 0 && _have[top] == 1)
            {
                _have[top] = 0;
                _jnt++;
                int value = GetShanten(top + 1, count - 1, shanten);
                _jnt--;
                if (value > max) max = value;
                _have[top] = 1;
            }
            return max;
        }

        private bool ChkShanten(int top, int count, int shanten)
        {
            if (count < (shanten * 3 + 1) / 2) return false;
            while (_have[top] == 0) top++;

            bool completeMentsuFound = false;
            if (top < 27)
            {
                if (top % 9 < 7)
                {
                    if (_have[top + 1] != 0 && _have[top + 2] != 0)
                    {
                        if (_men < 4)
                        {
                            if (shanten <= 2) return true;
                            _have[top]--;
                            _have[top + 1]--;
                            _have[top + 2]--;
                            _men++;
                            bool result = ChkShanten(top, count - 3, shanten - 2);
                            _men--;
                            _have[top]++;
                            _have[top + 1]++;
                            _have[top + 2]++;
                            if (result) return true;
                        }
                        else
                        {
                            return shanten <= 1;
                        }
                        completeMentsuFound = true;
                    }

                    if (!completeMentsuFound && _have[top + 1] != 0 && _rest[top + 2] != 0 && _men < 4)
                    {
                        if (shanten <= 1) return true;
                        _have[top]--;
                        _have[top + 1]--;
                        _rest[top + 2]--;
                        _men++;
                        bool result = ChkShanten(top, count - 2, shanten - 1);
                        _men--;
                        _have[top]++;
                        _have[top + 1]++;
                        _rest[top + 2]++;
                        if (result) return true;
                    }

                    if (!completeMentsuFound && _rest[top + 1] != 0 && _have[top + 2] != 0 && _men < 4)
                    {
                        if (shanten <= 1) return true;
                        _have[top]--;
                        _rest[top + 1]--;
                        _have[top + 2]--;
                        _men++;
                        bool result = ChkShanten(top, count - 2, shanten - 1);
                        _men--;
                        _have[top]++;
                        _rest[top + 1]++;
                        _have[top + 2]++;
                        if (result) return true;
                    }
                }

                if (!completeMentsuFound && top % 9 > 0 && top % 9 < 8
                    && _rest[top - 1] != 0 && _have[top + 1] != 0 && _men < 4)
                {
                    if (shanten <= 1) return true;
                    _rest[top - 1]--;
                    _have[top]--;
                    _have[top + 1]--;
                    _men++;
                    bool result = ChkShanten(top, count - 2, shanten - 1);
                    _men--;
                    _rest[top - 1]++;
                    _have[top]++;
                    _have[top + 1]++;
                    if (result) return true;
                }
            }

            if (_have[top] >= 3)
            {
                if (_men < 4)
                {
                    if (shanten <= 2) return true;
                    _men++;
                    bool result = ChkShanten(top + 1, count - _have[top], shanten - 2);
                    _men--;
                    return result;
                }
                return shanten <= 1;
            }

            if (_have[top] >= 2)
            {
                if (_jnt < 1)
                {
                    if (shanten <= 1) return true;
                    _jnt++;
                    bool result = ChkShanten(top + 1, count - _have[top], shanten - 1);
                    _jnt--;
                    if (result) return true;
                }

                if (_rest[top] != 0 && _men < 4)
                {
                    if (shanten <= 1) return true;
                    _rest[top]--;
                    _men++;
                    bool result = ChkShanten(top + 1, count - _have[top], shanten - 1);
                    _men--;
                    _rest[top]++;
                    if (result) return true;
                }
            }

            if (_need[top] == 0 && top < 33)
            {
                int saved = _rest[top];
                _rest[top] = 0;
                bool result = ChkShanten(top + 1, count - _have[top], shanten);
                _rest[top] = saved;
                if (result) return true;
            }

            if (_jnt == 0 && _need[top] != 0 && _have[top] == 1)
            {
                _have[top] = 0;
                _jnt++;
                bool result = ChkShanten(top + 1, count - 1, shanten);
                _jnt--;
                _have[top] = 1;
                if (result) return true;
            }
            return false;
        }

        private int GetShanten()
        {
            _jnt = 0;
            int[] saved = (int[])_rest.Clone();
            for (int serial = 0; serial < 34; serial++)
                _rest[serial] = 4 - _have[serial];
            int shanten = GetShanten(0, _pieceTotal, _men * 2);
            Array.Copy(saved, _rest, 34);
            return shanten;
        }

        private bool ChkShanten(int shanten)
        {
            shanten -= _men * 2;
            if (shanten == 0) return true;
            _jnt = 0;
            int[] saved = (int[])_rest.Clone();
            for (int serial = 0; serial < 34; serial++)
                _rest[serial] = 4 - _have[serial];
            bool result = ChkShanten(0, _pieceTotal, shanten);
            Array.Copy(saved, _rest, 34);
            return result;
        }

        private double GetValSim(int shanten)
        {
            if (shanten == 9)
            {
                var points = _game.EvaluateTrainingAiHoraPoints(_engineOrder, _have, _pai);
                _tenTsumo = points.Tsumo;
                _tenRon = points.Ron;
                _riichiTsumo = points.RiichiTsumo;
                _riichiRon = points.RiichiRon;
                return 1;
            }

            if (_level == 2 || (_level == 1 && shanten < 5)) return 1;

            int maxSerial = -1;
            double maxValue = -1;
            for (int discardSerial = 0; discardSerial < 34; discardSerial++)
            {
                if (_have[discardSerial] == 0 || _need[discardSerial] != 0) continue;

                _disc[discardSerial]++;
                _have[discardSerial]--;
                _pieceTotal--;
                if (ChkShanten(shanten))
                {
                    _tenTsumo = 0;
                    _tenRon = 0;
                    _riichiTsumo = 0;
                    _riichiRon = 0;
                    double value = 1.0 / (1 << 16);
                    bool furiten = false;

                    for (int drawSerial = 0; drawSerial < 34; drawSerial++)
                    {
                        if (drawSerial == discardSerial || _rest[drawSerial] == 0
                            || !IsRelevantDraw(drawSerial)) continue;

                        _have[drawSerial]++;
                        _rest[drawSerial]--;
                        _need[drawSerial]++;
                        _pieceTotal++;
                        if (ChkShanten(shanten + 1))
                        {
                            if (_disc[drawSerial] != 0) furiten = true;
                            var child = new LegacyEvaluator(
                                _type,
                                _game,
                                _engineOrder,
                                _have,
                                _rest,
                                _need,
                                _disc,
                                _pieceTotal,
                                new PaiCode(drawSerial / 9, drawSerial % 9 + 1))
                            {
                                _sub = _sub,
                                _level = _level + 1,
                            };
                            double childValue = child.GetValSim(shanten + 1);
                            if (childValue > 0)
                            {
                                int remaining = _rest[drawSerial] + 1;
                                if (shanten == 8)
                                {
                                    _tenTsumo += child._tenTsumo * remaining;
                                    _tenRon += child._tenRon * remaining * AiParameters[_type].TenronProbability;
                                    _riichiTsumo += child._riichiTsumo * remaining;
                                    _riichiRon += child._riichiRon * remaining * AiParameters[_type].RiichiRonProbability;
                                }
                                else
                                {
                                    value += childValue * remaining;
                                }
                            }
                        }
                        _pieceTotal--;
                        _need[drawSerial]--;
                        _rest[drawSerial]++;
                        _have[drawSerial]--;
                    }

                    if (shanten >= 8 && !furiten)
                    {
                        _tenTsumo += _tenRon;
                        _riichiTsumo += _riichiRon;
                    }

                    if (_sub == 0 && _level == 0 && shanten >= 7)
                    {
                        double baseline = shanten >= 8
                            ? Math.Max(_tenTsumo, _riichiTsumo)
                            : value;
                        double changeValue = 0;
                        for (int drawSerial = 0; drawSerial < 34; drawSerial++)
                        {
                            if (drawSerial == discardSerial || _rest[drawSerial] == 0
                                || !IsRelevantDraw(drawSerial)) continue;

                            _have[drawSerial]++;
                            _rest[drawSerial]--;
                            _need[drawSerial]++;
                            _pieceTotal++;
                            if (!ChkShanten(shanten + 1) && ChkShanten(shanten))
                            {
                                var child = new LegacyEvaluator(
                                    _type,
                                    _game,
                                    _engineOrder,
                                    _have,
                                    _rest,
                                    _need,
                                    _disc,
                                    _pieceTotal,
                                    new PaiCode(drawSerial / 9, drawSerial % 9 + 1))
                                {
                                    _sub = 1,
                                };
                                double childValue = child.GetValSim(shanten);
                                if (shanten == 8)
                                    childValue = Math.Max(child._tenTsumo, child._riichiTsumo);
                                if (childValue > baseline)
                                    changeValue += (childValue - baseline) * (_rest[drawSerial] + 1);
                            }
                            _pieceTotal--;
                            _need[drawSerial]--;
                            _rest[drawSerial]++;
                            _have[drawSerial]--;
                        }

                        if (shanten >= 8)
                            _tenTsumo += changeValue * AiParameters[_type].ChangeProbability;
                        else
                            value += changeValue * AiParameters[_type].ChangeProbability;
                    }

                    bool riichi = false;
                    if (shanten >= 8)
                    {
                        if (_tenTsumo > _riichiTsumo)
                        {
                            value = _tenTsumo;
                        }
                        else
                        {
                            value = _riichiTsumo;
                            riichi = true;
                        }
                    }

                    value = ApplyRandomization(value);
                    if (value > maxValue)
                    {
                        maxValue = value;
                        maxSerial = discardSerial;
                        ShouldRiichi = riichi;
                    }
                }

                _pieceTotal++;
                _have[discardSerial]++;
                _disc[discardSerial]--;
            }

            _maxSerial = maxSerial;
            return maxValue;
        }

        private bool IsRelevantDraw(int serial)
        {
            if (_have[serial] != 0) return true;
            if (serial >= 27) return false;
            int number = serial % 9;
            if (number > 1 && (_have[serial - 2] != 0 || _have[serial - 1] != 0)) return true;
            if (number == 1 && _have[serial - 1] != 0) return true;
            if (number < 7 && (_have[serial + 2] != 0 || _have[serial + 1] != 0)) return true;
            return number == 7 && _have[serial + 1] != 0;
        }

        private double GetValPai(int mentsuCount, int targetCount, int top, int[] mentsuAccessible)
        {
            if (targetCount == 0) return 1;

            int end = top < 27 ? top / 9 * 9 + 9 : 34;
            double value = 0;
            for (int first = top; first < end; first++)
            {
                if (_rest[first] == 0 || mentsuAccessible[first] != 0
                    || (first >= 27 && _have[first] != 1) || !IsRelevantDraw(first)) continue;

                _pieceCount[first / 9] += 2;
                int firstRemaining = _rest[first];
                _rest[first]--;
                _have[first]++;
                _need[first]++;

                int second = first < 27 ? top / 9 * 9 : first;
                int secondEnd = first < 27 ? second + 9 : first + 1;
                double secondValue = 0;
                for (; second < secondEnd; second++)
                {
                    if (_rest[second] == 0 || mentsuAccessible[second] != 0
                        || (second >= 27 && _have[second] != 2)
                        || !IsAdjacentToHand(second)) continue;

                    _rest[second]--;
                    _have[second]++;
                    _need[second]++;
                    if (ChkMntCnt(_mentsuCount[first / 9] + 1, first / 9 * 9, _pieceCount[first / 9]))
                    {
                        if (_disc[second] != 0)
                        {
                            secondEnd = 0;
                            secondValue = 0;
                        }
                        else
                        {
                            _mentsuCount[first / 9]++;
                            double combinations = firstRemaining * (_rest[second] + 1);
                            secondValue += GetValPai(mentsuCount + 1, targetCount - 1, first, mentsuAccessible)
                                * combinations * PAIVAL;
                            _mentsuCount[first / 9]--;
                        }
                    }
                    _rest[second]++;
                    _have[second]--;
                    _need[second]--;
                }
                value += secondValue;
                _rest[first]++;
                _have[first]--;
                _need[first]--;
                _pieceCount[first / 9] -= 2;
            }
            return value;
        }

        private double GetValJntPai(int jantsuCount, int targetCount, int top, int[] jantsuAccessible)
        {
            if (targetCount == 0) return 1;

            int end = top < 27 ? top / 9 * 9 + 9 : 34;
            double value = 0;
            for (int first = top; first < end; first++)
            {
                if (_rest[first] == 0 || jantsuAccessible[first] != 0
                    || (first >= 27 && _have[first] != 1) || !IsRelevantDraw(first)) continue;

                _pieceCount[first / 9] += 2;
                int firstRemaining = _rest[first];
                _rest[first]--;
                _have[first]++;
                _need[first]++;

                int second = first < 27 ? top / 9 * 9 : first;
                int secondEnd = first < 27 ? second + 9 : first + 1;
                double secondValue = 0;
                for (; second < secondEnd; second++)
                {
                    if (_rest[second] == 0 || jantsuAccessible[second] != 0
                        || (second >= 27 && _have[second] != 2)
                        || !IsAdjacentToHand(second)) continue;

                    _rest[second]--;
                    _have[second]++;
                    _need[second]++;
                    if (ChkJntCnt(_jantsuCount[first / 9] + 1, first / 9 * 9, _pieceCount[first / 9]))
                    {
                        if (_disc[second] != 0)
                        {
                            secondEnd = 0;
                            secondValue = 0;
                        }
                        else
                        {
                            _jantsuCount[first / 9]++;
                            double combinations = firstRemaining * (_rest[second] + 1);
                            secondValue += GetValJntPai(jantsuCount + 1, targetCount - 1, first, jantsuAccessible)
                                * combinations * PAIVAL;
                            _jantsuCount[first / 9]--;
                        }
                    }
                    _rest[second]++;
                    _have[second]--;
                    _need[second]--;
                }
                value += secondValue;
                _rest[first]++;
                _have[first]--;
                _need[first]--;
                _pieceCount[first / 9] -= 2;
            }
            return value;
        }

        private bool IsAdjacentToHand(int serial)
        {
            if (_have[serial] != 0) return true;
            if (serial >= 27) return false;
            int number = serial % 9;
            return (number > 0 && _have[serial - 1] != 0)
                || (number < 8 && _have[serial + 1] != 0);
        }

        private double GetValTatSub(int mentsuCount, int targetCount, int top, int[] mentsuAccessible)
        {
            if (targetCount == 0) return 1;

            var saved = new int[34];
            double value = 0;
            int end = top < 27 ? top / 9 * 9 + 9 : 34;
            for (int serial = top; serial < end; serial++)
            {
                if (_disc[serial] != 0 || _rest[serial] == 0
                    || (serial >= 27 && _have[serial] != 2)
                    || !IsAdjacentToHand(serial)) continue;

                _pieceCount[serial / 9]++;
                _rest[serial]--;
                _have[serial]++;
                _need[serial]++;
                if (ChkMntCnt(_mentsuCount[serial / 9] + 1, serial / 9 * 9, _pieceCount[serial / 9]))
                {
                    _mentsuCount[serial / 9]++;
                    value += GetValTatSub(mentsuCount + 1, targetCount - 1, serial, mentsuAccessible)
                        * (_rest[serial] + 1);
                    _mentsuCount[serial / 9]--;
                    mentsuAccessible[serial]++;
                    saved[serial]++;
                }
                _need[serial]--;
                _have[serial]--;
                _rest[serial]++;
                _pieceCount[serial / 9]--;
            }

            value += GetValPai(mentsuCount, targetCount, top / 9 * 9, mentsuAccessible);
            for (int serial = 0; serial < 34; serial++)
                if (saved[serial] != 0) mentsuAccessible[serial]--;
            return value;
        }

        private double GetValJntSub(int jantsuCount, int targetCount, int top, int[] jantsuAccessible)
        {
            if (targetCount == 0) return 1;

            var saved = new int[34];
            double value = 0;
            int end = top < 27 ? top / 9 * 9 + 9 : 34;
            for (int serial = top; serial < end; serial++)
            {
                if (_disc[serial] != 0 || _rest[serial] == 0
                    || (serial >= 27 && _have[serial] != 2)
                    || !IsAdjacentToHand(serial)) continue;

                _pieceCount[serial / 9]++;
                _rest[serial]--;
                _have[serial]++;
                _need[serial]++;
                if (ChkJntCnt(jantsuCount + 1, serial / 9 * 9, _pieceCount[serial / 9]))
                {
                    _jantsuCount[serial / 9]++;
                    value += GetValJntSub(jantsuCount + 1, targetCount - 1, serial, jantsuAccessible)
                        * (_rest[serial] + 1);
                    _jantsuCount[serial / 9]--;
                    jantsuAccessible[serial]++;
                    saved[serial]++;
                }
                _need[serial]--;
                _have[serial]--;
                _rest[serial]++;
                _pieceCount[serial / 9]--;
            }

            value += GetValJntPai(jantsuCount, targetCount, top / 9 * 9, jantsuAccessible);
            for (int serial = 0; serial < 34; serial++)
                if (saved[serial] != 0) jantsuAccessible[serial]--;
            return value;
        }

        private double GetValMntMul(int targetCount, int top)
        {
            if (targetCount == 0) return 1;
            double sum = 0;
            for (int count = 0; count <= targetCount; count++)
            {
                double value = _mentsuValue[top, count];
                if (count < targetCount)
                {
                    if (top == 3) continue;
                    value *= GetValMntMul(targetCount - count, top + 1);
                }
                sum += value;
            }
            return sum;
        }

        private double GetValTatMul()
        {
            double sum = 0;
            var saved = new double[5];
            for (int suit = 0; suit < 4; suit++)
            {
                for (int count = 0; count <= 4; count++)
                {
                    saved[count] = _mentsuValue[suit, count];
                    _mentsuValue[suit, count] = _jantsuValue[suit, count];
                }
                sum += GetValMntMul(4 - _men, 0);
                for (int count = 0; count <= 4; count++)
                    _mentsuValue[suit, count] = saved[count];
            }
            return sum;
        }

        private double GetValMen()
        {
            double maxValue = 0;
            var mentsuAccessible = new int[34];
            var jantsuAccessible = new int[34];
            var mentsuMissing = new int[34];
            var jantsuMissing = new int[34];

            int totalMentsu = 0;
            for (int suit = 0; suit < 4; suit++)
            {
                _mentsuCount[suit] = GetMntCnt(9 * suit, _pieceCount[suit]);
                totalMentsu += _mentsuCount[suit];
                _jantsuCount[suit] = GetJntCnt(9 * suit, _pieceCount[suit]);
            }

            for (int serial = 0; serial < 34; serial++)
            {
                if (_rest[serial] == 0) continue;
                _pieceCount[serial / 9]++;
                _have[serial]++;
                _need[serial]++;
                if (ChkMntCnt(_mentsuCount[serial / 9] + 1, serial / 9 * 9, _pieceCount[serial / 9]))
                    mentsuAccessible[serial]++;
                if (ChkJntCnt(_jantsuCount[serial / 9] + 1, serial / 9 * 9, _pieceCount[serial / 9]))
                    jantsuAccessible[serial]++;
                _have[serial]--;
                _need[serial]--;
                _pieceCount[serial / 9]--;
            }

            for (int top = 0; top < 34; top += 9)
            {
                int suit = top / 9;
                _mentsuValue[suit, 0] = 1;
                int count = 1;
                for (; count <= _mentsuCount[suit]; count++)
                    _mentsuValue[suit, count] = Math.Pow(MNTVAL, count);
                for (; count <= 4; count++)
                    _mentsuValue[suit, count] = GetValTatSub(
                        _mentsuCount[suit], count - _mentsuCount[suit], top, mentsuAccessible)
                        * Math.Pow(MNTVAL, _mentsuCount[suit]);

                int jantsu = _jantsuCount[suit];
                count = 0;
                for (; count <= jantsu; count++)
                    _jantsuValue[suit, count] = Math.Pow(MNTVAL, count);
                if (jantsu < 0) jantsu = -1;
                for (; count <= 4; count++)
                    _jantsuValue[suit, count] = GetValJntSub(
                        jantsu, count - jantsu, top, jantsuAccessible)
                        * Math.Pow(MNTVAL, jantsu);
            }

            for (int discardSerial = 0; discardSerial < 34; discardSerial++)
            {
                if (_have[discardSerial] == 0) continue;

                int suit = discardSerial / 9;
                _pieceCount[suit]--;
                _disc[discardSerial]++;
                _have[discardSerial]--;

                totalMentsu -= _mentsuCount[suit];
                _mentsuCount[suit] = GetMntCnt(suit * 9, _pieceCount[suit]);
                totalMentsu += _mentsuCount[suit];
                _jantsuCount[suit] = GetJntCnt(suit * 9, _pieceCount[suit]);

                int start = discardSerial < 27 ? suit * 9 : discardSerial;
                int end = discardSerial < 27 ? start + 9 : start + 1;
                for (int serial = start; serial < end; serial++)
                {
                    if (mentsuAccessible[serial] != 0)
                    {
                        _pieceCount[serial / 9]++;
                        _have[serial]++;
                        _need[serial]++;
                        if (!ChkMntCnt(_mentsuCount[serial / 9] + 1, serial / 9 * 9, _pieceCount[serial / 9]))
                        {
                            mentsuMissing[serial]++;
                            mentsuAccessible[serial]--;
                        }
                        _have[serial]--;
                        _need[serial]--;
                        _pieceCount[serial / 9]--;
                    }

                    if (jantsuAccessible[serial] != 0)
                    {
                        _pieceCount[serial / 9]++;
                        _have[serial]++;
                        _need[serial]++;
                        if (!ChkJntCnt(_jantsuCount[serial / 9] + 1, serial / 9 * 9, _pieceCount[serial / 9]))
                        {
                            jantsuMissing[serial]++;
                            jantsuAccessible[serial]--;
                        }
                        _have[serial]--;
                        _need[serial]--;
                        _pieceCount[serial / 9]--;
                    }
                }

                var savedMentsuValues = new double[5];
                int count = 0;
                for (; count <= _mentsuCount[suit]; count++)
                {
                    savedMentsuValues[count] = _mentsuValue[suit, count];
                    _mentsuValue[suit, count] = Math.Pow(MNTVAL, count);
                }
                for (; count <= 4 - totalMentsu + _mentsuCount[suit]; count++)
                {
                    savedMentsuValues[count] = _mentsuValue[suit, count];
                    _mentsuValue[suit, count] = GetValTatSub(
                        _mentsuCount[suit], count - _mentsuCount[suit], suit * 9, mentsuAccessible)
                        * Math.Pow(MNTVAL, _mentsuCount[suit]);
                }

                int jantsu = _jantsuCount[suit];
                var savedJantsuValues = new double[5];
                count = 0;
                for (; count <= _jantsuCount[suit]; count++)
                {
                    savedJantsuValues[count] = _jantsuValue[suit, count];
                    _jantsuValue[suit, count] = Math.Pow(MNTVAL, count);
                }
                if (jantsu < 0) jantsu = 0;
                for (; count <= 4; count++)
                {
                    savedJantsuValues[count] = _jantsuValue[suit, count];
                    _jantsuValue[suit, count] = GetValJntSub(
                        jantsu, count - jantsu, suit * 9, jantsuAccessible)
                        * Math.Pow(MNTVAL, jantsu);
                }

                double value = GetValTatMul();
                _tapValue[discardSerial] += value;
                if (value > maxValue)
                {
                    maxValue = value;
                    _maxSerial = discardSerial;
                }

                for (count = 0; count <= 4 - totalMentsu + _mentsuCount[suit]; count++)
                    _mentsuValue[suit, count] = savedMentsuValues[count];
                for (count = 0; count <= 4; count++)
                    _jantsuValue[suit, count] = savedJantsuValues[count];

                for (int serial = 0; serial < 34; serial++)
                {
                    if (mentsuMissing[serial] != 0)
                    {
                        mentsuMissing[serial] = 0;
                        mentsuAccessible[serial]++;
                    }
                    if (jantsuMissing[serial] != 0)
                    {
                        jantsuMissing[serial] = 0;
                        jantsuAccessible[serial]++;
                    }
                }

                _have[discardSerial]++;
                _disc[discardSerial]--;
                _pieceCount[suit]++;
            }
            return maxValue;
        }
    }
}