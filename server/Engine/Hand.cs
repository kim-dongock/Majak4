namespace MajakServer.Engine;

/// <summary>
/// Mahjong hand evaluator — port of CHand.cpp/h.
/// Operates on a count-array (0-33 serial) plus furo blocks.
/// Stateless except for context fields set by CheckYaku / GetYaku.
/// </summary>
public class Hand
{
    private const int MentsuCount = 4;

    // Tile count array [34] — hand only (not furo)
    private readonly int[] _cnt = new int[34];
    private int _handCnt;

    // Furo block references
    private readonly int     _furoCnt;
    private readonly FuroBlock?[] _furo = new FuroBlock?[4];

    // Context for yaku evaluation
    private int  _paiHora;   // serial of winning tile
    private bool _isTsumo;
    private bool _isMenzen;
    private int  _chanfon;
    private int  _menfon;
    private bool _doubleYakuman; // 通常ゲームでダブル役満を適用する — 原典: bRevaluate (CHand.cpp)

    public Hand(EnginePlayer player)
    {
        _handCnt = player.Tehai.Count;
        _furoCnt = player.Furo.Count;
        foreach (var p in player.Tehai) _cnt[p.GetSerial()]++;
        for (int i = 0; i < player.Furo.Count; i++) _furo[i] = player.Furo[i];
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    public bool CheckTempai(int pai)
    {
        _cnt[pai]--; _handCnt--;
        bool r = CheckTempai();
        _cnt[pai]++; _handCnt++;
        return r;
    }

    public bool CheckTempai()
    {
        for (int i = 0; i < 34; i++)
            if (_cnt[i] < 4 && CheckHoraForm(i)) return true;
        return false;
    }

    public bool CheckHoraForm(int pai)
    {
        _cnt[pai]++; _handCnt++;
        bool h = CheckHoraForm();
        _cnt[pai]--; _handCnt--;
        return h;
    }

    public bool CheckHoraForm()
        => ChkKokushi() || ChkChitoi() || ChkHead(0, _handCnt);

    public bool CheckYaku(bool kuitan, bool tsumo, bool menzen, int chanfon, int menfon, int paiHora)
    {
        SetContext(tsumo, menzen, chanfon, menfon, paiHora);
        return ChkYaku(kuitan);
    }

    public void GetYaku(Yaku yaku, bool kuitan, bool tsumo, bool menzen, int chanfon, int menfon, int paiHora, bool doubleYakuman = true)
    {
        SetContext(tsumo, menzen, chanfon, menfon, paiHora, doubleYakuman);
        GetYakuInternal(yaku, kuitan);
    }

    /// <summary>
    /// Validate ankan doesn't break tenpai form after riichi.
    /// Returns true if ankan is valid.
    /// </summary>
    public bool CheckAnkan(int paiSerial)
    {
        _cnt[paiSerial] -= 2;
        for (int j = 0; j < 34; j++)
        {
            if (j == paiSerial || _cnt[j] >= 4) continue;
            _cnt[j]++;
            bool breaks = false;
            if (paiSerial % 9 >= 2 && _cnt[paiSerial - 2] > 0 && _cnt[paiSerial - 1] > 0)
            {
                _cnt[paiSerial - 2]--; _cnt[paiSerial - 1]--;
                if (ChkHead(0, _handCnt - 3)) breaks = true;
                _cnt[paiSerial - 2]++; _cnt[paiSerial - 1]++;
            }
            if (!breaks && paiSerial % 9 >= 1 && paiSerial % 9 <= 7 && _cnt[paiSerial - 1] > 0 && _cnt[paiSerial + 1] > 0)
            {
                _cnt[paiSerial - 1]--; _cnt[paiSerial + 1]--;
                if (ChkHead(0, _handCnt - 3)) breaks = true;
                _cnt[paiSerial - 1]++; _cnt[paiSerial + 1]++;
            }
            if (!breaks && paiSerial % 9 <= 6 && _cnt[paiSerial + 1] > 0 && _cnt[paiSerial + 2] > 0)
            {
                _cnt[paiSerial + 1]--; _cnt[paiSerial + 2]--;
                if (ChkHead(0, _handCnt - 3)) breaks = true;
                _cnt[paiSerial + 1]++; _cnt[paiSerial + 2]++;
            }
            _cnt[j]--;
            if (breaks) { _cnt[paiSerial] += 2; return false; }
        }
        _cnt[paiSerial] += 2;
        return true;
    }

    // ─── Private: Basic Form Checks ─────────────────────────────────────────

    private bool ChkKokushi()
    {
        if (_handCnt != 14) return false;
        int[] kokTbl = { 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33 };
        bool hasDouble = false;
        foreach (int k in kokTbl)
        {
            if (_cnt[k] == 2) hasDouble = true;
            else if (_cnt[k] != 1) return false;
        }
        return hasDouble;
    }

    private bool ChkChitoi()
    {
        if (_handCnt != 14) return false;
        for (int i = 0; i < 34; i++)
            if (_cnt[i] != 0 && _cnt[i] != 2) return false;
        return true;
    }

    private bool ChkHead(int top, int cnt)
    {
        if (cnt < 2) return false;
        while (top < 34 && _cnt[top] == 0) top++;
        if (top >= 34) return false;

        if (_cnt[top] >= 3)
        {
            _cnt[top] -= 3;
            bool b = ChkHead(top, cnt - 3);
            _cnt[top] += 3;
            if (b) return true;
            _cnt[top] -= 2;
            b = ChkMent(top, cnt - 2);
            _cnt[top] += 2;
            return b;
        }
        if (_cnt[top] == 2)
        {
            _cnt[top] -= 2;
            bool b = ChkMent(top + 1, cnt - 2);
            _cnt[top] += 2;
            if (b) return true;
            if (top < 27 && top % 9 < 7 && _cnt[top + 1] >= 2 && _cnt[top + 2] >= 2)
            {
                _cnt[top] -= 2; _cnt[top + 1] -= 2; _cnt[top + 2] -= 2;
                b = ChkHead(top + 1, cnt - 6);
                _cnt[top] += 2; _cnt[top + 1] += 2; _cnt[top + 2] += 2;
                return b;
            }
            return false;
        }
        if (top < 27 && top % 9 < 7 && _cnt[top + 1] >= 1 && _cnt[top + 2] >= 1)
        {
            _cnt[top]--; _cnt[top + 1]--; _cnt[top + 2]--;
            bool b = ChkHead(top, cnt - 3);
            _cnt[top]++; _cnt[top + 1]++; _cnt[top + 2]++;
            return b;
        }
        return false;
    }

    private bool ChkMent(int top, int cnt)
    {
        if (cnt == 0) return true;
        while (top < 34 && _cnt[top] == 0) top++;
        if (top >= 34) return false;

        if (_cnt[top] == 3)
        {
            _cnt[top] -= 3;
            bool b = ChkMent(top + 1, cnt - 3);
            _cnt[top] += 3;
            return b;
        }
        if (top >= 27 || top % 9 >= 7) return false;
        if (_cnt[top] == 4)
        {
            if (_cnt[top + 1] > 0 && _cnt[top + 2] > 0)
            {
                _cnt[top] -= 4; _cnt[top + 1]--; _cnt[top + 2]--;
                bool b = ChkMent(top + 1, cnt - 6);
                _cnt[top] += 4; _cnt[top + 1]++; _cnt[top + 2]++;
                return b;
            }
            return false;
        }
        if (_cnt[top] == 2)
        {
            if (_cnt[top + 1] >= 2 && _cnt[top + 2] >= 2)
            {
                _cnt[top] -= 2; _cnt[top + 1] -= 2; _cnt[top + 2] -= 2;
                bool b = ChkMent(top + 1, cnt - 6);
                _cnt[top] += 2; _cnt[top + 1] += 2; _cnt[top + 2] += 2;
                return b;
            }
            return false;
        }
        if (_cnt[top + 1] > 0 && _cnt[top + 2] > 0)
        {
            _cnt[top]--; _cnt[top + 1]--; _cnt[top + 2]--;
            bool b = ChkMent(top + 1, cnt - 3);
            _cnt[top]++; _cnt[top + 1]++; _cnt[top + 2]++;
            return b;
        }
        return false;
    }

    // ─── Yaku Check / Resolution ─────────────────────────────────────────────

    private void SetContext(bool tsumo, bool menzen, int chanfon, int menfon, int paiHora, bool doubleYakuman = true)
    {
        _isTsumo = tsumo; _isMenzen = menzen;
        _chanfon = chanfon; _menfon = menfon; _paiHora = paiHora;
        _doubleYakuman = doubleYakuman;
    }

    private bool ChkYaku(bool kuitan)
    {
        if (ChkKokushi()) return true;
        if (ChkChitoi()) return true;
        if (!ChkHead(0, _handCnt)) return false;
        return ChkYakuGeneral(kuitan);
    }

    private void GetYakuInternal(Yaku yaku, bool kuitan)
    {
        if (ChkKokushi())       { ChkYakuKokushi(yaku); return; }
        GetYakuGeneral(yaku, kuitan);
        if (!yaku.IsYakuman && ChkChitoi())
        {
            bool ym = false;
            var tmp = new Yaku();
            ChkYakuChitoi(tmp, ref ym);
            yaku.CheckAndUpdate(tmp);
        }
    }

    private bool ChkYakuGeneral(bool kuitan)
    {
        var men = new Mentsu[MentsuCount];
        PreFillFuroMen(men);
        bool ym = false;
        for (int i = 0; i < 34; i++)
        {
            if (_cnt[i] < 2) continue;
            _cnt[i] -= 2;
            if (ResMenShu(men, _furoCnt, 0, _handCnt - 2))
            {
                var y1 = new Yaku();
                ChkYakuGeneral(y1, kuitan, i, men, ref ym);
                if (y1.HanSum > 0) { _cnt[i] += 2; return true; }

                ResMenKou(men, _furoCnt, 0, _handCnt - 2);
                var y2 = new Yaku();
                ChkYakuGeneral(y2, kuitan, i, men, ref ym);
                if (y2.HanSum > 0) { _cnt[i] += 2; return true; }
            }
            _cnt[i] += 2;
        }
        return false;
    }

    private void GetYakuGeneral(Yaku yaku, bool kuitan)
    {
        var men = new Mentsu[MentsuCount];
        PreFillFuroMen(men);
        bool ym = false;
        for (int i = 0; i < 34; i++)
        {
            if (_cnt[i] < 2) continue;
            _cnt[i] -= 2;
            if (ResMenShu(men, _furoCnt, 0, _handCnt - 2))
            {
                var y1 = new Yaku();
                ChkYakuGeneral(y1, kuitan, i, men, ref ym);
                yaku.CheckAndUpdate(y1);
                ResMenKou(men, _furoCnt, 0, _handCnt - 2);
                var y2 = new Yaku();
                ChkYakuGeneral(y2, kuitan, i, men, ref ym);
                yaku.CheckAndUpdate(y2);
            }
            _cnt[i] += 2;
        }
    }

    // ─── Meld Resolution ─────────────────────────────────────────────────────

    private struct Mentsu
    {
        public int Pai;   // serial
        public Act Act;

        public bool IsKan()  => Act is Act.Kan or Act.Ank or Act.Cha;
        public bool IsKou()  => Act is not (Act.Shu or Act.Chi);
        public bool IsShu()  => Act is Act.Shu or Act.Chi;
        public bool IsGreen()
        {
            if (Pai == 32) return true;
            if (Pai / 9 != 1) return false;
            if (IsShu()) return Pai % 9 == 1;
            bool[] k = { false, true, true, true, false, true, false, true, false };
            return k[Pai % 9];
        }
    }

    private void PreFillFuroMen(Mentsu[] men)
    {
        for (int i = 0; i < _furoCnt; i++)
        {
            men[i] = new Mentsu
            {
                Pai = _furo[i]!.Tiles[0].GetSerial(),
                Act = _furo[i]!.Act,
            };
        }
    }

    private bool ResMenShu(Mentsu[] men, int slot, int top, int cnt)
    {
        if (cnt == 0) return true;
        while (top < 34 && _cnt[top] == 0) top++;
        if (top >= 34) return false;

        if (top < 27 && top % 9 < 7 && _cnt[top + 1] > 0 && _cnt[top + 2] > 0)
        {
            _cnt[top]--; _cnt[top + 1]--; _cnt[top + 2]--;
            if (ResMenShu(men, slot + 1, top, cnt - 3))
            {
                _cnt[top]++; _cnt[top + 1]++; _cnt[top + 2]++;
                men[slot] = new Mentsu { Pai = top, Act = Act.Shu };
                return true;
            }
            _cnt[top]++; _cnt[top + 1]++; _cnt[top + 2]++;
        }
        if (_cnt[top] >= 3)
        {
            _cnt[top] -= 3;
            if (ResMenShu(men, slot + 1, top, cnt - 3))
            {
                _cnt[top] += 3;
                men[slot] = new Mentsu { Pai = top, Act = Act.Kou };
                return true;
            }
            _cnt[top] += 3;
        }
        return false;
    }

    private bool ResMenKou(Mentsu[] men, int slot, int top, int cnt)
    {
        if (cnt == 0) return true;
        while (top < 34 && _cnt[top] == 0) top++;
        if (top >= 34) return false;

        if (_cnt[top] >= 3)
        {
            _cnt[top] -= 3;
            if (ResMenKou(men, slot + 1, top, cnt - 3))
            {
                _cnt[top] += 3;
                men[slot] = new Mentsu { Pai = top, Act = Act.Kou };
                return true;
            }
            _cnt[top] += 3;
        }
        if (top < 27 && top % 9 < 7 && _cnt[top + 1] > 0 && _cnt[top + 2] > 0)
        {
            _cnt[top]--; _cnt[top + 1]--; _cnt[top + 2]--;
            if (ResMenKou(men, slot + 1, top, cnt - 3))
            {
                _cnt[top]++; _cnt[top + 1]++; _cnt[top + 2]++;
                men[slot] = new Mentsu { Pai = top, Act = Act.Shu };
                return true;
            }
            _cnt[top]++; _cnt[top + 1]++; _cnt[top + 2]++;
        }
        return false;
    }

    // ─── Yaku Evaluation ─────────────────────────────────────────────────────

    private void ChkYakuGeneral(Yaku yaku, bool kuitan, int jan, Mentsu[] men, ref bool yakumanOnly)
    {
        // ── Yakuman ──────────────────────────────────────────────────────────
        bool allKou = true;
        for (int i = 0; i < MentsuCount; i++) if (men[i].IsShu()) { allKou = false; break; }
        if (_isMenzen && allKou && (_isTsumo || jan == _paiHora))
        {
            // 四暗刻: 単騎待ち(Suuankou2) は通常ゲームでダブル役満 — 原典: CHand::chkYakuGeneral bRevaluate
            yaku.AddYakuman(jan == _paiHora ? HoraYaku.Suuankou2 : HoraYaku.Suuankou,
                            jan == _paiHora && _doubleYakuman ? 2 : 1);
        }

        bool allKan = true;
        for (int i = 0; i < MentsuCount; i++) if (!men[i].IsKan()) { allKan = false; break; }
        if (allKan) yaku.AddYakuman(HoraYaku.Suukantsu, 1);

        int sangen = 0;
        for (int i = 0; i < MentsuCount; i++) if (men[i].Pai >= 31) sangen++;
        if (sangen == 3) yaku.AddYakuman(HoraYaku.Daisangen, 1);

        int fonCnt = (jan >= 27 && jan < 31 ? 1 : 0);
        for (int i = 0; i < MentsuCount; i++) if (men[i].Pai >= 27 && men[i].Pai < 31) fonCnt++;
        if (fonCnt == 4)
        {
            if (jan < 27 || jan >= 31)
                yaku.AddYakuman(HoraYaku.Daisuushi, _doubleYakuman ? 2 : 1);
            else
                yaku.AddYakuman(HoraYaku.Shosuushi, 1);
        }

        if (jan < 27 && (jan % 9 == 0 || jan % 9 == 8))
        {
            bool allRao = true;
            for (int i = 0; i < MentsuCount; i++)
                if (men[i].IsShu() || men[i].Pai >= 27 || (men[i].Pai % 9 != 0 && men[i].Pai % 9 != 8))
                { allRao = false; break; }
            if (allRao) yaku.AddYakuman(HoraYaku.Chinroutou, 1);
        }

        if (PaiIsGreen(jan))
        {
            bool allGreen = true;
            for (int i = 0; i < MentsuCount; i++) if (!men[i].IsGreen()) { allGreen = false; break; }
            if (allGreen) yaku.AddYakuman(HoraYaku.Ryuisou, 1);
        }

        if (_isMenzen && _paiHora < 27)
        {
            int s = (_paiHora / 9) * 9;
            if (_cnt[s + 0] >= 3 && _cnt[s + 8] >= 3)
            {
                bool churen = true;
                for (int i = 1; i < 8; i++) if (_cnt[s + i] == 0) { churen = false; break; }
                if (churen)
                {
                    int c = _cnt[_paiHora];
                    // 九蓮宝燈: 9面待ち(Churenpaotou2) は通常ゲームでダブル役満 — 原典: CHand::chkYakuGeneral bRevaluate
                    yaku.AddYakuman(c == 2 || c == 4 ? HoraYaku.Churenpaotou2 : HoraYaku.Churenpaotou,
                                   (c == 2 || c == 4) && _doubleYakuman ? 2 : 1);
                }
            }
        }

        if (jan >= 27)
        {
            bool allTsu = true;
            for (int i = 0; i < MentsuCount; i++) if (men[i].Pai < 27) { allTsu = false; break; }
            if (allTsu) yaku.AddYakuman(HoraYaku.Tsuisou, 1);
        }

        if (yaku.IsYakuman) { yaku.Fu = 0; yaku.Ten = 8000 * yaku.HanSum; return; }
        if (yakumanOnly) { yaku.Ten = 0; return; }

        // ── Normal Yaku ──────────────────────────────────────────────────────
        int[] sm = new int[34], km = new int[34];
        bool toitoi = true;
        for (int i = 0; i < MentsuCount; i++)
        {
            if (men[i].IsShu()) { sm[men[i].Pai]++; toitoi = false; }
            else                  km[men[i].Pai]++;
        }
        if (toitoi) yaku.AddYaku(HoraYaku.Toitoi, 2);

        // Ittsuu
        for (int s = 0; s < 27; s += 9)
            if (sm[s] > 0 && sm[s + 3] > 0 && sm[s + 6] > 0)
                { AddYakuKui(yaku, HoraYaku.Ittsuu, 2); break; }

        // Sanshoku doujun
        for (int i = 0; i < 7; i++)
            if (sm[i] > 0 && sm[i + 9] > 0 && sm[i + 18] > 0)
                { AddYakuKui(yaku, HoraYaku.Sanshokudoujun, 2); break; }

        // Sanshoku doukou
        for (int i = 0; i < 9; i++)
            if (km[i] > 0 && km[i + 9] > 0 && km[i + 18] > 0)
                { yaku.AddYaku(HoraYaku.Sanshokudoukou, 2); break; }

        // Shosangen
        if (jan >= 31 && km[27 + 4] + km[27 + 5] + km[27 + 6] == 2)
            yaku.AddYaku(HoraYaku.Shosangen, 2);

        // Iipeikou / Ryanpeikou
        if (_isMenzen)
        {
            int n = 0;
            for (int si = 0; si < 27; si += 9)
                for (int j = 0; j < 7; j++)
                    if (sm[si + j] == 4) n = 2;
                    else if (sm[si + j] >= 2) n++;
            if (n == 1) yaku.AddYaku(HoraYaku.Iipeikou, 1);
            else if (n == 2) yaku.AddYaku(HoraYaku.Ryanpeikou, 3);
        }

        // Tanyao / Chanta / Junchan / Honroutou
        {
            int nj = jan >= 27 ? 1 : 0;
            int ny = jan < 27 && (jan % 9 == 0 || jan % 9 == 8) ? 1 : 0;
            for (int i = 0; i < MentsuCount; i++)
            {
                if (men[i].IsShu())
                {
                    if (men[i].Pai % 9 == 0 || men[i].Pai % 9 == 6) ny++;
                }
                else
                {
                    if (men[i].Pai >= 27) nj++;
                    else if (men[i].Pai % 9 == 0 || men[i].Pai % 9 == 8) ny++;
                }
            }
            if (ny + nj == 0 && (_isMenzen || kuitan)) yaku.AddYaku(HoraYaku.Tanyao, 1);
            else if (ny == 5) AddYakuKui(yaku, HoraYaku.Junchan, 3);
            else if (ny + nj == 5)
            {
                if (toitoi) yaku.AddYaku(HoraYaku.Honroutou, 2);
                else AddYakuKui(yaku, HoraYaku.Chanta, 2);
            }
        }

        // Honitsu / Chinitsu
        {
            bool ok = true; int n = 0; int kind = -1;
            if (jan >= 27) n++;
            else kind = jan / 9;
            for (int i = 0; i < MentsuCount; i++)
            {
                if (men[i].Pai >= 27) n++;
                else if (kind < 0) kind = men[i].Pai / 9;
                else if (men[i].Pai / 9 != kind) { ok = false; break; }
            }
            if (ok) AddYakuKui(yaku, n == 0 ? HoraYaku.Chinisou : HoraYaku.Honisou, n == 0 ? 6 : 3);
        }

        // Sankantsu
        {
            int n = 0;
            for (int i = 0; i < MentsuCount; i++) if (men[i].IsKan()) n++;
            if (n == 3) yaku.AddYaku(HoraYaku.Sankantsu, 2);
        }

        // Yakuhai
        {
            int n = 0;
            for (int i = 0; i < MentsuCount; i++)
            {
                int v = ChkYakuhai(men[i].Pai);
                if (v > 0) { yaku.YakuhaiCnt[men[i].Pai - 27] = v; n += v; }
            }
            if (n > 0) yaku.AddYaku(HoraYaku.Yakuhai, n);
        }

        // Sanankou
        {
            bool koutsu = true;
            for (int i = 0; i < MentsuCount; i++)
                if (men[i].IsShu() && men[i].Pai <= _paiHora && _paiHora <= men[i].Pai + 2)
                { koutsu = false; break; }
            int n = 0;
            for (int i = 0; i < MentsuCount; i++)
                if (men[i].Act is Act.Kou or Act.Ank && (men[i].Pai != _paiHora || !koutsu || _isTsumo)) n++;
            if (n == 3) yaku.AddYaku(HoraYaku.Sanankou, 2);
        }

        // Pinfu
        if (_isMenzen && _paiHora < 27 && ChkYakuhai(jan) == 0)
        {
            int horaNumber = _paiHora % 9 + 1;
            bool koutsu = false;
            bool ryanmen = false;
            for (int i = 0; i < MentsuCount; i++)
            {
                if (men[i].IsKou()) { koutsu = true; break; }
                if (horaNumber >= 4 && men[i].Pai == _paiHora - 2) ryanmen = true;
                if (horaNumber <= 6 && men[i].Pai == _paiHora)     ryanmen = true;
            }
            if (!koutsu && ryanmen) { yaku.AddYaku(HoraYaku.Pinfu, 1); yaku.Pinfu = true; }
        }

        CalcFu(yaku, jan, men);
        yaku.CalcHoraTen();
    }

    // ─── Chitoitsu ───────────────────────────────────────────────────────────

    private void ChkYakuChitoi(Yaku yaku, ref bool yakuman)
    {
        var buf = new List<int>();
        for (int i = 0; i < 34; i++) if (_cnt[i] != 0) buf.Add(i);

        if (buf.Count == 7 && buf[0] >= 27)
        {
            yaku.AddYakuman(HoraYaku.Tsuisou, 1);
            yaku.Ten = 8000;
            return;
        }
        if (!yakuman)
        {
            yaku.AddYaku(HoraYaku.Chitoitsu, 2);
            bool c = true, h = true, y = true, t = true;
            int lastNumeric = buf[0];
            foreach (int x in buf)
            {
                if (x >= 27)        { c = false; t = false; break; }
                else if (x % 9 % 8 == 0) t = false;
                else                  y = false;
                lastNumeric = x;
            }
            if (c && buf[0] / 9 == lastNumeric / 9) AddYakuKui(yaku, HoraYaku.Chinisou, 6);
            else if (h && buf[0] / 9 == lastNumeric / 9) AddYakuKui(yaku, HoraYaku.Honisou, 3);
            if (y)      yaku.AddYaku(HoraYaku.Honroutou, 2);
            else if (t) yaku.AddYaku(HoraYaku.Tanyao, 1);
            yaku.Fu = 25;
            yaku.CalcHoraTen();
        }
        else yaku.Ten = 0;
    }

    // ─── Kokushi ─────────────────────────────────────────────────────────────

    private void ChkYakuKokushi(Yaku yaku)
    {
        int c = _cnt[_paiHora];
        // 国士無双: 13面待ち(Kokushi2) は通常ゲームでダブル役満 — 原典: CHand::chkYakuKokushi bRevaluate
        yaku.AddYakuman(c == 2 ? HoraYaku.Kokushi2 : HoraYaku.Kokushi,
                        c == 2 && _doubleYakuman ? 2 : 1);
        yaku.Ten = 8000 * yaku.HanSum;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void AddYakuKui(Yaku yaku, HoraYaku name, int han)
    {
        if (!_isMenzen) han--;
        yaku.AddYaku(name, han);
    }

    private int ChkYakuhai(int paiSerial)
    {
        if (paiSerial < 27) return 0;
        if (paiSerial >= 31) return 1;   // sangenpai (Haku/Hatsu/Chun)
        int n = 0;
        if (paiSerial == 27 + _chanfon) n++;
        if (paiSerial == 27 + _menfon)  n++;
        return n;
    }

    private void CalcFu(Yaku yaku, int jan, Mentsu[] men)
    {
        int fu = 20;
        if (!yaku.Pinfu)
        {
            bool koutsu    = !_isTsumo;
            bool horafu    = false;
            if (_paiHora == jan) { koutsu = false; horafu = true; }
            fu += 2 * ChkYakuhai(jan);
            for (int i = 0; i < MentsuCount; i++)
            {
                if (men[i].IsShu())
                {
                    if (men[i].Pai <= _paiHora && _paiHora <= men[i].Pai + 2)
                    {
                        koutsu = false;
                        int num = _paiHora % 9 + 1;
                        if ((num == 3 && men[i].Pai == _paiHora - 2) ||
                            (num == 7 && men[i].Pai == _paiHora) ||
                            (num > 1 && men[i].Pai == _paiHora - 1))
                            horafu = true;
                    }
                }
            }
            for (int i = 0; i < MentsuCount; i++)
            {
                if (men[i].IsKou())
                {
                    int tmp = men[i].Act switch
                    {
                        Act.Kou => (men[i].Pai != _paiHora || !koutsu) ? 4 : 2,
                        Act.Pon => 2,
                        Act.Kan or Act.Cha => 8,
                        Act.Ank => 16,
                        _ => 2,
                    };
                    bool yaochu = men[i].Pai >= 27 || men[i].Pai % 9 == 0 || men[i].Pai % 9 == 8;
                    if (yaochu) tmp *= 2;
                    fu += tmp;
                }
            }
            if (horafu) fu += 2;
            if (_isTsumo) fu += 2;
        }
        if (!_isTsumo && (fu == 20 || _isMenzen)) fu += 10;
        yaku.Fu = (fu + 9) / 10 * 10;
    }

    // ─── Serial helpers ──────────────────────────────────────────────────────
    private static bool PaiIsGreen(int serial)
    {
        if (serial == 32) return true;
        if (serial / 9 != 1) return false;
        bool[] tbl = { false, true, true, true, false, true, false, true, false };
        return tbl[serial % 9];
    }
}
