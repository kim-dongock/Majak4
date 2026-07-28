using System.Globalization;
using System.Text;

namespace MajakServer.Services;

/// <summary>
/// NHN Hangame ログインクッキー (login クッキー) の解析・復号サービス。
///
/// ■ クッキー形式
///   HTTP Cookie ヘッダー:  login=hangame%3D{URL_ENCODED_CSV}
///   または:                login=hangametest%3D{URL_ENCODED_CSV}  (alpha/local 環境)
///   ※ ブラウザから見える cookie 名は "login"、値の先頭に "hangame=" prefix が埋め込まれる。
///
/// ■ CSV フィールド (28 項目, HangameLoginCookieOrder に対応)
///   [0] userid        ← unpackString 復号対象
///   [1] password      ← unpackString 復号対象
///   [2] name          ← unpackString 復号対象
///   [3] sex           (M/F)
///   [4] age
///   [5] valid         (Y/N)
///   [6] avatarid
///   ...
///
/// ■ unpackString アルゴリズム (com.nhn.sapphire.security.CookieEncryptor 互換)
///   1. 末尾 4 文字 = CRC-16 CCITT 変形チェックサム (16 進数)
///   2. 残り文字列を Latin-1 バイト列として扱い、カスタム Base64 デコード (4 chars → 3 bytes)
///      カスタム alphabet: "URnsDa4jzCWrpP-hlt3M68OHfIXJZNGo7Ve_E2widBkcxqg51vmSKY0yAbFu9LTQ"
///   3. XOR chain 復号: R = phB[0]; result[i] = R ^ phB[i+1]; R = phB[i+1]; (NUL で終端)
///   4. 結果バイト列を Shift_JIS (MS932) でデコード
/// </summary>
public static class HangameCookieDecryptor
{
    // ────────────────────────────────────────────────────────────────
    // フィールド定義 (HangameLoginCookieOrder)
    // ────────────────────────────────────────────────────────────────

    public static readonly string[] FieldNames =
    {
        "userid",  "password",    "name",       "sex",     "age",
        "valid",   "avatarid",    "idvalid",    "nickname","socialid",
        "absuid",  "absstatus",   "pluslink",   "service", "subupdate",
        "regpath", "lastdate",    "naverid",    "from",    "chkmip",
        "roomid",  "figclass",    "emailchk",   "birthday","userno",
        "avitemexpire", "chclassno", "siftuse"
    };

    // unpackString を適用するフィールドのインデックス
    // (HangameLoginCookieOrder.needPackStringValues と同じ)
    private static readonly HashSet<int> PackedIndices = new()
    {
        0,  // userid
        1,  // password
        2,  // name
        9,  // socialid
        17, // naverid
        19, // chkmip
        23, // birthday
        24, // userno
        25, // avitemexpire
    };

    // ────────────────────────────────────────────────────────────────
    // カスタム Base64 テーブル (LoginCookieEncryptor.charset)
    // ────────────────────────────────────────────────────────────────

    private const string Charset = "URnsDa4jzCWrpP-hlt3M68OHfIXJZNGo7Ve_E2widBkcxqg51vmSKY0yAbFu9LTQ";

    // charmap: ASCII 値 → 6 bit インデックス (0xFF = 無効文字)
    private static readonly byte[] Charmap = BuildCharmap();

    private static byte[] BuildCharmap()
    {
        var map = new byte[256];
        Array.Fill(map, (byte)0xFF);
        for (int i = 0; i < Charset.Length; i++)
            map[(byte)Charset[i]] = (byte)i;
        return map;
    }

    // ────────────────────────────────────────────────────────────────
    // エンコーディング (Shift_JIS / MS932)
    // ────────────────────────────────────────────────────────────────

    private static readonly Encoding ShiftJis;

    static HangameCookieDecryptor()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ShiftJis = Encoding.GetEncoding(932); // MS932 = Shift-JIS
    }

    // ────────────────────────────────────────────────────────────────
    // 公開 API
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// "login" クッキーの値 ("hangame=..." 形式) からユーザーID を抽出する。
    /// </summary>
    /// <param name="loginCookieValue">HTTP クッキー値 (例: "hangame=U4xn...")</param>
    /// <returns>復号したユーザーID。解析失敗・未ログインの場合は null。</returns>
    public static string? GetUserId(string loginCookieValue)
    {
        var fields = ParseCookie(loginCookieValue);
        return fields?.GetValueOrDefault("userid");
    }

    /// <summary>
    /// クッキー値を解析し、全フィールドの辞書を返す。
    /// </summary>
    public static Dictionary<string, string>? ParseCookie(string cookieValue)
    {
        if (string.IsNullOrEmpty(cookieValue))
            return null;

        // ① prefix を除去 ("hangame=" / "hangametest=")
        if (cookieValue.StartsWith("hangametest=", StringComparison.Ordinal))
            cookieValue = cookieValue["hangametest=".Length..];
        else if (cookieValue.StartsWith("hangame=", StringComparison.Ordinal))
            cookieValue = cookieValue["hangame=".Length..];

        if (string.IsNullOrWhiteSpace(cookieValue))
            return null;

        // ② URL デコード (+ は空白にしない: %2B→+ のみ)
        // Java の URLDecoder.decode(..., "ISO-8859-1") に相当
        var decoded = Uri.UnescapeDataString(cookieValue.Replace("+", "%2B"));

        // ③ CSV 分割
        var values = ParseCsv(decoded);
        if (values.Count < FieldNames.Length)
            return null;

        // ④ 各フィールドを復号
        var result = new Dictionary<string, string>(FieldNames.Length, StringComparer.Ordinal);
        for (int i = 0; i < FieldNames.Length; i++)
        {
            var raw = values[i];
            string fieldValue;

            if (PackedIndices.Contains(i) && raw.Length > 0)
            {
                if (!TryUnpackString(raw, out var unpacked))
                    return null; // チェックサム不一致 → 不正クッキー
                fieldValue = unpacked ?? string.Empty;
            }
            else
            {
                fieldValue = raw;
            }

            result[FieldNames[i]] = fieldValue;
        }

        return result;
    }

    // ────────────────────────────────────────────────────────────────
    // unpackString
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// packString で暗号化された文字列を復号する。
    /// </summary>
    public static bool TryUnpackString(string packed, out string? result)
    {
        result = null;

        // 最低限: 4 文字 (エンコード) + 4 文字 (チェックサム)
        if (packed.Length < 8)
            return false;

        // ① 末尾 4 文字 = CRC-16 チェックサム
        var body = packed.AsSpan(0, packed.Length - 4);
        var chksumStr = packed[^4..];

        if (!int.TryParse(chksumStr, NumberStyles.HexNumber, null, out int expectedChksum))
            return false;

        int actualChksum = ComputeChecksum(body);
        if (actualChksum != expectedChksum)
            return false;

        // ② body バイト列取得 (Latin-1 として扱う = ASCII byte per char)
        if (body.Length % 4 != 0)
            return false;

        var src = new byte[body.Length];
        for (int i = 0; i < body.Length; i++)
            src[i] = (byte)body[i]; // 全文字 0x00-0x7E の範囲

        // ③ カスタム Base64 デコード: 4 chars → 3 bytes
        int phBsize = (src.Length / 4) * 3;
        var phB = new byte[phBsize];
        for (int ptrA = 0, ptrB = 0; ptrA < src.Length; ptrA += 4, ptrB += 3)
            Ascii2Value(phB, ptrB, src, ptrA);

        // ④ XOR chain 復号 (phB[0] = ランダムシード R)
        var output = new byte[phBsize];
        byte R = phB[0];
        int length = 0;
        for (int psrc = 1; psrc < phBsize; psrc++)
        {
            byte val = (byte)(R ^ phB[psrc]);
            if (val == 0) break;   // NUL = 終端
            output[length++] = val;
            R = phB[psrc];
        }

        // ⑤ Shift-JIS (MS932) デコード
        result = ShiftJis.GetString(output, 0, length);
        return true;
    }

    // ────────────────────────────────────────────────────────────────
    // 内部メソッド
    // ────────────────────────────────────────────────────────────────

    /// <summary>カスタム Base64 の 4 chars → 3 bytes 変換。</summary>
    private static void Ascii2Value(byte[] dst, int dstOff, byte[] src, int srcOff)
    {
        int v1 = Charmap[src[srcOff]];
        int v2 = Charmap[src[srcOff + 1]];
        int v3 = Charmap[src[srcOff + 2]];
        int v4 = Charmap[src[srcOff + 3]];
        dst[dstOff]     = (byte)((0xfc & (v1 << 2)) | (0x03 & (v2 >> 4)));
        dst[dstOff + 1] = (byte)((0xf0 & (v2 << 4)) | (0x0f & (v3 >> 2)));
        dst[dstOff + 2] = (byte)((0xc0 & (v3 << 6)) | (0x3f & v4));
    }

    /// <summary>
    /// CRC-16 CCITT 変形チェックサム。
    /// Java: LoginCookieEncryptor.checkSum(s1, s2) の C# 移植。
    /// polynomial = 0x01102100 (CCITT-16)
    /// </summary>
    private static int ComputeChecksum(ReadOnlySpan<char> s)
    {
        // Java: src = s.getBytes(ENCODING) → 全文字 ASCII なので Latin-1 と同等
        const long Polynomial = 0x01102100L;
        uint sum = 0;

        foreach (char c in s)
        {
            sum |= (byte)c;
            for (int j = 0; j < 8; j++)
            {
                sum <<= 1;
                if ((sum & 0x01000000u) != 0)
                    sum ^= (uint)Polynomial;
            }
        }

        // Java の末尾ゼロバイト 2 回処理 (flush CRC)
        for (int i = 0; i < 2; i++)
        {
            sum |= 0;
            for (int j = 0; j < 8; j++)
            {
                sum <<= 1;
                if ((sum & 0x01000000u) != 0)
                    sum ^= (uint)Polynomial;
            }
        }

        // Java: (int)((sum >>> 8) & 0x0000ffff)
        return (int)((sum >> 8) & 0x0000ffffu);
    }

    /// <summary>
    /// CSV 文字列をカンマで分割する。ダブルクォートの中のカンマは無視。
    /// com.nhn.sapphire.util.CSV.parse() に相当。
    /// </summary>
    private static List<string> ParseCsv(string input)
    {
        var result = new List<string>(FieldNames.Length);
        var sb = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in input)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }
}
