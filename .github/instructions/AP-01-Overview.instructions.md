---
applyTo: "client/**,server/**,server.tests/**,scripts/**,admin/**"
description: "麻雀4プロジェクトの目的、現行構成、認証・ID・データ・主要サービスの全体像を確認するときに参照する"
---

# AP-01 ゲーム概要 / 麻雀4 (HgMajak2)

## 0. 適用範囲と正本

- 本ドキュメント群はオンライン対戦麻雀ゲーム「麻雀4」だけを対象とする。
- 他ゲームのDB、通信コード、サービス、アセット、ゲームルールを根拠として持ち込まない。
- ゲーム仕様は AP-15、通貨は AP-16、DBは AP-03、通信は AP-05、セキュリティとIDは AP-02を正本とする。
- 旧Win32版は移植時の参考資料であり、認証、会員登録、課金、Web UIは現行ソースを優先する。
- ユーザーが明示的に依頼した場合を除き、検証・確認目的で `git diff` を実行しない。
- 変更後は対象テスト、診断、検索など、変更範囲に合った方法で検証する。

## 1. ゲームコンセプト

- 最大4人で対局するリアルタイムオンラインリーチ麻雀ゲーム。
- レガシーWin32クライアント `HgMajak2.exe` とC++サーバーを、React、Phaser 3、ASP.NET Coreへ移植する。
- アウトゲームは現行WebのレスポンシブUI、インゲームはレガシー対局画面と挙動を基準にする。

---

## 2. 全体フロー

```
認証 (Googleログインを基本とし、レガシーhange起動は互換経路として保持)
  ↓
未登録時は会員登録 (ニックネーム / 性別 / 出生年 / アバター / 規約同意)
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
- **場代 (GP / `game_money`)**: 卓種別に設定 (基本卓 500 GP / ハイ卓 3,000 GP)
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

- **GP (`game_money`)**: 公式Webマニュアルの「麻雀コイン」に相当するゲームマネー。場代、対局精算、無料補充、所持額称号に使用する。
- **MP (`cash_count`)**: 有料アイテム用のプレミアム通貨。現行は残高・消費・管理者調整を実装済みで、外部決済による直接購入は未実装。
- **龍珠 (`gem_count`)**: プレイ報酬・交換用通貨。MPとは別の残高として扱う。
- **アイテムショップ** (`CItemShopDlg`): 保険 / 通常 / 期間限定アイテム
- **カスタムショップ** (`CMajakCustomDlg`): 牌デザイン / 背景 / キャラクター変更

通貨名称と用途の正本は AP-16 を参照する。

---

## 8. 技術スタック

| 層 | 技術 |
|---|---|
| クライアント (アウトゲーム) | React 18 + TypeScript + Vite |
| クライアント (インゲーム) | Phaser 3.88 (WebGL/Canvas) |
| 通信 | SignalR WebSocket (`/hubs/majak`) |
| 認証 | Google ID Token、ゲームJWT、HttpOnly Refresh Cookie。レガシーhange Cookieは互換経路のみ |
| サーバー | ASP.NET Core / .NET 8 |
| 永続化 | MySQL 8 (ゲームDB・ログDBを分離) |
| キャッシュ | Redis |

---

## 9. プレイヤーデータとID

| 区分 | 正本 | 用途 |
|---|---|---|
| 内部会員ID | `player_account.member_no` | DB主キー、JWTの `sub` / `member_no`、サーバー内部処理 |
| 公開セッションID | `pix` | クライアント、SignalR、ロビー・ルーム内の公開識別 |
| アカウント | `player_account` | Google subject、表示名、性別、出生年、アバター、規約・承認状態 |
| 残高 | `player_wallet` | GP、MP、有償・無償MP、龍珠 |
| ゲーム状態 | `player_profile` | レーティング、経験値、称号、最終対局など |

- `member_no` は永続する内部IDであり、他プレイヤーや通常のクライアント表示へ公開しない。
- `pix` は `PlayerSessionService` が発行する非永続のランダムIDであり、DB主キーではない。
- 認証レスポンスの互換フィールド `memberNo` には内部IDではなく `pix` を返す。
- 詳細な信頼境界とライフサイクルは AP-02を参照する。

---

## 10. 主要サービス

| サービス | 役割 |
|---|---|
| `GameAuthTokenService` | ゲームJWTの発行・検証 |
| `AuthRefreshSessionService` | Refresh Tokenの発行・ローテーション・失効 |
| `PlayerSessionService` | SignalR接続、内部会員ID、`pix`、チャンネルの対応管理 |
| `GamePlayerRepository` | アカウント検索、Google会員登録、ログイン更新 |
| `PlayerRepository` | プロフィール、残高、称号、統計等のゲームDBアクセス |
| `GameLogicService` | 対局進行、和了、精算、ミッション・称号判定 |
| `RatingService` | レーティング、段位、資産レベル判定 |
| `GameMoneyService` | GPの作成・補充・履歴 |
| `ItemService` / `MajItemService` | MP・龍珠商品の購入、所持品、装着 |
| `MasterCacheService` | マスターデータのRedisキャッシュとDBフォールバック |

---

## 11. 関連ドキュメント

| ドキュメント | 内容 |
|-------------|------|
| AP-02-Security | Google認証、会員登録、JWT、Refresh Cookie、`member_no` / `pix`、課金セキュリティ |
| AP-03-Database-Schema | DB テーブル定義・シードデータ |
| AP-04-Architecture | チャンネルサーバー構成・Redis・SignalR接続フロー |
| AP-05-Protocols | 麻雀4のSignalR・REST通信契約 (`mjkc*e`, `smmc*e`) |
| AP-06-Resource | 静的リソース構成・公開ディレクトリ |
| AP-15-Official-Web-Manual | 公式Webマニュアルに基づくゲーム仕様 |
| AP-16-Currency-Economy | GP・MP・龍珠、商品、残高、購入の正本 |
