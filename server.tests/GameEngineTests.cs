using MajakServer.Engine;

namespace MajakServer.Tests;

/// <summary>
/// Game engine tests for MajakGameLogic and Hand.
///
/// Covered scenarios:
///   - Init / InitHanchan: initial state validation.
///   - ProcessAction: error codes for invalid input.
///   - Hand.CheckTempai: tenpai validation.
///   - Hand.CheckHoraForm: winning form validation.
///   - PaiCode: serial conversion correctness.
/// </summary>
public class GameEngineTests
{
    // Defaults matched to the valid RuleInfo fields.
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan  = true,
        Kuitan   = true,
        Contest  = 0,
        AkaDora  = 1,
        Uma      = 0,
    };

    // Init / InitHanchan

    [Fact]
    public void Init_SetsGameStatusNotPlaying()
    {
        var logic = new MajakGameLogic();
        logic.Init();

        Assert.Equal(GameStatus.NotPlaying, logic.GameStatus);
    }

    [Fact]
    public void InitHanchan_HasFourPlayers()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        Assert.Equal(4, logic.Player.Length);
    }

    [Fact]
    public void InitHanchan_CurKyokuStartsAt0()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        Assert.Equal(0, logic.HanchanInfo.CurKyoku);
    }

    [Fact]
    public void InitHanchan_PlayerSeatsArePermutationOf0to3()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        // Seats are a permutation of 0..3.
        var seats = logic.HanchanInfo.Player.OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, seats);
    }

    [Fact]
    public void InitHanchan_EachPlayerHas13Or14Tiles()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        // Parent (OyaOrder) starts with 14 tiles; others start with 13.
        int parent = logic.KyokuInfo.OyaOrder;
        for (int i = 0; i < 4; i++)
        {
            int expected = (i == parent) ? 14 : 13;
            Assert.Equal(expected, logic.Player[i].Tehai.Count);
        }
    }

    // ProcessAction invalid input errors

    [Fact]
    public void ProcessAction_InvalidOrder_ReturnsErrInvalidOrder()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        var result = logic.ProcessAction(-1, Act.Pas, Array.Empty<int>(), 0);
        Assert.Equal(ActionResult.ErrInvalidOrder, result);
    }

    [Fact]
    public void ProcessAction_OrderOutOfRange_ReturnsErrInvalidOrder()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        var result = logic.ProcessAction(4, Act.Pas, Array.Empty<int>(), 0);
        Assert.Equal(ActionResult.ErrInvalidOrder, result);
    }

    [Fact]
    public void ProcessAction_InvalidPaiCount_ReturnsErr()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        // Tap requires 1 tile, but 0 tiles are passed.
        var result = logic.ProcessAction(0, Act.Tap, Array.Empty<int>(), 0);
        Assert.Equal(ActionResult.ErrInvalidPaiCount, result);
    }

    // KyokuInfo initial state

    [Fact]
    public void KyokuInfo_Dora_HasDoraAfterInitHanchan()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        // The initial round has a visible dora indicator.
        Assert.NotNull(logic.KyokuInfo.Dora);
        Assert.True(logic.KyokuInfo.Dora[0].IsValid);
    }

    // PaiCode MakeSerial / GetSerial

    [Theory]
    [InlineData(0)]    // 1m (serial 0)
    [InlineData(8)]    // 9m
    [InlineData(9)]    // 1s
    [InlineData(17)]   // 9s
    [InlineData(18)]   // 1p
    [InlineData(26)]   // 9p
    [InlineData(27)]   // East
    [InlineData(33)]   // Chun
    public void PaiCode_MakeSerial_RoundTrip(int serial)
    {
        var pai = PaiCode.MakeSerial(serial);
        Assert.Equal(serial, pai.GetSerial());
    }

    [Fact]
    public void PaiCode_Invalid_IsNotValid()
    {
        Assert.False(PaiCode.Invalid.IsValid);
    }

    [Fact]
    public void PaiCode_1m_IsShupai()
    {
        var pai = PaiCode.MakeSerial(0);   // 1m
        Assert.True(pai.IsShupai);
        Assert.False(pai.IsTsupai);
    }

    [Fact]
    public void PaiCode_Haku_IsTsupaiAndSangenpai()
    {
        var pai = PaiCode.MakeSerial(31);  // Haku = serial 31
        Assert.True(pai.IsTsupai);
        Assert.True(pai.IsSangenpai);
    }

    // Hand.CheckTempai

    [Fact]
    public void Hand_CheckTempai_SequenceWait_IsTrue()
    {
        // 1m2m3m 4m5m6m 7m8m9m EastEastEast [South] => South single wait tenpai.
        // serial: 0-8=1m-9m, 27=East, 28=South.
        // East triplet + South single wait completes with South as the pair.
        var player = MakePlayerWithTehai(0, 1, 2, 3, 4, 5, 6, 7, 8, 27, 27, 27, 28);
        var hand = new Hand(player);
        Assert.True(hand.CheckTempai());
    }

    [Fact]
    public void Hand_CheckTempai_BareTiles_IsFalse()
    {
        // Completely scattered tiles are not tenpai.
        var player = MakePlayerWithTehai(0, 4, 8, 12, 16, 20, 24, 28, 1, 5, 9, 13, 17);
        var hand = new Hand(player);
        Assert.False(hand.CheckTempai());
    }

    // Hand.CheckHoraForm

    [Fact]
    public void Hand_CheckHoraForm_CompleteHand_IsTrue()
    {
        // 1m2m3m 4m5m6m 7m8m9m EastEastEast [South] => wins by South ron.
        var player = MakePlayerWithTehai(
            0, 1, 2, 3, 4, 5, 6, 7, 8,    // 1m~9m
            27, 27, 27,                    // East triplet
            28                             // South (single wait)
        );
        var hand = new Hand(player);
        // Add South to complete the pair and validate CheckHoraForm(serial=28).
        Assert.True(hand.CheckHoraForm(28));
    }

    [Fact]
    public void Hand_CheckHoraForm_Incomplete_IsFalse()
    {
        // This hand does not form a winning shape.
        var player = MakePlayerWithTehai(
            0, 1, 3, 4, 6, 7, 9, 10, 12, 13, 15, 16, 18
        );
        var hand = new Hand(player);
        Assert.False(hand.CheckHoraForm(18));
    }

    // Helpers

    private static EnginePlayer MakePlayerWithTehai(params int[] serials)
    {
        var p = new EnginePlayer();
        foreach (var s in serials)
            p.Tehai.Add(PaiCode.MakeSerial(s));
        return p;
    }
}
