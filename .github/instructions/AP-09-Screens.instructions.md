---
applyTo: "client/**,server/**"
description: "クライアント画面一覧 (アウトゲーム / インゲーム / ポップアップ)"
---

# AP-09 クライアント画面一覧

## 参照ソース

- `Majak4_legacy/client/client/HgMajak2/` — メインゲームクライアント (Majak3.exe)
- `Majak4_legacy/client/client/HgChnlM/` — チャンネルウィンドウ

---

## 1. レガシー ↔ 新 Web 実装 画面対応表

画面移植・監査では、まずこの表で **レガシー画面名 / レガシークラス / 新 Web 実装名** を対応させる。アウトゲームは機能・表示条件・イベント・通信を比較し、レイアウトは現行 Web のレスポンシブ UI に合わせる。インゲームは座標・画像を含めてレガシーと比較する。

### 1-1. アウトゲーム / ロビー / ルーム

| # | レガシー画面名 | レガシークラス | レガシーソース | 新 Web 実装名 | 新 Web ファイル | 状態 / 備考 |
|---|---|---|---|---|---|---|
| 1 | ダウンロード画面 | `CDownloadWnd` | `MJDownload.h/cpp` | `DownloadWnd` | `client/src/screens/outgame/DownloadWnd.tsx` | 移植対象 |
| 2 | メインフレーム | `CMajakFrame` | `MajakFrame.h/cpp` | `MajakFrame` / ルーティング | `client/src/components/MajakFrame.tsx`, `client/src/App.tsx` | Web では React ルート + 共通フレーム |
| 3 | チャンネルグループ選択 | `CMJSelGroupWnd` | `MJSelGroupWnd.h/cpp` | `ChannelGroupScreen` | `client/src/screens/outgame/ChannelGroupScreen.tsx` | 移植対象 |
| 4 | ロビー選択 | `CMJSelLobbyWnd` | `MJSelLobbyWnd.h/cpp` | `LobbySelectScreen` | `client/src/screens/outgame/LobbySelectScreen.tsx` | 移植対象 |
| 5 | チャンネル (ロビー) 画面 | `CMajakChannelWnd` | `MajakChannelWnd.h/cpp` | `LobbyScreen` | `client/src/screens/outgame/LobbyScreen.tsx` | ルーム一覧・チャット・メンバーリストを統合 |
| 6 | 交流戦チャンネル | `CMajakStadiumWnd` | `MajakStadiumWnd.h/cpp` | `LobbyScreen` / 未分離 | `client/src/screens/outgame/LobbyScreen.tsx` | 専用画面差分は個別確認が必要 |
| 7 | ミニチャンネルウィンドウ | `CMJMiniChannelWnd` | `MajakChannelWnd.h` | `MiniChannelWnd` | `client/src/screens/outgame/MiniChannelWnd.tsx` | 移植対象 |
| 8 | プレイヤー情報 | `CMJPlayerInfo` | `MJPlayerInfo.h/cpp` | `PlayerInfoWnd` | `client/src/screens/outgame/dialogs/PlayerInfoWnd.tsx` | ダイアログとして実装 |
| 9 | メンバーリスト | `CMJMemberListWnd` | `MJMemberListWnd.h/cpp` | `MemberListPanel` | `client/src/screens/outgame/LobbyScreen.tsx` | `LobbyScreen` 内部コンポーネント |
| 10 | メンバーフィルターリスト | `CMJMemberListFilter` | `MJMemberListWnd.h/cpp` | `MemberListPanel` | `client/src/screens/outgame/LobbyScreen.tsx` | フィルター UI を同コンポーネント内に実装 |
| 11 | ゲームリクエストダイアログ | `CMJGetReqGameDialog` | `MajakChannelWnd.h`, `HgGetReqGameDialog.cpp` | `GetReqGameDialog` | `client/src/screens/outgame/dialogs/GetReqGameDialog.tsx` | 招待承諾 / 拒否 |
| 12 | ルーム待機画面 | `CMJRoomWnd` | `MJRoomWnd.h`, `MJRoomWnd1-3.cpp` | `RoomScreen` | `client/src/screens/outgame/RoomScreen.tsx` | ルーム待機・OK・招待・退室 |
| 13 | ユーザー情報表示 | `DrawMemberInfo` 相当 | `MajakChannelWnd.h/cpp` 周辺 | `DrawMemberInfo` | `client/src/components/DrawMemberInfo.tsx` | ロビー内ユーザー情報パネル |

### 1-2. インゲーム / Phaser シーン

| # | レガシー画面名 | レガシークラス | レガシーソース | 新 Web 実装名 | 新 Web ファイル | 状態 / 備考 |
|---|---|---|---|---|---|---|
| 1 | インゲーム画面コンテナ | `CMJGameWnd` | `MJWindow.h`, `MJWindow1-2.cpp` | `GameScreen` / `GameInstance` | `client/src/screens/ingame/GameScreen.tsx`, `client/src/game/GameInstance.ts` | React コンテナ + Phaser 起動管理 |
| 2 | 画像・リソース初期化 | `CMJGameWnd` / 各描画クラス初期化 | `MJWindow*.cpp`, `MJTblDraw*.cpp` | `PreloadScene` | `client/src/scenes/PreloadScene.ts` | Phaser アセット preload |
| 3 | ゲームテーブル (牌 / UI) | `CMJTblGame` | `MJTblGame.h/cpp` | `GameScene` | `client/src/scenes/GameScene.ts` | 対局状態・牌操作の中心 |
| 4 | 牌情報レイヤー | `CMJTblPaif` | `MJTblPaif.h`, `MJTblPaif1-2.cpp` | `GameScene` | `client/src/scenes/GameScene.ts` | 手牌・捨て牌・副露を Phaser で描画 |
| 5 | ユーザー UI レイヤー | `CMJTblUser` | `MJTblUser.h`, `MJTblUser1-4.cpp` | `UIScene` | `client/src/scenes/UIScene.ts` | 点数・名前・局情報・タイマー |
| 6 | 履歴レイヤー | `CMJTblHist` | `MJTblHist.h/cpp` | `GameScene` | `client/src/scenes/GameScene.ts` | 局履歴・再生系は追加確認が必要 |
| 7 | 描画レイヤー | `CMJTblDraw` | `MJTblDraw.h`, `MJTblDraw1-5.cpp` | `GameScene` / `UIScene` | `client/src/scenes/GameScene.ts`, `client/src/scenes/UIScene.ts` | 卓・牌・プレイヤー UI に分割 |
| 8 | 観戦者リスト | `CMJViewerListWnd` | `MJViewerListWnd.h/cpp` | `ViewerListWnd` | `client/src/screens/ingame/ViewerListWnd.tsx` | ルーム / インゲーム右側パネル |
| 9 | 半荘 / 東風結果 | `CMJHanRes` | `MJHanRes.h/cpp` | `HanRes` | `client/src/screens/ingame/HanRes.tsx` | 終局結果 |
| 10 | 局結果 | `CMJKyoRes` | `MJKyoRes.h/cpp` | `KyoRes` | `client/src/screens/ingame/KyoRes.tsx` | 1局結果 |
| 11 | スライドアナウンス | `CMJSlideAnnounce` | `MJSlideAnnounce.h/cpp` | `SlideAnnounce` | `client/src/screens/ingame/SlideAnnounce.tsx` | お知らせ表示 |
| 12 | 牌譜 (リプレイ) 再生 | `CMJPaifWnd` | `MJPaifWnd.h/cpp` | `PaifWnd` | `client/src/screens/ingame/PaifWnd.tsx` | 牌譜再生 |

### 1-3. ポップアップ / ダイアログ

| # | レガシー画面名 | レガシークラス | レガシーソース | 新 Web 実装名 | 新 Web ファイル | 状態 / 備考 |
|---|---|---|---|---|---|---|
| 1 | スタートポップアップ (お知らせ) | `CMJStartPopupWnd` | `MJStartPopupWnd.h/cpp` | `StartPopupWnd` | `client/src/screens/outgame/dialogs/StartPopupWnd.tsx` | 移植対象 |
| 2 | サービス終了案内 | `CMJEndingPopupWnd` | `MJEndingPopupWnd.h/cpp` | `EndingPopupWnd` | `client/src/screens/outgame/dialogs/EndingPopupWnd.tsx` | 移植対象 |
| 3 | ウェルカムメッセージ | `CMJWelcomeDlg` | `MJWelcomeDlg.h/cpp` | `WelcomeDlg` | `client/src/screens/outgame/dialogs/WelcomeDlg.tsx` | × ボタン無し系 |
| 4 | オプション設定 | `CMJOptDlg` | `MJOptDlg.h/cpp` | `OptDlg` | `client/src/screens/outgame/dialogs/OptDlg.tsx` | クライアント設定 |
| 5 | 設定詳細タブ | `CMJCfgDlg` / `CMJCfgDlg2` / `CMJCfgDlg3` / `CMJCfgDlgEx` | `MJCfgDlg.h/cpp` | `CfgDlg` | `client/src/screens/outgame/dialogs/CfgDlg.tsx` | ルーム / ゲーム設定 |
| 6 | サークルオプション | `CMJCircleOptDlg` | `MJCircleOptDlg.h/cpp` | `CircleOptDlg` | `client/src/screens/outgame/dialogs/CircleOptDlg.tsx` | サークルルーム設定 |
| 7 | 牌譜選択 | `CMJSelPaifuDlg` | `mjselpaifu.h/cpp` | `SelPaifuDlg` | `client/src/screens/outgame/dialogs/SelPaifuDlg.tsx` | 牌譜選択 |
| 8 | 牌譜保存 | `CPaifuSaveDlg` | `PaifuSaveDlg.h/cpp` | `PaifuSaveDlg` | `client/src/screens/outgame/dialogs/PaifuSaveDlg.tsx` | 牌譜保存 |
| 9 | シリアルコード入力 | `CSerialCodeDlg` | `SerialCodeDlg.h/cpp` | `SerialCodeDlg` | `client/src/screens/outgame/dialogs/SerialCodeDlg.tsx` | シリアルボーナス |
| 10 | アイテムショップ | `CItemShopDlg` | `ItemShopDlg.h/cpp` | `ItemShopDlg` | `client/src/screens/outgame/dialogs/ItemShopDlg.tsx` | ショップ親画面 |
| 11 | アイテムポップアップ | `CItemPopupDlg` | `ItemPopupDlg.h/cpp` | `ItemPopupDlg` | `client/src/screens/outgame/dialogs/ItemPopupDlg.tsx` | 状況別案内 |
| 12 | カスタムショップ | `CMajakCustomDlg` | `MajakCustomDlg.h/cpp` | `CustomDlg` | `client/src/screens/outgame/dialogs/CustomDlg.tsx` | カスタムアイテムショップ |
| 13 | MP便利アイテム購入確認 | `CMJBuyItemDlg` | `MJBuyItemDlg.h/cpp` | `BuyHanCoinItemDlg` | `client/src/screens/outgame/dialogs/BuyHanCoinItemDlg.tsx` | クラス名は互換、表示と価格単位はMP |
| 14 | 龍珠交換アイテム購入確認 | `CMJBuyItemDlg2` | `MJBuyItemDlg2.h/cpp` | `BuyExchangeItemDlg` | `client/src/screens/outgame/dialogs/BuyExchangeItemDlg.tsx` | 龍珠交換の確認 |
| 15 | カスタムアイテム購入 | `CMJBuyCustomItemDlg` | `MJBuyCustomItemDlg.h/cpp` | `BuyCustomItemDlg` | `client/src/screens/outgame/dialogs/BuyCustomItemDlg.tsx` | カスタム購入確認 |
| 16 | アイテム購入最終確認 | `CMJConfirmItemDlg` | `MJConfirmItemDlg.h/cpp` | `ConfirmItemDlg` | `client/src/screens/outgame/dialogs/ConfirmItemDlg.tsx` | 有効期間 / 所持確認 |
| 17 | MPアイテム購入レシート | `CMJReceiptDlg` | `MJReceiptDlg.h/cpp` | `HanCoinReceiptDlg` | `client/src/screens/outgame/dialogs/HanCoinReceiptDlg.tsx` | クラス名は互換、表示はMP購入完了 |
| 18 | 交換系購入レシート | `CMJReceiptDlg2` | `MJReceiptDlg2.h/cpp` | `ExchangeItemReceiptDlg` | `client/src/screens/outgame/dialogs/ExchangeItemReceiptDlg.tsx` | 第2タイプレシート |
| 19 | カスタムレシート | `CMJCustomReceiptDlg` | `MJCustomReceiptDlg.h/cpp` | `CustomReceiptDlg` | `client/src/screens/outgame/dialogs/CustomReceiptDlg.tsx` | カスタム購入完了 |
| 20 | 抽選スロット | `CMJLotSlotDlg` | `MJLotSlotDlg.h/cpp` | `LotSlotDlg` | `client/src/screens/outgame/dialogs/LotSlotDlg.tsx` | 抽選演出 |
| 21 | 抽選結果 | `CMJLotResultDlg` | `MJLotResultDlg.h/cpp` | `LotResultDlg` | `client/src/screens/outgame/dialogs/LotResultDlg.tsx` | 抽選結果 |
| 22 | コイン獲得 | `CMJGetCoinDlg` | `MJGetCoinDlg.h/cpp` | `GetCoinDlg` | `client/src/screens/outgame/dialogs/GetCoinDlg.tsx` | × ボタン無し系 |
| 23 | ゲーム終了確認 | `CMJAskEndDlg` | `MJAskEndDlg.h/cpp` | `AskEndDlg` | `client/src/screens/outgame/dialogs/AskEndDlg.tsx` | 対局中離脱確認 |
| 24 | レベルアップ | `CMJLevelupDlg` | `MJLevelupDlg.h/cpp` | `LevelupDlg` | `client/src/screens/outgame/dialogs/LevelupDlg.tsx` | × ボタン無し系 |
| 25 | リード表示 | `CMJLeadDlg` | `MJLeadDlg.h/cpp` | `LeadDlg` | `client/src/screens/outgame/dialogs/LeadDlg.tsx` | × ボタン無し系 |
| 26 | イベント系ポップアップ | `CEventPointDlg` / `CEventCloseDlg` / `CEventIntroDlg` | `Event*.h/cpp` | `EventDialogs` | `client/src/screens/outgame/dialogs/EventDialogs.tsx` | イベント系を集約 |
| 27 | デバッグログイン | `CMJDebugLogin` | `MJDebugLogin.h/cpp` | `DebugLoginDlg` | `client/src/screens/outgame/dialogs/DebugLoginDlg.tsx` | 開発環境専用 |
| 28 | ミッション | レガシー未整理 | 要調査 | `MissionDlg` | `client/src/screens/outgame/dialogs/MissionDlg.tsx` | AP-05 mission コマンド側と合わせて別途レガシー特定 |

---

## 2. 画面遷移フロー

```
アプリ起動
  ↓
[ダウンロード画面] (CDownloadWnd) -- リソース最新化
  ↓
[スタートポップアップ] (CMJStartPopupWnd) -- お知らせ1回表示
  ↓
[メインフレーム] (CMajakFrame)
  ├─ [チャンネルグループ選択] (CMJSelGroupWnd)  ← レーティング進入制限適用 (AP-07 参照)
  │     ↓
  │   [ロビー選択] (CMJSelLobbyWnd)
  │     ↓
  │   [チャンネルロビー] (CMajakChannelWnd)
  │     ├─ ルーム一覧 / チャット / メンバーリスト
  │     ├─ [アイテムショップ] (CItemShopDlg)
  │     └─ [カスタムショップ] (CMajakCustomDlg)
  │           ↓ ルーム入室
  │         [ルーム待機] (CMJRoomWnd)
  │           ↓ 全員 OK
  └─ [インゲーム]
        ├─ [ゲームウィンドウ] (CMJGameWnd) + [テーブル] (CMJTblGame)
        │     ├─ [局結果] (CMJKyoRes)
        │     ├─ [半荘/東風結果] (CMJHanRes)
        │     ├─ [レベルアップ] (CMJLevelupDlg)       ← ポップアップ
        │     ├─ [リード表示] (CMJLeadDlg)            ← ポップアップ
        │     └─ [ゲーム終了確認] (CMJAskEndDlg)      ← ポップアップ
        └─ 対局終了 → [チャンネルロビー] に戻る
```

---

## 3. 実装注意点

### × ボタン無しダイアログ
以下は `CDialog` を継承しつつ × ボタンを非表示にしている。  
ゲーム進行を妨害する悪意ある操作 (×ボタン長押しによるフリーズ) を防ぐ設計。  
Web 移植時は `modal` / `z-index` 制御で同等の UX を実現すること。

- `CMJWelcomeDlg` — エラーメッセージ
- `CMJLevelupDlg` — レベルアップ演出
- `CMJLeadDlg` — リード表示
- `CMJGetCoinDlg` — コイン獲得

### 画面サイズ定数 (MJRoomWnd.h)
| 定数 | 値 | 意味 |
|---|---|---|
| `X_SIDEBAR` / `Y_SIDEBAR` | 794 / 31 | サイドバー左上座標 |
| `W_SIDEBAR` / `H_SIDEBAR` | 225 / 704 | サイドバーサイズ |
| ゲーム画面全体 | 1019 x 735 | インゲーム解像度 |

### カスタムアイテム種別 (MajakCustomDlg → AP-05 keyCustom*)
- `keyCustomBoard` (`mjkk134e`) — 背景板
- `keyCustomHai` (`mjkk135e`) — 牌デザイン
- `keyCustomCostume` (`mjkk136e`) — コスチューム
