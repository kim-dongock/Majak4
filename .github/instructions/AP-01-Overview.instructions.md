# AP-01 ゲーム概要 / 麻雀4 (HgMajak2)

## 0. 作業時の注意

- ユーザーが明示的に依頼した場合を除き、検証・確認目的で `git diff` を実行しない。
- 変更後の確認は、対象テスト、`dotnet test` / `dotnet build`、`get_errors`、または対象ファイルの読み取り・検索で行う。

## 1. ゲームコンセプト

hange プラットフォーム上で動作するオンライン対戦麻雀ゲーム。
最大 4 人のリアルタイムマルチプレイヤー対戦。
レガシー Win32 クライアント (HgMajak2.exe) を React + Phaser 3 の Web クライアントに移植するプロジェクト。

---

## 2. 全体フロー

```
ログイン (hange クッキー認証 — login クッキー hangame= / hangametest=)
  ↓
ゲームスタート画面 (CMJSelGroupWnd)
  → 交流戦 / 段位戦 / 大会 / 牌譜再生 を選択
  ↓
ロビー選択 (CMJSelLobbyWnd)
  → 基本卓 / ハイ卓 / 練習卓 など卓種別を選択 + 在室人数確認
  ↓
チャンネル (ロビー) 画面 (CMajakChannelWnd)
  → ルーム一覧 + チャット + メンバーリスト
  ↓
ルーム待機 (CMJRoomWnd)
  → 座席配置 + ルールオプション確認 → OK で対局開始同意
  ↓
対局 (CMJGameWnd / Phaser GameScene)
  → 手牌 / 捨て牌 / 副露 / リーチ / ロン / ツモ
  ↓
局結果 (CMJKyoRes) → 半荘/東風 結果 (CMJHanRes)
  ↓
ロビーに戻る
```

---

## 3. チャンネル構成

| チャンネル種別 | SubID | 卓種別 |
|---|---|---|
| 交流戦 (自由) | 0082B / 0086B / 0085F / 0075B / 00T5A | 基本卓 / ハイ卓 / 練習卓 |
| 段位戦 | 0ZG6A〜0ZG6D / 0ZG7A〜0ZG7D | 通常卓 / 段位卓 / 高段位卓 / 十段位卓 |

---

## 4. ゲームルール (リーチ麻雀)

- **局数**: 東南戦 (半荘) / 東風戦 (東南戦の半分)
- **プレイヤー数**: 4 人
- **牌種**: 萬子 (1m-9m) / 索子 (1s-9s) / 筒子 (1p-9p) / 字牌 (東南西北白発中) 34 種
- **赤牌**: なし / 1 枚 / 2 枚 (オプション)
- **クイタン**: あり / なし (オプション)
- **ワレメ**: あり / なし (オプション)

### タイルコード (PaiCode — `m_nCode = (kind << 4) | number`)

| 種別 | kind | コード範囲 | スプライトフレーム |
|------|------|-----------|----------------|
| 萬子 | 0 | 0x01-0x09 | frame 0-8 |
| 索子 | 1 | 0x11-0x19 | frame 9-17 |
| 筒子 | 2 | 0x21-0x29 | frame 18-26 |
| 風牌 | 3 | 0x31-0x34 | frame 27-30 |
| 三元牌 | 3 | 0x35-0x37 | frame 31-33 |

---

## 5. 勝敗と報酬

- 局終了ごとに点棒を精算、半荘/東風 終了時に最終順位確定
- **場代 (gam_money)**: 卓種別に設定 (基本卓 500 円 / ハイ卓 3,000 円)
- **龍珠 (ryuju / ドラゴンジェム)**: 順位に応じて付与 (ハイ卓 1位 +5 等)
- **レーティング**: 段位戦のみ上下

---

## 6. レーティング / 称号システム

- レーティングポイントが閾値を超えると段位/称号が上昇
- 称号 (SLevel, `mj_sho_moji.png` 12 フレーム):
  `無一文 → ぴよぴよ → 金欠 → 庶民 → 中流 → 上流 → 富豪 → 大富豪 → 貴族 → 大臣 → 王様 → 大王様`
- `CMJLevelupDlg` で称号上昇を演出

---

## 7. アイテム / カスタム

- **麻雀コイン (gam_money)**: ゲーム内通貨。場代消費 / 無料補充 (1日1回)
- **ハンコイン**: 課金通貨 (HanCoin GSC API経由)
- **龍珠 (gem)**: 課金通貨2種目 (交換専用)
- **アイテムショップ** (`CItemShopDlg`): 保険 / 通常 / 期間限定アイテム
- **カスタムショップ** (`CMajakCustomDlg`): 牌デザイン / 背景 / キャラクター変更

---

## 8. 技術スタック

| 層 | 技術 |
|---|---|
| クライアント (アウトゲーム) | React 18 + TypeScript + Vite |
| クライアント (インゲーム) | Phaser 3.88 (WebGL/Canvas) |
| 通信 | SignalR WebSocket (`/hubs/majak`) |
| 認証 | Majak 認証 → `POST /auth/majak-login` |
| サーバー | ASP.NET Core + Oracle + MySQL |
| 環境 | production: `majak2.hange.jp` / alpha: `alpha-majak2.hange.jp` |

- アイテムマスターは `typing_item_mast` / `typing_item_change_t` テーブルで管理
- ショップ開放 → 購入 → インベントリ反映の流れで処理

---

## 8. プレイヤーデータ (typing_rat)

| データ | 内容 |
|--------|------|
| memberId | hange ユーザー ID (PK) |
| rating | 累積レーティング |
| nLevel / sLevel | ランク番号 / 称号文字列 |
| matchCount / winCount / defeatCount / drawCount | 対戦統計 |
| gam_money | グアラ (試合報酬) |
| game_t_point | T-Point (楽曲購入に使用) |
| premium_point | プレミアムポイント |
| missionExp | ミッション経験値 |
| clearMusicList | クリア済み楽曲リスト |
| joinDate / lastDate | 初回ログイン日 / 最終ログイン日 |

---

## 9. ゲームを構成するサービス一覧

| サービス | 役割 |
|----------|------|
| `TypingGameCommandService` | メインゲームコマンド処理 (楽曲選択・入力判定・リザルト) |
| `GameLogicService` | スコア計算・レベル計算・勝敗判定ロジック |
| `ShopCommandService` | アイテムショップ処理 |
| `MusicCommandService` | 楽曲情報取得・楽曲キャンセル |
| `RoomSessionManager` | ルーム/チャンネルのメモリ上セッション管理 |
| `MusicTokenService` | 楽曲ストリーム署名付きトークン発行・検証 |
| `ChannelRegistrationService` | 起動時チャンネル情報を DB から Redis に登録 |
| `HangameCookieDecryptor` | Hangame ログインクッキー復号・ユーザー ID 抽出 |

---

## 10. 関連ドキュメント

| ドキュメント | 内容 |
|-------------|------|
| AP-02-Security | Hangame 認証フロー・楽曲ファイル保護 |
| AP-03-Database-Schema | DB テーブル定義・シードデータ |
| AP-04-Architecture | チャンネルサーバー構成・Redis・WebSocket 接続フロー |
| AP-05-Protocols | WebSocket コマンドコード一覧 (tpgc コマンド) |
| AP-06-Resource | 静的リソース構成・公開ディレクトリ |
