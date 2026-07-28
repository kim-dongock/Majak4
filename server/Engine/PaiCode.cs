namespace MajakServer.Engine;

/// <summary>
/// Tile code type — port of CPaiCode.
/// Encoding: (kind &lt;&lt; 4) | number
///   kind 0=Man, 1=Sou, 2=Pin, 3=Tsu
///   number 1..9 for suited; 1..7 for honor (E/S/W/N/Haku/Hatsu/Chun)
/// Invalid = 0x3F.  Flower tiles = code &gt;= 0x40.
/// Serial (0-33): kind*9 + number-1  (honors: 27+n-1)
/// </summary>
public struct PaiCode : IEquatable<PaiCode>
{
    public enum Kind { Man = 0, Sou = 1, Pin = 2, Tsu = 3 }

    private int  _code;
    private bool _isRed;
    private byte _bipaiIndex;

    // ─── Factories ───────────────────────────────────────────────────────────
    public PaiCode(int kind, int number) : this((kind << 4) | number) { }
    internal PaiCode(int code) { _code = code; _isRed = false; _bipaiIndex = 0; }
    public PaiCode() { _code = 0x3F; _isRed = false; _bipaiIndex = 0; }

    public static PaiCode Invalid  => new(0x3F);
    public static PaiCode MakeSerial(int serial)
        => new(serial + serial / 9 * 7 + 1);   // inverse of GetSerial()

    // ─── Predicates ──────────────────────────────────────────────────────────
    public bool IsValid    => (_code > 0x30 && _code <= 0x37)
                           || (_code > 0 && _code < 0x30 && (_code & 0xF) > 0 && (_code & 0xF) <= 9);
    public bool IsTsupai   => _code >= 0x30;
    public bool IsShupai   => _code < 0x30;
    public bool IsHuapai   => _code >= 0x40;
    public bool IsYaochupai => IsTsupai || (_code & 0x07) == 1;
    public bool IsRaotoupai => IsShupai && (_code & 0x07) == 1;
    public bool IsFonpai   => _code >= 0x31 && _code < 0x35;
    public bool IsSangenpai => _code >= 0x35;
    public bool IsWind       => _code >= 0x31 && _code <= 0x34;
    public bool IsWindOf(int n) => _code == 0x31 + n;
    public bool IsFonpaiOf(int n) => _code == 0x31 + n;
    public bool IsSangenpaiOf(int n) => _code == 0x35 + n;

    public bool IsGreen
    {
        get
        {
            if (_code == 0x36) return true;              // Hatsu
            if ((_code & 0xF0) != 0x10) return false;   // must be Sou
            bool[] tbl = { false, true, true, true, false, true, false, true, false };
            int n = (_code & 0x0F) - 1;
            return n >= 0 && n < 9 && tbl[n];
        }
    }

    // ─── Accessors ───────────────────────────────────────────────────────────
    public int  GetNumber() => _code & 0x0F;
    public Kind GetKind()   => (Kind)(_code >> 4);
    public int  Code        => _code;

    public bool IsRed { get => _isRed; set => _isRed = value; }
    public int  BipaiIndex { get => _bipaiIndex; set => _bipaiIndex = (byte)value; }

    /// <summary>Serial index 0-33:  kind*9 + number-1</summary>
    public int GetSerial()
    {
        if (!IsValid) return -1;
        return _code - (_code >> 4) * 7 - 1;   // same formula as C++ GetSerial()
    }

    public int GetSerialRed() => _isRed ? 34 + (int)GetKind() : GetSerial();

    // ─── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Next tile for dora indicator wrapping (N→E, Chun→Haku, 9→1)</summary>
    public PaiCode GetNextNumberPai()
    {
        if (IsTsupai)
        {
            if (_code == 0x34) return new PaiCode(0x31);  // North → East
            if (_code == 0x37) return new PaiCode(0x35);  // Chun → Haku
        }
        else
        {
            if (GetNumber() == 9) return new PaiCode((_code & 0x30) | 0x01); // 9 → 1 same suit
        }
        return new PaiCode(_code + 1);
    }

    public PaiCode GetNextKindPai()
    {
        if (IsTsupai) throw new InvalidOperationException("No next kind for honor tiles");
        return new PaiCode(_code + 0x10);
    }

    // ─── Operators ───────────────────────────────────────────────────────────
    public static PaiCode operator +(PaiCode a, int n) => new(a._code + n);
    public static PaiCode operator -(PaiCode a, int n) => new(a._code - n);
    public static bool    operator ==(PaiCode a, PaiCode b) => a._code == b._code;
    public static bool    operator !=(PaiCode a, PaiCode b) => a._code != b._code;
    public static bool    operator  >(PaiCode a, PaiCode b) => a._code  > b._code;
    public static bool    operator  <(PaiCode a, PaiCode b) => a._code  < b._code;
    public static bool    operator >=(PaiCode a, PaiCode b) => a._code >= b._code;
    public static bool    operator <=(PaiCode a, PaiCode b) => a._code <= b._code;

    public bool Equals(PaiCode other) => _code == other._code;
    public override bool Equals(object? obj) => obj is PaiCode p && Equals(p);
    public override int  GetHashCode() => _code;
    public override string ToString() => IsValid ? $"{GetKind()}{GetNumber()}" : "?";
}
