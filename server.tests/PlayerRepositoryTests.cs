using MajakServer.Commands.Channel;
using MajakServer.Infrastructure;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL.Entities;

namespace MajakServer.Tests;

public class PlayerRepositoryTests
{
    [Theory]
    [InlineData("grade", GameConst.RatingGradeModeInit)]
    [InlineData("regular", 1400)]
    [InlineData("compete", 1400)]
    [InlineData("high_class", 1400)]
    public void GetInitialModeRating_UsesOfficialGradeRating(string modeCode, int expected)
    {
        Assert.Equal(expected, PlayerRepository.GetInitialModeRating(modeCode));
    }

    [Theory]
    [InlineData("grade", 1400, 0, true)]
    [InlineData("grade", 1400, 1, false)]
    [InlineData("grade", 1500, 0, false)]
    [InlineData("regular", 1400, 0, false)]
    public void ShouldRepairInitialGradeRating_OnlyRepairsUntouchedLegacyRows(
        string modeCode, int rating, uint matchCount, bool expected)
    {
        var stats = new Repositories.MySQL.Entities.PlayerModeStatsEntity
        {
            ModeCode = modeCode,
            Rating = rating,
            MatchCount = matchCount,
        };

        Assert.Equal(expected, PlayerRepository.ShouldRepairInitialGradeRating(stats));
    }

    [Fact]
    public void FilterGradeRankQuery_AlwaysUsesRequestedRankKind()
    {
        var month = new DateOnly(2026, 8, 1);
        var rows = new[]
        {
            new PlayerGradeRankEntity { RankDate = month, RankKind = 99, MemberNo = 1, GradeLevel = 10 },
            new PlayerGradeRankEntity { RankDate = month, RankKind = 10, MemberNo = 2, GradeLevel = 99 },
            new PlayerGradeRankEntity { RankDate = month, RankKind = 0, MemberNo = 3, GradeLevel = 0 },
            new PlayerGradeRankEntity { RankDate = month.AddMonths(-1), RankKind = 99, MemberNo = 4, GradeLevel = 10 },
        }.AsQueryable();

        var overall = PlayerRepository.FilterGradeRankQuery(rows, month, GameConst.RatingRankAll).ToList();
        var gradeZero = PlayerRepository.FilterGradeRankQuery(rows, month, 0).ToList();

        Assert.Equal(1UL, Assert.Single(overall).MemberNo);
        Assert.Equal(3UL, Assert.Single(gradeZero).MemberNo);
    }

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

