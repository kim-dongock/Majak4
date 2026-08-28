# Majak Currency Model

## Names and ownership

| Display name | Legacy source | Current wallet field | Purpose |
| --- | --- | --- | --- |
| Majak Cash | Hangame HanCoin | `cash_count` | Paid platform currency used to buy cash-shop products. `paid_cash_count` and `free_cash_count` preserve its paid/free provenance. |
| Dragon Gem | `MJKCOMMONRAT.GEMCNT` / `m_nGemCount` | `gem_count` | In-game reward and exchange currency. It is not HanCoin and has no paid/free split. |
| Game Money | `MJKCOMMONRAT.GAMMONEY` | `game_money`, `pending_game_money`, `earned_game_money` | In-game money used for table fees and item exchanges. |

## Majak Cash

- The legacy client calls the GSC HanCoin inquiry endpoint when the shop opens and calls the execute endpoint only to buy an item. See `GSHanCoin.cpp::InquiryHanCoin` and `CGSHanCoin::ExecuteHanCoin`.
- `PC_GMBSYS_MAJAKITEMBUY` checks HanCoin with `BILL.TOTALHCOINBALQ` and deducts it through `BILL.COINUSEIPREG`.
- The current replacement records this balance as Majak Cash. Administrator grants increase `free_cash_count`; deductions consume free cash before paid cash.
- Majak Cash is never a match, mission, or serial reward.

## Dragon Gem

- At game-result calculation, `HMajRoomServer::CalcPlayerResult` assigns a Dragon Gem reward from `GetGemCountToGet`; the result writer adds it to `MJKCOMMONRAT.GEMCNT`.
- `HMajChnlServer::ProcessCommand_RcvWeeklyReward` adds `MSN_RT_GEM` rewards to `m_nGemCount`.
- `HMajPlayer::AddPlayerResource` also adds serial/event `MSN_RT_GEM` rewards to `m_nGemCount`.
- `HMajDBObject::ExchangeItem` deducts `GEMCNT` together with game money when a player exchanges for an item.

## Game Money

- A match calculates the table fee (`m_nRoomCharge`) and subtracts it from the result money in `HMajRoomServer::CalcPlayerResult`.
- `PC_MAJAK2_HIST` credits purchased or awarded game money to `EARNEDMONEY` and writes `GAMEMONEYHIST`.
- HanCoin shop procedures, including `PC_GMBSYS_MAJAKITEMBUY` and `PC_GMBSYS_MAJAKCUSTOMITEMBUY`, grant the product's configured `GAMEMONEY` through `PC_MAJAK2_HIST` after a successful HanCoin payment.
- `HMajDBObject::ExchangeItem` also deducts game money when the exchange item has `llCostMoney > 0`.

## Compatibility rule

The legacy protocol key `mjkk55e` and the `gemcount` response field always mean Dragon Gem. Majak Cash uses the separate `cashCount` field and must not be substituted into legacy gem protocol fields.