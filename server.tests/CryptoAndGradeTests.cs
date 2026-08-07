using System.Text;
using MajakServer.Models.Protocol;
using MajakServer.Utils;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// GradeLevelTable テスト
// 原典: s_stLevelGradeMode[] in HMajCommon.h
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// s_stLevelGradeMode[] の MaxPoint 値を検証する。
/// 原典の enum GRADE_LEVEL を参照:
///   GRADE_10_KYU=0, GRADE_1_DAN=10, GRADE_9_DAN=18
/// </summary>
public class GradeLevelTableDetailTests
{
    // ─── 級位 (Kyu) MaxPoint ──────────────────────────────────────────────

    // 原典: {GRADE_10_KYU, 0, 0, 30, FALSE}
    [Fact] public void MaxPoint_Grade10Kyu_Is30()  => Assert.Equal(30, GradeLevelTable.GetMaxPoint(0));
    // 原典: {GRADE_9_KYU, 0, 0, 30, FALSE}
    [Fact] public void MaxPoint_Grade9Kyu_Is30()   => Assert.Equal(30, GradeLevelTable.GetMaxPoint(1));
    // 原典: {GRADE_8_KYU, 0, 0, 30, FALSE}
    [Fact] public void MaxPoint_Grade8Kyu_Is30()   => Assert.Equal(30, GradeLevelTable.GetMaxPoint(2));
    // 原典: {GRADE_7_KYU, 0, 0, 30, FALSE}
    [Fact] public void MaxPoint_Grade7Kyu_Is30()   => Assert.Equal(30, GradeLevelTable.GetMaxPoint(3));
    // 原典: {GRADE_6_KYU, 0, 0, 60, FALSE}
    [Fact] public void MaxPoint_Grade6Kyu_Is60()   => Assert.Equal(60, GradeLevelTable.GetMaxPoint(4));
    // 原典: {GRADE_5_KYU, 0, 0, 60, FALSE}
    [Fact] public void MaxPoint_Grade5Kyu_Is60()   => Assert.Equal(60, GradeLevelTable.GetMaxPoint(5));
    // 原典: {GRADE_4_KYU, 0, 0, 60, FALSE}
    [Fact] public void MaxPoint_Grade4Kyu_Is60()   => Assert.Equal(60, GradeLevelTable.GetMaxPoint(6));
    // 原典: {GRADE_3_KYU, 0, 0, 90, FALSE}
    [Fact] public void MaxPoint_Grade3Kyu_Is90()   => Assert.Equal(90, GradeLevelTable.GetMaxPoint(7));
    // 原典: {GRADE_2_KYU, 0, 0, 90, FALSE}
    [Fact] public void MaxPoint_Grade2Kyu_Is90()   => Assert.Equal(90, GradeLevelTable.GetMaxPoint(8));
    // 原典: {GRADE_1_KYU, 0, 0, 90, FALSE}
    [Fact] public void MaxPoint_Grade1Kyu_Is90()   => Assert.Equal(90, GradeLevelTable.GetMaxPoint(9));

    // ─── 段位 (Dan) MaxPoint ─────────────────────────────────────────────

    // 原典: {GRADE_1_DAN, 0, 0, 600, FALSE}
    [Fact] public void MaxPoint_Grade1Dan_Is600()  => Assert.Equal(600,  GradeLevelTable.GetMaxPoint(10));
    // 原典: {GRADE_2_DAN, 600, 0, 1200, TRUE}
    [Fact] public void MaxPoint_Grade2Dan_Is1200() => Assert.Equal(1200, GradeLevelTable.GetMaxPoint(11));
    // 原典: {GRADE_3_DAN, 600, 0, 1200, TRUE}
    [Fact] public void MaxPoint_Grade3Dan_Is1200() => Assert.Equal(1200, GradeLevelTable.GetMaxPoint(12));
    // 原典: {GRADE_4_DAN, 1200, 0, 2400, TRUE}
    [Fact] public void MaxPoint_Grade4Dan_Is2400() => Assert.Equal(2400, GradeLevelTable.GetMaxPoint(13));
    // 原典: {GRADE_5_DAN, 1200, 0, 2400, TRUE}
    [Fact] public void MaxPoint_Grade5Dan_Is2400() => Assert.Equal(2400, GradeLevelTable.GetMaxPoint(14));
    // 原典: {GRADE_6_DAN, 1200, 0, 2400, TRUE}
    [Fact] public void MaxPoint_Grade6Dan_Is2400() => Assert.Equal(2400, GradeLevelTable.GetMaxPoint(15));
    // 原典: {GRADE_7_DAN, 2400, 0, 4800, TRUE}
    [Fact] public void MaxPoint_Grade7Dan_Is4800() => Assert.Equal(4800, GradeLevelTable.GetMaxPoint(16));
    // 原典: {GRADE_8_DAN, 2400, 0, 4800, TRUE}
    [Fact] public void MaxPoint_Grade8Dan_Is4800() => Assert.Equal(4800, GradeLevelTable.GetMaxPoint(17));
    // 原典: {GRADE_9_DAN, 2400, 0, 4800, TRUE}
    [Fact] public void MaxPoint_Grade9Dan_Is4800() => Assert.Equal(4800, GradeLevelTable.GetMaxPoint(18));

    // ─── 未定義グレード ───────────────────────────────────────────────────

    [Fact] public void MaxPoint_Unknown_Is0()      => Assert.Equal(0, GradeLevelTable.GetMaxPoint(999));
    [Fact] public void MaxPoint_Negative_Is0()     => Assert.Equal(0, GradeLevelTable.GetMaxPoint(-1));
}

// ═══════════════════════════════════════════════════════════════════════════
// HangameCrypto 暗号化テスト
// 原典: Crypto.cpp — CBC 暗号化・復号化の検証
// ═══════════════════════════════════════════════════════════════════════════
public class HangameCryptoTests
{
    // ─── InitKey ─────────────────────────────────────────────────────────

    // シナリオ1: 空文字キーで InitKey → true (C++ でも空文字は許可)
    // 原典: C++ only checks for NULL, empty strings are allowed
    [Fact]
    public void InitKey_EmptyString_ReturnsTrue()
    {
        var crypto = new HangameCrypto();
        bool result = crypto.InitKey("");
        Assert.True(result);
    }

    // シナリオ2: 任意の文字列キー → true
    [Fact]
    public void InitKey_AnyString_ReturnsTrue()
    {
        var crypto = new HangameCrypto();
        Assert.True(crypto.InitKey("testkey"));
    }

    // シナリオ3: 2回 InitKey → 2回目は無視 (C++ と同じ: Skip if already initialized)
    [Fact]
    public void InitKey_CalledTwice_SecondCallIgnored()
    {
        var crypto = new HangameCrypto();
        Assert.True(crypto.InitKey("key1"));
        Assert.True(crypto.InitKey("key2")); // 2回目も true を返す
    }

    // ─── Encrypt / Decrypt 基本 ───────────────────────────────────────────

    // シナリオ4: 未初期化時の Encrypt → null
    // 原典: if(!m_isKeyInitialized || buffer == null || ...) return null
    [Fact]
    public void Encrypt_NotInitialized_ReturnsNull()
    {
        var crypto = new HangameCrypto();
        var result = crypto.Encrypt(new byte[16]);
        Assert.Null(result);
    }

    // シナリオ5: 16バイト以外の Encrypt → null
    [Fact]
    public void Encrypt_WrongSize_ReturnsNull()
    {
        var crypto = new HangameCrypto();
        crypto.InitKey("testkey");
        Assert.Null(crypto.Encrypt(new byte[15])); // 15バイト
        Assert.Null(crypto.Encrypt(new byte[17])); // 17バイト
    }

    // シナリオ6: 16バイト Encrypt → 16バイト結果
    [Fact]
    public void Encrypt_16Bytes_Returns16Bytes()
    {
        var crypto = new HangameCrypto();
        crypto.InitKey("testkey");
        var result = crypto.Encrypt(new byte[16]);
        Assert.NotNull(result);
        Assert.Equal(16, result!.Length);
    }

    // シナリオ7: Encrypt → Decrypt → 元データと一致
    [Fact]
    public void EncryptDecrypt_RoundTrip_RestoresOriginal()
    {
        var crypto = new HangameCrypto();
        crypto.InitKey("testkey");

        var original = new byte[] { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16 };
        var encrypted = crypto.Encrypt(original);
        Assert.NotNull(encrypted);

        var crypto2 = new HangameCrypto();
        crypto2.InitKey("testkey");
        var decrypted = crypto2.Decrypt(encrypted!);
        Assert.NotNull(decrypted);
        Assert.Equal(original, decrypted);
    }

    // シナリオ8: 異なるキーで Decrypt → 元データと不一致
    [Fact]
    public void Decrypt_WrongKey_NotSameAsOriginal()
    {
        var c1 = new HangameCrypto(); c1.InitKey("key1");
        var c2 = new HangameCrypto(); c2.InitKey("key2");

        var original = new byte[] { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16 };
        var encrypted = c1.Encrypt(original);
        Assert.NotNull(encrypted);

        var decrypted = c2.Decrypt(encrypted!);
        Assert.NotNull(decrypted); // 復号はできる (ただし内容は違う)
        Assert.NotEqual(original, decrypted);
    }

    // ─── CBCEncrypt / CBCDecrypt ─────────────────────────────────────────

    // シナリオ9: EncryptWithHMAC → DecryptWithHMAC ラウンドトリップ
    [Fact]
    public void EncryptWithHMAC_DecryptWithHMAC_RoundTrip()
    {
        var crypto = new HangameCrypto();
        crypto.InitKey("hmackey");

        var original = Encoding.UTF8.GetBytes("Hello, Hangame!");
        var encrypted = crypto.EncryptWithHMAC(original);
        Assert.NotNull(encrypted);

        var crypto2 = new HangameCrypto();
        crypto2.InitKey("hmackey");
        var decrypted = crypto2.DecryptWithHMAC(encrypted!);
        Assert.NotNull(decrypted);
        Assert.Equal(original, decrypted);
    }

    // シナリオ10: 空データ EncryptWithHMAC → null
    [Fact]
    public void EncryptWithHMAC_EmptyData_ReturnsNull()
    {
        var crypto = new HangameCrypto();
        crypto.InitKey("key");
        var result = crypto.EncryptWithHMAC(Array.Empty<byte>());
        Assert.Null(result);
    }

    // ─── HangameCryptographic (高レベルAPI) ───────────────────────────────

    // シナリオ11: 静的キー (useDynamicKey=false) で Encrypt → Decrypt ラウンドトリップ
    // 原典: useDynamicKey=false → key = "" (全スペースキー)
    [Fact]
    public void HangameCryptographic_StaticKey_RoundTrip()
    {
        var original = Encoding.UTF8.GetBytes("TestData123");
        var encrypted = HangameCryptographic.Encrypt(original, useDynamicKey: false);
        Assert.NotNull(encrypted);

        var decrypted = HangameCryptographic.Decrypt(encrypted!, useDynamicKey: false);
        Assert.NotNull(decrypted);
        Assert.Equal(original, decrypted);
    }

    // シナリオ12: null 入力 → null 返却
    [Fact]
    public void HangameCryptographic_NullInput_ReturnsNull()
    {
        Assert.Null(HangameCryptographic.Encrypt(null!, false));
        Assert.Null(HangameCryptographic.Decrypt(null!, false));
    }

    // シナリオ13: 空データ → null 返却
    [Fact]
    public void HangameCryptographic_EmptyInput_ReturnsNull()
    {
        Assert.Null(HangameCryptographic.Encrypt(Array.Empty<byte>(), false));
        Assert.Null(HangameCryptographic.Decrypt(Array.Empty<byte>(), false));
    }

    // シナリオ14: MAX_BLOCK 定数確認
    // 原典: C++ では BLOCK_SIZE=32
    [Fact]
    public void HangameCrypto_MaxBlock_Is32()
        => Assert.Equal(32, HangameCrypto.MAX_BLOCK);

    // ─── GetKeyWithComputerName ───────────────────────────────────────────

    // シナリオ15: キー生成 → 非空文字列
    [Fact]
    public void GetKeyWithComputerName_ReturnsNonEmpty()
    {
        string key = HangameCrypto.GetKeyWithComputerName();
        Assert.False(string.IsNullOrEmpty(key));
    }

    // シナリオ16: キーに "xpfldntm" サフィックスが含まれる
    // 原典: key = ComputerName.ToUpper() + "xpfldntm"
    [Fact]
    public void GetKeyWithComputerName_ContainsSecretSuffix()
    {
        string key = HangameCrypto.GetKeyWithComputerName();
        Assert.EndsWith("xpfldntm", key);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// CryptoHelper テスト
// 原典: Crypto.cpp EncryptDecrypt (AWS Parameter Store 用)
// ═══════════════════════════════════════════════════════════════════════════
public class CryptoHelperTests
{
    // シナリオ1: Encrypt は非空文字列を返す
    // (Encrypt は useDynamicKey=true なので Decrypt とは異なるキー)
    [Fact]
    public void Encrypt_NonEmptyString_ReturnsNonEmpty()
    {
        string result = CryptoHelper.Encrypt("Hello, World!", "key");
        Assert.False(string.IsNullOrEmpty(result));
    }

    // シナリオ2: 空文字 Encrypt → 空文字返却
    [Fact]
    public void Encrypt_EmptyString_ReturnsEmpty()
    {
        string result = CryptoHelper.Encrypt("", "key");
        Assert.Equal(string.Empty, result);
    }

    // シナリオ3: 空文字 Decrypt → 空文字返却
    [Fact]
    public void Decrypt_EmptyString_ReturnsEmpty()
    {
        string result = CryptoHelper.Decrypt("", "key");
        Assert.Equal(string.Empty, result);
    }

    // シナリオ4: HangameCryptographic 静的キーでのラウンドトリップ
    // (両方 useDynamicKey=false → 同一キー → ラウンドトリップ成功)
    [Fact]
    public void HangameCryptographic_StaticKey_RoundTrip()
    {
        var original = System.Text.Encoding.UTF8.GetBytes("Hello, World!");

        // Encrypt with static key (false)
        var encrypted = HangameCryptographic.Encrypt(original, useDynamicKey: false);
        Assert.NotNull(encrypted);

        // Decrypt with static key (false) → same key
        var decrypted = HangameCryptographic.Decrypt(encrypted!, useDynamicKey: false);
        Assert.NotNull(decrypted);
        Assert.Equal(original, decrypted);
    }

    // シナリオ5: DecryptParameterStoreValue ラウンドトリップ確認
    // static key で暗号化したデータを DecryptParameterStoreValue で復号
    [Fact]
    public void DecryptParameterStoreValue_FromStaticEncrypt_RoundTrip()
    {
        const string plaintext = "database_password=secret123";
        var bytes     = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var encrypted = HangameCryptographic.Encrypt(bytes, useDynamicKey: false);
        Assert.NotNull(encrypted);

        // DecryptParameterStoreValue は useDynamicKey=false を使用
        string base64  = System.Text.Encoding.ASCII.GetString(encrypted!);
        string result  = CryptoHelper.DecryptParameterStoreValue(base64);
        Assert.Equal(plaintext, result);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GameConst.EvtCode テスト
// 原典: HMajDef.h GH_EVTCODE_* defines
// ═══════════════════════════════════════════════════════════════════════════
public class GameConstEvtCodeTests
{
    // 原典で定義されているイベントコードの値を確認
    [Fact] public void EvtCode_DefaultMoney_Format()
        => Assert.Equal("JM00068", GameConst.EvtCodeDefaultMoney);

    [Fact] public void EvtCode_RoomCharge_Format()
        => Assert.Equal("JM00069", GameConst.EvtCodeRoomCharge);

    [Fact] public void EvtCode_FreeMoney_Format()
        => Assert.Equal("JM00070", GameConst.EvtCodeFreeMoney);
}
