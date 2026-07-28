using MajakServer.Infrastructure;
using MajakServer.Models.Game;
using MajakServer.Models.Player;

namespace MajakServer.Services;

public static class CupPlayLicense
{
    public const int Success = 1;
    public const int LicenseOver = 30006;
    public const int LicenseBuyItem = 30008;
    public const int LicenseBuyItemFree = 30010;

    private const int CupMatchCntNoLimit = -1;
    private const int EvtBillingBefore = 1;
    private const int EvtBillingFree = 2;

    public static int Check(CupConfig? cup, MajakPlayer player)
    {
        if (cup is null) return Success;

        string subId = ExtractSubId(cup.ChannelId);
        return Check(subId, cup.MaxMatchCntLimit, cup.ConditionBilling, cup.EntryLimited, player);
    }

    public static int Check(GameRoom room, MajakPlayer player)
    {
        if (!room.IsCupChannel) return Success;

        return Check(room.SubId, room.CupMaxMatchCntLimit, room.CupConditionBilling, room.CupEntryLimited, player);
    }

    private static int Check(string subId, int maxMatchCntLimit, int conditionBilling, bool entryLimited, MajakPlayer player)
    {
        char cupType = subId.Length > 4 ? subId[4] : '\0';
        switch (cupType)
        {
            case 'A':
                if (maxMatchCntLimit != CupMatchCntNoLimit && maxMatchCntLimit <= player.CupRec.CupMatchCnt)
                    return LicenseOver;
                return Success;

            case 'F':
                if (maxMatchCntLimit != CupMatchCntNoLimit && player.CupEvtRec.MatchCnt >= maxMatchCntLimit)
                    return LicenseOver;

                if (conditionBilling == EvtBillingFree && !entryLimited)
                    return Success;

                if (conditionBilling == EvtBillingBefore || (conditionBilling == EvtBillingFree && entryLimited))
                {
                    if (conditionBilling == EvtBillingFree && entryLimited && player.CupEvtRec.EntryTitle != 0)
                        return Success;

                    if (!player.CupEvtRec.BuyItem)
                        return conditionBilling == EvtBillingBefore ? LicenseBuyItem : LicenseBuyItemFree;
                }
                else if (conditionBilling != 0 && !player.CupEvtRec.BuyItem)
                {
                    return LicenseBuyItem;
                }

                return Success;

            default:
                return Success;
        }
    }

    private static string ExtractSubId(string channelId)
        => channelId.Length >= 11 ? channelId[6..11] : channelId;
}