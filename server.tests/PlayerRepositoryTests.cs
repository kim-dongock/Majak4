using MajakServer.Commands.Channel;
using MajakServer.Infrastructure;
using MajakServer.Repositories.MySQL;

namespace MajakServer.Tests;

public class PlayerRepositoryTests
{
    [Theory]
    [InlineData("Y", true)]
    [InlineData("y", true)]
    [InlineData("N", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSkinAttachFlagSet_MatchesLegacyFirstCharacterCheck(string? attachFlag, bool expected)
    {
        Assert.Equal(expected, PlayerRepository.IsSkinAttachFlagSet(attachFlag));
    }

    [Fact]
    public void GetFestiveCupRatId_ReturnsCupIdOnlyForLegacyFestiveCupSubId()
    {
        var cup = new CupConfig(
            ChannelId: "MAJAK20ZC5A001",
            ChannelName: "Cup",
            DateFrom: DateTime.Now.AddDays(-1),
            DateTo: DateTime.Now.AddDays(1),
            IsFestive: true,
            CupId: 77);

        Assert.Equal(77, EnterChannelCommand.GetFestiveCupRatId("0ZC5A", cup));
        Assert.Null(EnterChannelCommand.GetFestiveCupRatId("0ZC5F", cup));
        Assert.Null(EnterChannelCommand.GetFestiveCupRatId("0ZG6A", cup));
        Assert.Null(EnterChannelCommand.GetFestiveCupRatId("0ZC5A", null));
    }

    [Fact]
    public void SelectSerialMastsSql_UsesLegacyStartDateGap()
    {
        var sql = (string)typeof(PlayerRepository)
            .GetField("SelectSerialMastsSql", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetRawConstantValue()!;
        var normalized = sql.Replace(" ", "").Replace("\r", "").Replace("\n", "");

        Assert.Contains("EVTCODEMAST", normalized);
        Assert.Contains("EVTGIFTMAST", normalized);
        Assert.Contains("A.SVCID=:svcId", normalized);
        Assert.Contains("A.EVTSTARTDT<=SYSDATE+1", normalized);
        Assert.Contains("A.EVTENDDT>=SYSDATE", normalized);
    }

    [Fact]
    public void CallCasualPointUpdMissionSql_MatchesLegacyProcedureShape()
    {
        var sql = (string)typeof(PlayerRepository)
            .GetField("CallCasualPointUpdMissionSql", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetRawConstantValue()!;
        var normalized = sql.Replace(" ", "").Replace("\r", "").Replace("\n", "");

        Assert.Contains("CALLCASUALPOINT.PC_UPDMISSION", normalized);
        Assert.Contains(":oszGameId", normalized);
        Assert.Contains(":onCondType", normalized);
        Assert.Contains(":onCondSubType", normalized);
        Assert.Contains(":oszMemberNo", normalized);
        Assert.Contains(":onCnt", normalized);
        Assert.Contains(":odtProcDt", normalized);
        Assert.Contains(":onRtnVal", normalized);
    }

    [Fact]
    public void SelectEvtCodeMastSql_MatchesLegacySingleRowShape()
    {
        var sql = (string)typeof(PlayerRepository)
            .GetField("SelectEvtCodeMastSql", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetRawConstantValue()!;
        var normalized = sql.Replace(" ", "").Replace("\r", "").Replace("\n", "");

        Assert.Contains("SELECTEVTCODE,EVTNO,EVTNAME,EVTDESC,SVCID,EVTTBLINFO,EVTSTARTDT,EVTENDDT", normalized);
        Assert.Contains("FROMEVTCODEMAST", normalized);
        Assert.Contains("EVTCODE=:vcEvtCode", normalized);
        Assert.Contains("EVTNO=:inEvtNo", normalized);
    }

}

