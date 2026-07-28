namespace MajakServer.Engine;

/// <summary>
/// Yaku information for a completed hand — port of the YAKU class.
/// Tracks han count, mangan level, fu, and individual yaku list.
/// </summary>
public class Yaku
{
    public struct YakuInfo
    {
        public HoraYaku Name;
        public int      Han;
        public YakuInfo(HoraYaku n, int h) { Name = n; Han = h; }
    }

    public int              HanSum;
    public List<YakuInfo>   List = new();
    public int              Mangan;      // -1=none, 0=mangan(fu-based), 1=mangan, 2=haneman, 3=baiman, 4=sanbaiman, 5=kazoe/yakuman, 6=yakuman+
    public bool             IsYakumanFlag;
    public bool             Pinfu;
    public int              Fu;
    public int              Ten;         // basic point (before oya/ko multiplier)
    public int              Chip;
    public int[]            YakuhaiCnt = new int[7];  // per-honor yakuhai count
    public int[]            DoraCnt    = new int[4];  // [0]=dora, [1]=uradora, [2]=red, [3]=nukidora

    public void Clear()
    {
        HanSum = 0; Mangan = 0; IsYakumanFlag = false; Pinfu = false;
        Fu = 0; Ten = 0; Chip = 0;
        Array.Clear(YakuhaiCnt, 0, 7);
        Array.Clear(DoraCnt,    0, 4);
        List.Clear();
    }

    public bool IsYakuman => IsYakumanFlag;

    public void AddYakuman(HoraYaku yaku, int han)
    {
        IsYakumanFlag = true;
        List.Add(new YakuInfo(yaku, han));
        HanSum += han;
    }

    public void AddYaku(HoraYaku yaku, int han)
    {
        List.Add(new YakuInfo(yaku, han));
        HanSum += han;
    }

    /// <summary>Calculate Ten (basic points) from HanSum / Fu</summary>
    public void CalcHoraTen()
    {
        int[] manTbl = { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 3, 4, 4 };
        int[] tenTbl = { 0, 2000, 3000, 4000, 6000, 8000 };

        if (IsYakumanFlag)
        {
            Mangan = 6;
            Ten = 8000 * HanSum;
            return;
        }
        if (HanSum > 4)
        {
            Mangan = HanSum > 12 ? 5 : manTbl[HanSum];
            Ten = tenTbl[Mangan];
            return;
        }
        // Kiriage mangan
        if ((HanSum == 4 && Fu >= 30) || (HanSum == 3 && Fu >= 60))
        {
            Mangan = 0; Ten = 2000; return;
        }
        Ten = (Fu << HanSum) * 4;
        if (Ten > 2000) { Mangan = 0; Ten = 2000; return; }
        Mangan = -1;
    }

    /// <summary>Update this yaku if the candidate has higher point value</summary>
    public void CheckAndUpdate(Yaku other)
    {
        if (other.Ten > Ten || (other.Ten == Ten && other.HanSum > HanSum))
        {
            HanSum       = other.HanSum;
            List         = other.List;
            Mangan       = other.Mangan;
            IsYakumanFlag = other.IsYakumanFlag;
            Pinfu        = other.Pinfu;
            Fu           = other.Fu;
            Ten          = other.Ten;
            Chip         = other.Chip;
            YakuhaiCnt   = (int[])other.YakuhaiCnt.Clone();
            DoraCnt      = (int[])other.DoraCnt.Clone();
        }
    }
}
