# Desktop Responsive Screen List

## Goal

Convert the desktop client from fixed-coordinate, image-heavy UI to responsive web controls one screen at a time. Keep gameplay assets that communicate game state (tiles, avatars, item art, board skins), but replace layout chrome and functional controls with HTML/CSS controls where practical.

## Common Foundations

| ID | Area | Current owner | Work |
|---|---|---|---|
| F-01 | Outer desktop canvas | `client/src/index.css` | Establish desktop width tiers, content max-widths, and a distinct outer background. |
| F-02 | Application frame | `client/src/components/MajakFrame.tsx` | Replace the fixed title-bar sprite layout with a responsive header and toolbar while retaining all existing commands. |
| F-03 | Shared player summary | `client/src/components/DrawMemberInfo.tsx`, `MobileUserSummary.tsx` | Unify desktop and mobile player summaries as responsive data-driven controls. |
| F-04 | Shared dialogs | `client/src/index.css`, dialog components | Define one responsive modal shell, command buttons, lists, tabs, fields, and confirmation layouts. |

## Main Screens

| Order | ID | Screen | Route / entry point | Primary file | Replace with web controls | Keep as assets | Status |
|---|---|---|---|---|---|---|---|
| 1 | O-01 | Channel group selection | `/channel` | `client/src/screens/outgame/ChannelGroupScreen.tsx` | Category actions, descriptions, exit command, player summary | Main title visual only | Desktop controls done |
| 2 | O-02 | Lobby selection | `/channel/select/:group`, `/channel/:channelId` | `client/src/screens/outgame/LobbySelectScreen.tsx` | Lobby list, filters, entry actions, back navigation | Rule and category icons only | Desktop controls done |
| 3 | O-03 | Channel lobby | `/channel/:channelId/lobby` | `client/src/screens/outgame/LobbyScreen.tsx` | Room list/grid, member list, filters, chat, commands, notices | Avatars, room rule icons, item/character art | Desktop controls done |
| 4 | O-04 | Room waiting | `/channel/:channelId/lobby/room/:roomId` | `client/src/screens/outgame/RoomScreen.tsx` | Seats, ready state, invitation, chat, room settings, leave action | Avatars, board/custom skin art, rule icons | Desktop controls done |
| 5 | O-05 | In-game shell | `/game/:roomId` | `client/src/screens/ingame/GameScreen.tsx` | Outer viewport, drawers, chat, auxiliary commands, overlays | Mahjong table, tiles, character art, board skin | Not started |
| 6 | O-06 | Replay | `/paifu`, `/paifu/:roomId` | `client/src/screens/ingame/PaifWnd.tsx` | Playback controls, hand navigation, information panels | Mahjong table, tiles, replay-specific art | Not started |
| 7 | O-07 | Download / startup | application bootstrap | `client/src/screens/outgame/DownloadWnd.tsx`, `client/src/App.tsx` | Progress and retry controls | Product logo | Not started |
| 8 | O-08 | Mini channel | invoked from lobby | `client/src/screens/outgame/MiniChannelWnd.tsx` | Compact room/member/chat views and commands | Avatars and state icons | Not started |

## In-game and Replay Overlays

| ID | Overlay | Primary file | Responsive conversion scope | Status |
|---|---|---|---|---|
| I-01 | Viewer list | `client/src/screens/ingame/ViewerListWnd.tsx` | Responsive side sheet / drawer and member rows | Not started |
| I-02 | Game invitation | `client/src/screens/ingame/GameInviteDialog.tsx` | Responsive dialog, member picker, request state | Not started |
| I-03 | Hand result | `client/src/screens/ingame/KyoRes.tsx` | Result layout, action controls, score rows | Not started |
| I-04 | Match result | `client/src/screens/ingame/HanRes.tsx` | Ranking/result layout and exit actions | Not started |
| I-05 | Announcements | `client/src/screens/ingame/SlideAnnounce.tsx` | Responsive announcement panel | Not started |
| I-06 | Game exit confirmation | `client/src/screens/outgame/dialogs/AskEndDlg.tsx` | Shared confirmation dialog | Not started |
| I-07 | Level / lead notices | `LevelupDlg.tsx`, `LeadDlg.tsx` | Shared non-dismissible progress notice shell | Not started |

## Outgame Dialog Groups

| ID | Group | Files | Responsive conversion scope | Status |
|---|---|---|---|---|
| D-01 | Session and account | `WelcomeDlg.tsx`, `EndingPopupWnd.tsx`, `RegistrationDlg.tsx`, `DebugLoginDlg.tsx` | Messages, registration, login and logout confirmations | Not started |
| D-02 | Room and player actions | `PlayerInfoWnd.tsx`, `GetReqGameDialog.tsx`, `RoomCreateDlg.tsx`, `CircleOptDlg.tsx`, `AccuseDlg.tsx` | Player cards, invitations, room forms, moderation controls | Not started |
| D-03 | Settings and options | `OptDlg.tsx`, `CfgDlg.tsx` | Tabbed settings forms and toggles | Not started |
| D-04 | Shop and collection | `ItemShopDlg.tsx`, `ResponsiveItemShopDlg.tsx`, `CustomDlg.tsx`, `CollectionDlg.tsx` | Catalog grid, tabs, selection, purchase flow | Not started |
| D-05 | Purchase flow | `BuyHanCoinItemDlg.tsx`, `BuyExchangeItemDlg.tsx`, `BuyCustomItemDlg.tsx`, `ConfirmItemDlg.tsx`, `ResponsiveShopTransactionDlg.tsx`, receipt dialogs | Price summary, confirmation, receipt states | Not started |
| D-06 | Missions, ranking, events | `MissionDlg.tsx`, `RankingDlg.tsx`, `EventDialogs.tsx`, `TournamentRegistDlg.tsx` | Lists, ranking tables, notices, registration forms | Not started |
| D-07 | Lottery and rewards | `LotSlotDlg.tsx`, `LotResultDlg.tsx`, `GetCoinDlg.tsx` | Result panels and acknowledgement actions; retain reward art | Not started |
| D-08 | Paifu and serial code | `SelPaifuDlg.tsx`, `PaifuSaveDlg.tsx`, `SerialCodeDlg.tsx` | Search/list/forms and save actions | Not started |
| D-09 | Startup notices | `StartPopupWnd.tsx`, `ItemPopupDlg.tsx` | Responsive notices and action buttons | Not started |

## Per-screen Acceptance Checklist

For each item, complete these before marking it done.

- [ ] List all image and sprite usages in the current component and its CSS.
- [ ] Classify every usage as functional control, game/state asset, or visual decoration.
- [ ] Replace functional controls and decoration with semantic HTML/CSS where appropriate.
- [ ] Preserve all existing REST, SignalR, navigation, keyboard, mouse, and touch behavior.
- [ ] Verify desktop narrow, desktop standard, wide desktop, iPad landscape, and mobile landscape layouts.
- [ ] Verify images remain undistorted through `object-fit`, `aspect-ratio`, and explicit size limits.
- [ ] Run the focused component test or type/lint check and record any intentionally retained assets.

## Initial Execution Order

1. F-01 through F-04: shared desktop responsive foundations.
2. O-01: channel group selection, as the first low-risk reference screen.
3. O-02 and D-01/D-03: navigation and common dialog patterns.
4. O-03: channel lobby, including room list, members, and chat.
5. O-04 and D-02: room workflow and player actions.
6. D-04 through D-09: transactional and auxiliary dialogs.
7. O-05, I-01 through I-07, and O-06: in-game and replay shells after the common responsive controls are established.