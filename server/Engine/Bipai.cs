namespace MajakServer.Engine;

/// <summary>
/// Tile wall manager — port of CBipai.
/// Manages the 136-tile wall, dead-wall (wanpai), dora/uradora indicators,
/// shuffle, and per-tile visibility flags.
/// </summary>
public class Bipai
{
    private const int DbgYDaisangen = 1001;
    private const int DbgYSuuankou = 1002;
    private const int DbgYShosuushi = 1003;
    private const int DbgYChinroutou = 1004;
    private const int DbgYTsuisou = 1005;
    private const int DbgYRyuisou = 1006;
    private const int DbgYChurenpaotou = 1007;
    private const int DbgYKokushi = 1008;
    private const int DbgYDaisuushi = 1009;
    private const int DbgYSuuankou2 = 1012;
    private const int DbgYKokushi2 = 1013;
    private const int DbgYChurenpaotou2 = 1014;
    private const int DbgYSuukantsu = 1015;

    private readonly PaiCode[] _buf      = new PaiCode[136];
    private readonly int[]     _openFlags = new int[136];

    private int _openIdx;     // haipai start position
    private int _bipPtr;      // draw pointer (from live wall)
    private int _rinPtr;      // rinshan pointer (from dead wall)
    private int _rinRsv;      // reserved dead-wall size (4 + flower count)

    public Bipai() { _rinRsv = 4; }

    // ─── Initialisation ──────────────────────────────────────────────────────

    /// <param name="nRed">Number of red dora (0/1/2)</param>
    /// <param name="nHua">Number of flower tiles (0 for standard)</param>
    public void Init(int nRed, int nHua)
    {
        _rinRsv = 4 + nHua;
        for (int i = 0; i < 34; i++)
        {
            var t = new PaiCode(i / 9, 1 + i % 9);
            _buf[34 * 0 + i] = t;
            _buf[34 * 1 + i] = t;
            _buf[34 * 2 + i] = t;
            _buf[34 * 3 + i] = t;
        }
        // Red dora assignment
        switch (nRed)
        {
            case 2:
                _buf[34 * 0 + 9 * 0 + 4].IsRed = true;  // Man-5
                _buf[34 * 0 + 9 * 1 + 4].IsRed = true;  // Sou-5
                _buf[34 * 0 + 9 * 2 + 4].IsRed = true;  // Pin-5
                break;
            case 1:
                _buf[34 * 1 + 9 * 2 + 4].IsRed = true;  // one Pin-5
                _buf[34 * 0 + 9 * 0 + 4].IsRed = true;
                _buf[34 * 0 + 9 * 1 + 4].IsRed = true;
                _buf[34 * 0 + 9 * 2 + 4].IsRed = true;
                break;
        }
        for (int i = 0; i < 136; i++)
        {
            _openFlags[i] = 0;
            _buf[i].BipaiIndex = i;
        }
    }

    /// <summary>Shuffle the wall (Fisher-Yates)</summary>
    public void Chipai()
    {
        for (int i = 0; i < 135; i++)
        {
            int j = i + Random.Shared.Next(136 - i);
            (_buf[j], _buf[i]) = (_buf[i], _buf[j]);
        }
        _bipPtr = 0;
        _rinPtr = 0;
        for (int i = 0; i < 136; i++)
        {
            _openFlags[i] = 0;
            _buf[i].BipaiIndex = i;
        }
    }

    public void ChipaiYakuDebug(int haipaiPos, int haipaiYaku, int redCount, int huaCount, bool oyaOrder)
    {
        Init(redCount, huaCount);

        var oyaTargets = BuildDebugTargets(DebugOyaYakuIdx, DebugOyaHaipaiIdx, haipaiPos, haipaiYaku);
        var ko1Targets = BuildDebugTargets(DebugKo1YakuIdx, DebugKo1HaipaiIdx, haipaiPos, haipaiYaku);
        var appliedTargets = new List<HashSet<int>>();

        if (oyaOrder)
        {
            ApplyDebugTargets(oyaTargets, appliedTargets);
            ApplyDebugTargets(ko1Targets, appliedTargets);
        }
        else
        {
            ApplyDebugTargets(ko1Targets, appliedTargets);
            ApplyDebugTargets(oyaTargets, appliedTargets);
        }

        ApplyDebugTargets(BuildDebugTargets(DebugKo2YakuIdx, DebugKo2HaipaiIdx, haipaiPos, haipaiYaku), appliedTargets);
        ApplyDebugTargets(BuildDebugTargets(DebugKo3YakuIdx, DebugKo3HaipaiIdx, haipaiPos, haipaiYaku), appliedTargets);

        _bipPtr = 0;
        _rinPtr = 0;
        for (int i = 0; i < 136; i++)
        {
            _openFlags[i] = 0;
            _buf[i].BipaiIndex = i;
        }
    }

    // ─── External Access ─────────────────────────────────────────────────────

    public void GetBipai(PaiCode[] dst)
    {
        for (int i = 0; i < 136; i++) dst[i] = _buf[i];
    }

    public void SetBipai(PaiCode[] src, int haipaiPos)
    {
        int idx = haipaiPos;
        for (int i = 0; i < 136; i++)
        {
            _buf[idx] = src[i];
            _buf[idx].BipaiIndex = idx;
            _openFlags[idx] = 0;
            if (++idx == 136) idx = 0;
        }
        _bipPtr = 0;
        _rinPtr = 0;
    }

    public PaiCode GetPai(int bipaiIndex) => _buf[bipaiIndex];
    public int     GetBipaiCount() => 136 - MajakConst.WanpaiCount - _bipPtr - _rinPtr;

    public void SetOpenIdx(int idx) => _openIdx = idx;
    public int  GetOpenIdx()        => _openIdx;
    public int  GetBipPtr()         => (_openIdx + _bipPtr) % 136;
    public int  GetRinPtr()         => (_openIdx + 135 - (_rinPtr ^ 1)) % 136;

    public int GetDoraIdx(int n, bool ura)
        => (_openIdx + 136 - _rinRsv - 2 - n * 2 + (ura ? 1 : 0)) % 136;

    public PaiCode GetDoraDisplay(int n, bool ura) => _buf[GetDoraIdx(n, ura)];

    private SortedDictionary<int, int> BuildDebugTargets(int[][] table, int[] haipaiIdx, int haipaiPos, int yaku)
    {
        var row = table.FirstOrDefault(r => r[0] == yaku);
        var targets = new SortedDictionary<int, int>();
        if (row == null) return targets;

        for (int i = 0; i < haipaiIdx.Length; i++)
        {
            int sourceIndex = row[i + 1];
            if (sourceIndex < 0) continue;
            int targetIndex = (haipaiIdx[i] + haipaiPos) % 136;
            targets[targetIndex] = _buf[sourceIndex].Code;
        }
        return targets;
    }

    private void ApplyDebugTargets(SortedDictionary<int, int> targets, List<HashSet<int>> appliedTargets)
    {
        if (targets.Count == 0) return;
        var reserved = targets.Keys.ToHashSet();
        appliedTargets.Add(reserved);

        foreach (var (targetIndex, targetCode) in targets)
        {
            for (int i = 0; i < 136; i++)
            {
                if (_buf[i].Code != targetCode) continue;
                bool skip = false;
                foreach (var reservedSet in appliedTargets)
                {
                    if (reservedSet.Contains(i)) { skip = true; break; }
                }
                if (skip) continue;

                (_buf[i], _buf[targetIndex]) = (_buf[targetIndex], _buf[i]);
                break;
            }
        }
    }

    public void Open(int bipaiIndex)
    {
        int openMask = (1 << (MajakConst.PlayerMaxCount + 1)) - 1;
        _openFlags[bipaiIndex] = (_openFlags[bipaiIndex] & openMask) | openMask;
    }

    public void OpenAll() { for (int i = 0; i < 136; i++) Open(i); }

    public PaiCode GetNextTsumo(int memberOrder)
    {
        int idx = GetBipPtr();
        _bipPtr++;
        _openFlags[idx] |= 1 << memberOrder;
        return _buf[idx];
    }

    public PaiCode GetNextRinshan(int memberOrder)
    {
        int idx = GetRinPtr();
        _rinPtr++;
        _openFlags[idx] |= 1 << memberOrder;
        return _buf[idx];
    }

    /// <summary>
    /// Fill BipaiInfo for a player (open-mask = which seats can see each tile;
    /// skip-mask = tiles already sent).
    /// </summary>
    public void GetPaiInfo(ref BipaiInfo buf, int openMask, int skipMask)
    {
        int smask = skipMask << (MajakConst.PlayerMaxCount + 1);
        buf.PaiCnt = 0;
        for (int i = 0; i < 136; i++)
        {
            if ((smask & _openFlags[i]) != 0) continue;
            if ((openMask & _openFlags[i]) != 0)
            {
                buf.Pai[buf.PaiCnt++] = _buf[i];
                _openFlags[i] |= smask;
            }
        }
    }

    private static readonly int[] DebugOyaHaipaiIdx = { 0, 1, 2, 3, 16, 17, 18, 19, 32, 33, 34, 35, 48, 52 };
    private static readonly int[] DebugKo1HaipaiIdx = { 4, 5, 6, 7, 20, 21, 22, 23, 36, 37, 38, 39, 49 };
    private static readonly int[] DebugKo2HaipaiIdx = { 8, 9, 10, 11, 24, 25, 26, 27, 40, 41, 42, 43, 50 };
    private static readonly int[] DebugKo3HaipaiIdx = { 12, 13, 14, 15, 28, 29, 30, 31, 44, 45, 46, 47, 51 };

    private static readonly int[][] DebugOyaYakuIdx =
    {
        new[] { DbgYDaisangen, 0, 1, 2, 31, 65, 99, 32, 66, 100, 33, 67, 27, 61, 3 },
        new[] { DbgYSuuankou, 0, 34, 68, 1, 35, 69, 2, 36, 70, 3, 37, 4, 38, 71 },
        new[] { DbgYSuuankou2, 0, 34, 68, 1, 35, 69, 2, 36, 70, 3, 37, 71, 4, 38 },
        new[] { DbgYKokushi, 0, 8, 9, 17, 18, 26, 28, 29, 30, 31, 32, 33, 67, 1 },
        new[] { DbgYKokushi2, 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33, 1 },
        new[] { DbgYTsuisou, 27, 61, 95, 28, 62, 96, 29, 63, 97, 30, 64, 98, 31, 1 },
        new[] { DbgYShosuushi, 1, 2, 27, 61, 95, 28, 62, 96, 29, 63, 97, 30, 64, 4 },
        new[] { DbgYDaisuushi, 0, 27, 61, 95, 28, 62, 96, 29, 63, 97, 30, 64, 98, 1 },
        new[] { DbgYRyuisou, 10, 44, 11, 45, 12, 46, 14, 48, 16, 50, 84, 32, 66, 1 },
        new[] { DbgYChinroutou, 0, 34, 68, 8, 42, 76, 9, 43, 77, 17, 51, 85, 18, 1 },
        new[] { DbgYChurenpaotou, 0, 34, 68, 1, 2, 3, 4, 5, 6, 40, 8, 42, 76, 9 },
        new[] { DbgYChurenpaotou2, 0, 34, 68, 1, 2, 3, 4, 5, 6, 7, 8, 42, 76, 9 },
        new[] { DbgYSuukantsu, 0, 34, 68, 1, 35, 69, 2, 36, 70, 3, 37, 71, 105, 29 },
        new[] { 1016, 0, 1, 2, 3, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 },
        new[] { 1017, 0, 1, 2, 3, 4, 10, 11, 12, 13, 14, 15, 16, 17, 18 },
        new[] { 1018, 0, 1, 2, 3, 4, 5, 11, 12, 13, 14, 15, 16, 17, 18 },
        new[] { 1019, 0, 1, 2, 3, 4, 5, 6, 12, 13, 14, 15, 16, 17, 18 },
        new[] { 1020, 1, 2, 3, 4, 5, 6, 7, 12, 13, 14, 15, 16, 17, 18 },
        new[] { 1021, 2, 3, 4, 5, 6, 7, 8, 12, 13, 14, 15, 16, 17, 18 },
        new[] { 1022, 3, 4, 5, 6, 7, 8, 11, 12, 13, 14, 15, 16, 17, 18 },
        new[] { 1023, 4, 5, 6, 7, 8, 10, 11, 12, 13, 14, 15, 16, 17, 18 },
        new[] { 1024, 5, 6, 7, 8, 0, 10, 11, 12, 13, 14, 15, 16, 17, 18 },
        new[] { 1025, 0, 34, 68, 3, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 },
        new[] { 1026, 1, 2, 3, 4, 38, 10, 11, 12, 14, 15, 19, 20, 21, 63 },
        new[] { 1027, 0, 34, 68, 8, 42, 76, 10, 44, 12, 13, 33, 67, 101, 63 },
        new[] { 1028, 1, 2, 3, 37, 71, 10, 11, 12, 15, 16, 19, 20, 21, 63 },
        new[] { 1029, 0, 1, 2, 3, 4, 5, 9, 10, 11, 18, 52, 31, 65, 63 },
        new[] { 1030, 8, 42, 76, 9, 43, 10, 11, 45, 17, 51, 85, 19, 53, 63 },
        new[] { 1031, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58 },
        new[] { 1032, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58 },
        new[] { 1033, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58 },
        new[] { 1034, 45, 46, 47, 48, 49, 50, 51, 100, 101, 54, 55, 56, 57, 58 },
        new[] { 1035, 0, 46, 47, 48, 49, 50, 1, 52, 2, 54, 55, 56, 57, 58 },
        new[] { 1036, 1, 2, 3, 4, 36, 70, 37, 71, 38, 72, 15, 16, 17, 18 },
        new[] { 1037, 1, 2, 3, 4, 36, 70, 37, 71, 8, 42, 15, 16, 17, 18 },
        new[] { 1038, 0, 34, 1, 2, 27, 61, 28, 62, 29, 63, 15, 16, 17, 18 },
        new[] { 1039, 0, 34, 1, 2, 27, 61, 28, 62, 29, 63, 15, 16, 17, 18 },
        new[] { 1040, 0, 1, 35, 2, 27, 61, 28, 62, 29, 63, 15, 16, 17, 18 },
        new[] { 1041, 0, 1, 2, 36, 27, 61, 28, 62, 29, 63, 15, 16, 17, 18 },
        new[] { 1042, 0, 1, 2, 3, 27, 61, 28, 62, 29, 63, 15, 16, 17, 18 },
        new[] { 1043, 1, 2, 3, 37, 27, 61, 28, 62, 29, 63, 15, 16, 17, 18 },
        new[] { 1044, 1, 2, 3, 4, 27, 61, 28, 62, 29, 63, 15, 16, 17, 18 },
        new[] { 1045, 0, 1, 2, 3, 4, 5, 6, 7, 9, 10, 14, 17, 18, 48 },
    };

    private static readonly int[][] DebugKo1YakuIdx =
    {
        new[] { DbgYDaisangen, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 101 },
        new[] { DbgYKokushi, 10, 11, 12, 13, 14, 15, 16, 19, 20, 21, 22, 23, 24 },
        new[] { DbgYKokushi2, 10, 11, 12, 13, 14, 15, 16, 19, 20, 21, 22, 23, 27 },
        new[] { DbgYTsuisou, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 65 },
        new[] { DbgYShosuushi, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 0 },
        new[] { DbgYDaisuushi, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 34 },
        new[] { DbgYRyuisou, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 20, 21, 100 },
        new[] { DbgYChinroutou, 10, 11, 12, 13, 14, 15, 16, 27, 28, 19, 20, 21, 52 },
        new[] { DbgYChurenpaotou, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 7 },
        new[] { DbgYChurenpaotou2, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 35 },
        new[] { DbgYSuukantsu, 102, 103, 104, 13, 14, 15, 16, 17, 18, 19, 20, 21, 63 },
        new[] { 1016, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
        new[] { 1017, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
        new[] { 1018, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
        new[] { 1019, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
        new[] { 1020, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
        new[] { 1021, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
        new[] { 1022, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
        new[] { 1023, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
        new[] { 1024, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
        new[] { 1025, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
        new[] { 1026, 35, 36, 37, 13, 23, 24, 25, 26, 27, 28, 29, 30, 13 },
        new[] { 1027, 35, 36, 37, 13, 23, 24, 25, 26, 27, 28, 29, 30, 11 },
        new[] { 1029, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 99 },
        new[] { 1030, 0, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 44 },
        new[] { 1031, 1, 2, 3, 4, 38, 10, 11, 12, 14, 15, 19, 20, 21 },
        new[] { 1032, 0, 34, 68, 8, 42, 76, 10, 44, 12, 13, 33, 67, 101 },
        new[] { 1033, 1, 2, 3, 37, 71, 10, 11, 12, 15, 16, 19, 20, 21 },
        new[] { 1034, 0, 1, 2, 3, 4, 5, 9, 10, 11, 18, 52, 31, 65 },
        new[] { 1035, 8, 42, 76, 9, 43, 10, 11, 45, 17, 51, 85, 19, 53 },
        new[] { 1036, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 104, 105, 106 },
        new[] { 1037, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 104, 105, 110 },
        new[] { 1038, 19, 20, 21, 22, 23, 24, 25, 26, 5, 6, 95, 96, 97 },
        new[] { 1039, 19, 20, 21, 22, 23, 24, 25, 26, 5, 6, 95, 96, 97 },
        new[] { 1040, 19, 20, 21, 22, 23, 24, 25, 26, 5, 6, 95, 96, 97 },
        new[] { 1041, 19, 20, 21, 22, 23, 24, 25, 26, 5, 6, 95, 96, 97 },
        new[] { 1042, 19, 20, 21, 22, 23, 24, 25, 26, 5, 6, 95, 96, 97 },
        new[] { 1043, 19, 20, 21, 22, 23, 24, 25, 26, 5, 6, 95, 96, 97 },
        new[] { 1044, 19, 20, 21, 22, 23, 24, 25, 26, 5, 6, 95, 96, 97 },
        new[] { 1045, 11, 12, 13, 15, 16, 19, 21, 22, 23, 26, 35, 53, 69 },
    };

    private static readonly int[][] DebugKo2YakuIdx =
    {
        new[] { DbgYKokushi, 2, 3, 34, 35, 36, 37, 38, 39, 40, 27, 61, 95, 129 },
        new[] { 1016, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1017, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1018, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1019, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1020, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1021, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1022, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1023, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1024, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1025, 32, 33, 1, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1031, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 13 },
        new[] { 1032, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 11 },
        new[] { 1034, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 99 },
        new[] { 1035, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 3, 4, 44 },
        new[] { 1036, 32, 33, 34, 35, 5, 6, 7, 39, 40, 41, 42, 43, 44 },
        new[] { 1037, 32, 33, 34, 35, 5, 6, 7, 39, 40, 41, 9, 43, 44 },
        new[] { 1038, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1039, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1040, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1041, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1042, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1043, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1044, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 },
        new[] { 1045, 40, 41, 42, 43, 44, 45, 46, 47, 49, 50, 51, 82, 116 },
    };

    private static readonly int[][] DebugKo3YakuIdx =
    {
        new[] { 1016, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 102 },
        new[] { 1017, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 103 },
        new[] { 1018, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 104 },
        new[] { 1019, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 105 },
        new[] { 1020, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 106 },
        new[] { 1021, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 107 },
        new[] { 1022, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 108 },
        new[] { 1023, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 109 },
        new[] { 1024, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 110 },
        new[] { 1025, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 102 },
        new[] { 1036, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 0 },
        new[] { 1037, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 0 },
        new[] { 1038, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 102 },
        new[] { 1039, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 105 },
        new[] { 1040, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 103 },
        new[] { 1041, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 104 },
        new[] { 1042, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 102 },
        new[] { 1043, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 102 },
        new[] { 1044, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 106 },
    };
}
