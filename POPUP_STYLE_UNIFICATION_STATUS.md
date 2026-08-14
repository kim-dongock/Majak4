# Popup Style Unification Status

Last audited: 2026-08-14

## Shared Standard

- Overlay: `.majak-popup-overlay`
- Panel: `.majak-popup-panel`
- Title: `.majak-popup-titlebar`
- Scrollable content: `.majak-popup-body`
- Actions: `.majak-popup-actions`, with `.is-primary` for the main command
- Desktop, mobile landscape, and mobile portrait use the same semantic markup; CSS handles size and layout changes.
- The shared palette tokens in `client/src/index.css` also cover the independent responsive popup roots, panels, title areas, and command-button footers.

## Browser Preview

Open `http://127.0.0.1:5173/popup-preview` while the client Vite development server is running. The preview is development-only and uses deterministic dummy data.

| Popup | Browser preview | Fixture |
| --- | --- | --- |
| WelcomeDlg | Ready | Text-based welcome guidance |
| LevelupDlg | Ready | Level 7, 50,000 GP insurance |
| GetCoinDlg | Ready | Free GP replenishment notice |
| LeadDlg | Ready | Rating challenge eligibility notice |
| ItemPopupDlg | Ready | Three purchasable items and recommendation state |
| EndingPopupWnd | Ready | Logout confirmation |
| AskEndDlg | Ready | Timed continue/end confirmation |
| StartPopupWnd | Ready | Admin-selected maintenance notice |
| RoomCreateDlg / OptDlg / CfgDlg | Ready | Room and client setting fixtures |
| RankingDlg / PlayerInfoWnd | Ready | Ranking and player record fixtures |
| GetReqGameDialog / AccuseDlg | Ready | Incoming invitation and chat report fixtures |
| MissionDlg / TournamentRegistDlg | Ready | Live-data shell and registration form |
| SelPaifuDlg | Ready | Saved paifu record fixture |
| ResponsiveItemShopDlg / CollectionDlg | Ready | Balance fixture and collection loading state |
| HanCoinReceiptDlg / ExchangeItemReceiptDlg / CustomReceiptDlg | Ready | Completed purchase fixtures |
| LotSlotDlg / LotResultDlg | Ready | Lottery start and result fixtures |
| Nested purchase dialogs / legacy-only dialogs | Parent or pending | See lifecycle audit below |

## Lifecycle Audit

| Popup | Current trigger | Audit result |
| --- | --- | --- |
| StartPopupWnd | Channel group mount, only when an admin-selected startup article exists and the user did not dismiss it today | Expected. |
| WelcomeDlg | Lobby mount after the registration-complete session flag is consumed | Expected; it is intentionally one time per registration flow. |
| RoomCreateDlg, OptDlg, CfgDlg, RankingDlg, PlayerInfoWnd, MissionDlg, TournamentRegistDlg | Corresponding lobby or room command | Expected. |
| GetReqGameDialog | Incoming `c22e` invitation | Expected; it closes automatically after its 10-second reply timer. |
| AccuseDlg | `MAJAK_ACCUSE_EVENT`, only when the local chat log has a reportable other speaker | Expected. |
| SelPaifuDlg / PaifuSaveDlg | Paifu screen load/save commands | Expected. |
| LevelupDlg | No render/import usage outside `PopupPreviewScreen.tsx` | Missing trigger: level-up reward presentation is not connected to a game result or server event. |
| LeadDlg | No render/import usage outside `PopupPreviewScreen.tsx` | Missing trigger: first-place/lead presentation is not connected to a result event. |
| GetCoinDlg | No render/import usage outside `PopupPreviewScreen.tsx` | Missing trigger: successful free GP replenishment updates the balance but does not show its completion dialog. |
| ItemPopupDlg | No render/import usage outside `PopupPreviewScreen.tsx` | Missing trigger: GP/MP shortage recommendation is not connected to a rejected action or low-balance condition. |
| CircleOptDlg / SerialCodeDlg | No current import or render site | Unreachable. These are retained legacy feature shells, not active game flows. |
| EventDialogs | No current import or render site | Unreachable legacy event dialogs. |

The preview now contains the primary standalone layouts and the parent layouts that own nested purchase dialogs. Nested dialogs remain reachable through their production parent flows; fully isolating them requires fixture injection because they intentionally wait for live purchase responses.

## Completed

| Popup | Source status | Browser status | Notes |
| --- | --- | --- |
| RoomCreateDlg | Complete | Pending | Common overlay, panel, title, and action styles. |
| OptDlg / CfgDlg | Complete | Pending | Common overlay, panel, title, and action styles. |
| RankingDlg / CollectionDlg | Complete | Pending | Independent structure receives shared palette and command-button styles. |
| ResponsiveItemShopDlg / ResponsiveShopTransactionDlg | Complete | Pending | Shared palette applies to shop roots, panels, headers, and footers. |
| TournamentRegistDlg / SerialCodeDlg / PaifuSaveDlg / AskEndDlg | Complete | Pending | Shared semantic dialog structure and shared palette are active. |

## Source Work Complete

| Group | Popups | Source status | Browser status |
| --- | --- | --- |
| Invitations and player actions | GetReqGameDialog, PlayerInfoWnd, AccuseDlg, CircleOptDlg | Complete | Pending |
| Shop legacy views | ItemShopDlg, CustomDlg, ItemPopupDlg, ConfirmItemDlg | Complete | Pending |
| Purchase receipts | HanCoinReceiptDlg, ExchangeItemReceiptDlg, CustomReceiptDlg | Complete | Pending |
| Missions, events, rewards | MissionDlg, EventDialogs, LotSlotDlg, LotResultDlg, GetCoinDlg, LevelupDlg, LeadDlg | Complete | Pending |
| Paifu and serial tools | SelPaifuDlg, PaifuSaveDlg, SerialCodeDlg | Complete | Pending |
| Startup and account notices | StartPopupWnd, WelcomeDlg, EndingPopupWnd, DebugLoginDlg, AskEndDlg | Complete | Pending |

## Remaining Verification

- Browser checks at desktop, mobile landscape, and mobile portrait.
- Verify title, input, primary, secondary, disabled, hover, and focus styles.
- Do not mark a popup complete from source inspection alone.