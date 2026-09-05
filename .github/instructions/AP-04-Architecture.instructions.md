---
applyTo: "server/**,server.tests/**,client/**,scripts/**"
description: "麻雀4のサーバー構成、ゲームモード、チャンネル階層、SignalR接続、ロビー入場、性能・同時実行、サービス間責務を確認・変更するときに参照する"
---

# AP-04 アーキテクチャ / チャンネルサーバー構成

## 1. サービス識別子

```
GameId (SVCID):  MAJAK4
```

---

## 2. チャンネル階層

```
チャンネル (subid グループ)
  └── ロビー (chanelid: MAJAK20090A001 ～ など)
        └── ルーム (roomId: 対局部屋, メモリ上で管理)
```

- **チャンネル** は subid によるロビーグループの括り
- **ロビー** は `channel_master` テーブルの1行に対応する実体 (`channel_id` が一意ID)
- **ルーム** はロビー内でプレイヤーが作成する対局部屋。`PlayerSessionService` がメモリ上で管理

### subid グループ一覧
| subid パターン | 区分 | 判定条件 (レガシー) |
|--------------|------|-------------------|
| `0090A` | 自由チャンネル (一般) | — |
| `1090A` | 初心者チャンネル | `subid[0] == '1'` |
| `00R3A` | 上級者 / 段位チャンネル | — |
| `0Z90A` など | オートマッチングチャンネル | `subid[1] == 'Z'` |
| CUP 専用 | カップチャンネル | `CT_CUP` |
| TOURNAMENT 専用 | トーナメントチャンネル | `CT_TOURNAMENT` |
| CIRCLE 専用 | サークルチャンネル | `CT_CIRCLE` |

### チャンネルタイプ (chaneltype) 一覧
| 値 | 定数 | 内容 |
|----|------|------|
| 0 | `CT_NORMAL` | 通常チャンネル |
| 1 | `CT_FRIEND` | フレンドチャンネル |
| 2 | `CT_MATCH` | マッチチャンネル |
| 3 | `CT_RANKING` | ランキングチャンネル |
| 5 | `CT_COMPETE` | 競技チャンネル |
| 6 | `CT_HGDP` | HangameDP チャンネル |
| 7 | `CT_ARIARI` | アリアリルールチャンネル |
| 8 | `CT_TRAINING` | 練習チャンネル |
| 100 | `CT_CUP` | カップチャンネル |
| 101 | `CT_GRADE` | 段位チャンネル |
| 102 | `CT_TOURNAMENT` | トーナメントチャンネル |
| 103 | `CT_CIRCLE` | サークルチャンネル |

---

## 3. チャンネル設定 (DB + appsettings)

### channel_master テーブル (MySQL)
| 列 | 内容 |
|----|------|
| `channel_id` | ロビーの一意ID (PK)。例: `MAJAK20090A001` |
| `game_id` | `MAJAK4` 固定 |
| `sub_id` | チャンネルグループ (例: `0090A` / `1090A` / `00R3A`) |
| `channel_name` | 表示名 |
| `max_member` | ロビー定員 (デフォルト100) |
| `max_room` | ルーム上限数 (デフォルト48) |
| `channel_type` | チャンネルタイプ (上表参照) |
| `environment` | 対象環境 (dev / alpha / prod) |
| `server_url` | このロビーを担当するサーバーの URL |

- `max_room` はクライアント表示だけの値ではなく、通常作成・オートマッチング・トーナメントを含む新規ルーム作成のサーバー側上限である。
- 新規作成は `RoomRegistryService.TryRegisterRoomAsync()` で Redis 上の有効ルームを数え、上限確認と登録を原子的に行う。複数サーバーから同時に作成しても `max_room` を超えてはならない。
- 管理画面で `max_room` を変更した場合はチャンネルマスターキャッシュを即時無効化する。上限を現在ルーム数より小さくした場合、既存ルームは維持し、新規作成だけを現在数が上限未満になるまで拒否する。
- `c1e` と `c12e` は構造化応答に `maxRoom` と `currentRoomCount` を含める。クライアントは満室時に作成操作を無効化するが、最終判定は常にサーバーが行う。

### appsettings 設定キー
```json
"ConnectionStrings": {
  "GameDatabase": "...", // MySQLゲーム・プレイヤー・マスターDB
  "LogDatabase":  "..."  // MySQL追記型ログDB
},
"GameSettings": {
  "TestEnvironment":    false,
  "TrainingAiLevel":    "Advanced", // 旧クライアント用fallback。新規練習ルームは作成時の選択を優先
  "DefaultRoomRate":    1,
  "RoomChargeDefault":  200,   // 通常チャンネルの室料
  "RoomChargeGrade":    100    // 段位チャンネルの室料
},
"ChannelServerSettings": {
  "ServerUrl": "http://localhost:5246",
  "LobbySessionLeaseSeconds": 90
},
"RuntimeFlag": {
  "SecureLaunching": true,
  "Hangame2005":     false,
  "BipSupport":      true,
  "GameLog":         true,
  "Mrs":             true,
  "MrsV3":           true,
  "LcsApi":          true,
  "SecureSocket":    true,
  "NetCafeIpCheck":  true
}
```

### 練習卓 NPC / 推奨打牌 (Web 拡張)

- 練習卓の作成時に `trainingAiLevel=Legacy|Advanced` を選択し、`GameRoom.TrainingAiLevel` に保持する。通常卓ではこの値を無視する。
- `Advanced` はシャンテン、受け入れ、牌価値、対リーチ危険度で打牌し、チー／ポンは次の必須打牌まで評価してシャンテンが厳密に改善する場合だけ行う。
- 未指定時は `GameSettings:TrainingAiLevel` をfallbackとして使い、既定は `Advanced` とする。
- 練習卓の人間プレイヤーには、現在の `actionSeq` に対応する高性能評価の推奨打牌を点滅表示する。推奨は操作を強制せず、通常の有効打牌検証を変更しない。

---

## 4. レガシーサーバークラス構成

```
HMajRootServer               ルートサーバー (1 プロセス)
  └── HMajChnlServer         チャンネルサーバー (chanelid ごとに 1 インスタンス)
        └── HMajRoomServer   ルームサーバー (roomId ごとに 1 インスタンス)
```

### レガシーのタイマー定義 (HMajDef.h より)
| タイマー ID | 間隔 | 担当 | 内容 |
|------------|------|------|------|
| [401] `TIMERID_MAJANG_GAMECLEARCOUNTER` | 60s | RootServer | ゲームクリアカウンター |
| [402] `TIMERID_MAJANG_GRADERANKING_DAYLYTIMER` | 300s | RootServer | 段位ランキング日次更新 |
| [403] `TIMERID_MAJANG_GRADERANKING_MONTHLYTIMER` | 300s | RootServer | 段位ランキング月次更新 |
| [404] `TIMERID_MAJANG_CLRSERIALMAST_TIMER` | 3600s | RootServer | シリアルマスタークリア |
| [501] `TIMERID_MAJANG_AUTOMATCHING` | 3s | ChnlServer | オートマッチング処理 |
| [502] `TIMERID_MAJANG_CHNLCTRL` | 60s×n | ChnlServer | カップチャンネル制御 |
| [503] `TIMERID_MAJANG_TOURNAMENT_LIMITTIMER` | 300s | ChnlServer | トーナメント参加者リスト更新 |
| [504] `TIMERID_MAJANG_TOURNAMENT_MANAGETIMER` | 60s | ChnlServer | トーナメントマッチング管理 |
| [505] `TIMERID_MAJANG_TOURNAMENT_PLAYTIMER` | 10s | ChnlServer | トーナメント部屋作成・開局開始 |
| [601] `TIMERID_MAJANG_FAILEROOM` | 5s | RoomServer | 切断プレイヤーのルームチェック |
| [602] `TIMERID_MAJANG_EVENTMANAGER` | 1s | RoomServer | イベント管理 |
| [603] `TIMERID_MAJANG_FORCEEXITROOM` | 60s | RoomServer | トーナメント強制退出 |

---

## 5. .NET サーバーの対応クラス

| レガシー | .NET 実装 |
|--------|----------|
| `HMajChnlServer` + `HMajRoomServer` | `MajakGameHub` (統合 Hub) |
| `HMajPlayer` (メモリ上プレイヤー) | `MajakPlayer` + `PlayerSessionService` |
| タイマー [501] AutoMatching | `AutoMatchingBackgroundService` |
| タイマー [502] ChnlCtrl (Cup) | `CupChannelBackgroundService` |
| タイマー [503〜505] Tournament | `TournamentBackgroundService` |
| `HMajDBObject` (レガシーDBアクセス) | `PlayerRepository` / `TournamentRepository` 等のMySQL Repository |
| ルームセッション管理 | `PlayerSessionService._rooms` (ConcurrentDictionary) |

---

## 6. SignalR接続フロー

### 接続エンドポイント
```
ws://{host}:{port}/hubs/majak
```
クライアントは SignalR クライアントライブラリ経由で接続する。

### 接続からゲームまでのシーケンス

```
クライアント                           サーバー (MajakGameHub)
    |                                         |
  |── SignalR接続 + access_token ──────────>|  OnConnectedAsync: JWT検証
  |                                         |  member_no / pix / ConnectionIdを対応付け
    |                                         |
  |── SendCommand("c1e", {pix, ...}) ─────>|  JWTのmember_noを正本としてDBロード
  |                                         |  pix整合性検証 + Groups.AddToGroupAsync
    |<── channel:entered                      |  rooms[], members[], 所持金, レベル等を返す
    |<── channel:member_joined (他メンバーへ)  |  旧チャンネルがあれば自動退出
    |                                         |
    |  (ロビー滞在中: mjkc コマンド送受信)      |
    |                                         |
    |── SendCommand("mjkc19e", data) ─────────|  RoomEnterRoom → ルーム入室
    |<── room:entered (ルーム全員へ)           |  Groups.AddToGroupAsync("room_{roomId}")
    |                                         |
    |  (対局中: mjkc コマンド送受信)            |
    |                                         |
    |── SendCommand("mjkc5de", data) ─────────|  GamePlay → 打牌・鳴き・アガリ等
    |<── room:game_state (ルーム全員へ)        |
    |                                         |
    |── 切断 ─────────────────────────────────|  OnDisconnectedAsync: 自動クリーンアップ
    |                                         |  Waiting 状態のルームなら AutoExitRoom 通知
    |                                         |  チャンネルメンバーから削除
```

### SignalR グループ命名規則
| グループ名 | 対象 |
|-----------|------|
| `chanel_{chanelId}` | そのチャンネルにSignalR接続中の全プレイヤー (ルーム内含む) |
| `room_{roomId}` | ルーム内の全接続 |

> **設計方針 (確定)**: ロビー入室時にSignalRを接続する。WebSocketは利用可能な場合のトランスポートであり、アプリケーション境界はSignalR Hubとする。
> `chanel_*` グループにはロビー滞在中も含む全プレイヤーが参加する。
> オートマッチング等のリアルタイムプッシュはこの接続を通じて配信される。

### OnDisconnectedAsync の自動クリーンアップ
切断時は明示的な退出コマンドがなくても自動的に実行される:
1. `PlayerSessionService.GetByConn(ConnectionId)` でプレイヤーを特定
2. ルームが `Waiting` 状態なら `AutoExitRoom` (mjkc5e) をルーム全員へ送信
3. `Groups.RemoveFromGroupAsync("room_{roomId}")` でルームグループから削除
4. チャンネルグループから削除

---

## 7. ゲームコマンド処理フロー

クライアントは `SendCommand(commandCode, data)` で任意のゲームコマンドを送信する。

```
クライアント                    MajakGameHub              ICommand 実装
    |── SendCommand(code, data) ─>|── DispatchCommand() ──>|
    |                             |                         |── ChannelLifecycleCommands
    |                             |                         |── MatchingCommands
    |                             |                         |── MoneyCommands
    |                             |                         |── GamePlayCommand
    |                             |                         |── AgariRecCommand
    |                             |                         └── TournamentCommands 等
    |<── レスポンス (Caller/Group) |<── response ────────────|
```

コマンドコードは `mjkc{hex}e` 形式 (レガシー ProcessCommand_* 対応)。
詳細は AP-05-Protocols を参照。

---

## 8. マルチサーバー構成・Redis 負荷分散

### 8-1. 設計方針 (確定)

| 項目 | 内容 |
|------|------|
| **チャンネル** | MySQLゲームDBのカテゴリ情報。特定サーバーに固定しない |
| **チャンネル→サーバー割り当て** | **MySQL固定割り当て** — `channel_master.server_url` を正本とし、管理画面から更新する。`channel:{chanelId}:server` は実行時所有リースとして使用する |
| **ロビー (チャンネル画面)** | **SignalR接続あり** (レガシー設計準拠)。`GET /api/channel/{id}/server` → DB割り当て先がRedis heartbeat上で生存している場合だけURL取得 → SignalR接続 |
| **ルーム入室/作成** | ロビーのSignalR接続を再利用 (同一サーバーなら再接続不要) |
| **チャンネルユーザーリスト** | Redis HASH (`channel:{chanelId}:members`) で管理。複数サーバー間で共有 |
| **ルームリスト** | Redis TTL (30秒) で管理。ゲームサーバーが書き込み、8秒ごとに現在状態を再登録 |
| **ルーム作成時サーバー選択** | `channel_master.server_url` で割り当て済みのロビー接続先をそのまま使用する。Redis で別サーバーへ動的に振り分けない |
| **自動スケールアウト** | 新サーバーが起動すると 8秒後に Redis に自動登録 → 即座に選択対象に追加 |

### 8-2. Redis データ構造

| キー | 型 | TTL | 書き込み主体 | 内容 |
|------|----|-----|-------------|------|
| `channel:{chanelId}:members` | HASH | **90s** | REST / SignalR同期 | HASH fieldはサーバー内部識別子、公開JSONの `memberNo` / `pix` は `pix` |
| `channel:{chanelId}:server` | STRING | **60s** | `ServerLoadService.ClaimChannelAsync()` | DB割り当て先サーバーの実行時所有リース。ルーティングの正本ではない |
| `game:server:channelcounts` | HASH | なし | `ServerLoadService` | serverUrl → 担当チャンネル数 |
| `room:{chanelId}:{roomId}` | STRING | **30s** | `MajakGameHub` + `ServerStatusBackgroundService` | チャンネルごとのJSONルーム情報 (serverUrl 含む)。roomIdはチャンネル間で重複するため単独キーにしない |
| `channel:{chanelId}:rooms` | SET | なし | `MajakGameHub` | roomId の集合 (TTL 切れ roomId は自動掃除) |
| `game:servers` | ZSET | なし | `ServerStatusBackgroundService` (8秒ごと) | score = lastSeenUnixTime |
| `game:server:roomcounts` | HASH | なし | `ServerStatusBackgroundService` (8秒ごと) | serverUrl → roomCount |

### 8-3. ゴーストルーム防止

```
通常時 (8秒ごと):
  ServerStatusBackgroundService
    → SET room:{chanelId}:{roomId} EX 30   ← 全アクティブルームの現在状態と TTL を同期

サーバークラッシュ時:
  TTL 更新が止まる → 最大 30 秒後にルームエントリが自動消滅

グレースフルシャットダウン時 (即座):
  ApplicationStopping フック
    → KeyDelete room:{chanelId}:{roomId} (全ルームを即削除)
    → SortedSetRemove game:servers (このサーバーを即除外)
    → HashDelete game:server:roomcounts

ルーム作成/削除時 (即座):
  MajakGameHub.CreateRoom → RegisterRoomAsync  (Redis に書き込み)
  MajakGameHub.LeaveRoom  → RemoveRoomAsync    (空ルームを即削除)
  OnDisconnectedAsync     → RemoveRoomAsync or UpdateMemberCountAsync
```

### 8-4. クライアント接続フロー

```
ChannelSelectScreen          REST API
    |── GET /api/channels ──>|  MySQLチャンネルマスターから取得
    |<── [{chanelId, ...}]   |

LobbyScreen (レガシー設計準拠: ロビー入室時にSignalR接続)
    |── GET /api/channel/{id}/server ─>|  MySQL channel_master.server_url から担当サーバー URL 取得
    |                                  |  game:servers の30秒以内heartbeatがなければ503で接続拒否
    |── SignalR.connect(serverUrl, JWT)>|  JWT検証 + Hub接続
    |── send("c1e", {pix, ...}) ─────>|  JWTのmember_noでDBロード + chanel_*登録
    |<── channel:entered             |  Redis リース登録 (ClaimChannelAsync)
    |── POST /api/channel/{id}/enter >|  Redis メンバーリスト登録
    |── GET  /api/channel/{id}/rooms >|  ルーム一覧 (3秒ポーリング)
    |── GET  /api/channel/{id}/members>|  メンバー一覧 (3秒ポーリング)
    |── mjkc2e (オートマッチ) ────────>|  キュー登録 → 3秒ごとデーモンがチェック
    |<── mjkc2e (マッチ完了プッシュ)    |  4人揃ったら即座にプッシュ通知
    |── ロビー退出: SignalR.disconnect()
    |── POST /api/channel/{id}/leave >

    ↓ ユーザーが「ルーム作成」をクリック
    |── 接続中ロビーの serverUrl を再利用  |  DB 割り当て済みサーバーから移動しない

  RoomScreen (SignalR接続を再利用)
    |── SignalR.connect(serverUrl) ───>|  同一 URL ならスキップ (ロビー接続を再利用)
    |── invoke("CreateRoom", ...) ────>|  ルーム作成
    |<── room:created { result:1, roomId }|

    ルーム入室モード:
    |── send("room:enter", { roomId }) >|
    |<── room:enter { result:1, ... }   |
```

### 8-5. チャンネル担当サーバーの決定

```
GET /api/channel/{chanelId}/server
  ① MySQL channel_master から公開状態と server_url を直接取得
  ② Redis game:servers で server_url の30秒以内 heartbeat を確認
  ③ 生存している場合だけ server_url を返す
  ④ Redis 利用不可、heartbeatなし、非公開の場合は503を返す
```

ルーム作成時はロビー接続中の同じ `serverUrl` を再利用する。Redis のルーム数を使って別サーバーを選択してはならない。

### 8-6. サーバー追加時の動作

新しいゲームサーバーは起動時に自身の実行状態だけを登録する。チャンネル割り当ては自動変更しない:
1. 新サーバーが起動 → `ServerStatusBackgroundService` が動作開始
2. 最初のループで直ちに `ZADD game:servers {now} {newServerUrl}` + `HSET game:server:roomcounts ... 0`
3. 管理画面で対象チャンネルの `channel_master.server_url` を新サーバーへ変更する
4. チャンネルマスターキャッシュと旧実行リースを無効化し、次回入場から新サーバーを使用する

### 8-7. REST API 一覧

| メソッド | パス | 説明 |
|---------|------|------|
| `GET`  | `/api/channels` | チャンネル一覧 (MySQLゲームDB) |
| `GET`  | `/api/channel/{chanelId}/server` | DB 割り当て先が Redis heartbeat 上で生存している場合に URL を返す |
| `POST` | `/api/channel/{chanelId}/enter` | ロビー入室 (Redis 登録) |
| `POST` | `/api/channel/{chanelId}/leave` | ロビー退室 (Redis 削除) |
| `GET`  | `/api/channel/{chanelId}/members` | チャンネルメンバー一覧 (Redis) |
| `GET`  | `/api/channel/{chanelId}/rooms` | ルーム一覧 (Redis TTL) |

### 8-8. 環境別構成

| 環境 | `ASPNETCORE_ENVIRONMENT` | Parameter Store path | 公開API URL |
|------|--------------------------|----------------------|-------------|
| 開発 | `Development` | `/config/application_development/majak4` | `http://localhost:5246` |
| アルファ | `Alpha` | `/config/application_alpha/majak4` | `https://alpha-app-majak4.hange.jp` |
| 本番 | `Production` | `/config/application_production/majak4` | `https://app-majak4.studio35app.net` |

- hange Alphaのクライアント公開URLは`https://alpha-game-majak4.hange.jp`、API公開URLは`https://alpha-app-majak4.hange.jp`として分離する。
- `alpha-game-majak4.hange.jp`はCloudFrontからS3バケット`alpha-game-majak4-hange-jp`を配信し、API・認証・SignalR通信は`alpha-app-majak4.hange.jp`へ直接接続する。
- `ParameterStoreJsonConfiguration`がサービス登録前に環境別SecureStringを復号し、JSONをconfigurationへ追加する。取得失敗、空値、不正JSONでは起動を失敗させる。
- DB、Redis、JWT、Google認証、CORS、Paifu等のruntime値はParameter Store JSONを正本とする。
- deploy scriptはSSH server、user、key path、domain、port、remote path等の配備情報だけを保持し、DB・Redis・JWT等をsystemdへ展開しない。

#### クライアント Vite 環境変数 (`VITE_API_BASE_URL`)
| 環境 | `.env` ファイル | 値 | ビルドコマンド |
|------|---------------|-----|--------------|
| 開発 | `.env.development` | `` (空) | `vite dev` |
| アルファ | `.env.alpha` | `https://alpha-app-majak4.hange.jp` | `vite build --mode alpha` |
| 本番 | `.env.production` | `https://app-majak4.studio35app.net` | `vite build --mode production` |

### 8-9. 開発環境セットアップ

```bash
# Studio35 profileからDevelopment用Parameter Store JSONを取得して起動
.\server\run-development.ps1

# hange profileを使う場合 (必要に応じて -AspNetCoreEnvironment Alpha を追加)
.\server\run-development.ps1 -AwsProfile hange_majak4

# クライアント起動
cd client
npm run dev
```

DevelopmentでもDB・Redis接続先は`/config/application_development/majak4`を正本とする。DevelopmentのRedis接続先にはAlphaとは分離されたRedisを設定する。Redisが利用できない場合、一部のsession/leader機能はローカルfallbackで動作するが、Redis依存の分散動作を検証したことにはならない。

---

## 9. バックグラウンドサービス一覧

全サーバーは API サーバー兼ゲームサーバーとして動作する。
バックグラウンドサービスの一部は `PrimaryLeaderService` によるリーダー選出で実行制御される。

### 9-1. サービス一覧

| クラス | 間隔 | Primary のみ | レガシー相当 | 概要 |
|--------|------|:---:|------|------|
| `AutoMatchingBackgroundService` | **3秒** | ❌ (全サーバー) | `TIMERID_MAJANG_AUTOMATCHING` | 自サーバーのオートマッチングキューを走査し、4人揃ったらルームを作成して `mjkc2e` を送信 |
| `CupChannelBackgroundService` | **1分** | ✅ | `TIMERID_MAJANG_CHNLCTRL` | カップチャンネルの開始/停止状態遷移を管理。DB 更新と `mjkc13e` 送信を行う |
| `TournamentBackgroundService` | **30秒** | ✅ | `TIMERID_MAJANG_TOURNAMENT_*` | トーナメントの PreMatching → GoMatching → PostMatching を統合処理 |
| `ServerStatusBackgroundService` | **8秒** | ❌ (全サーバー) | — | 自サーバーのルーム数を Redis に登録。全ルームの TTL をリフレッシュ。**リーダーロック更新も担当** |

### 9-2. Redis リーダー選出 (`PrimaryLeaderService`)

プライマリサーバーは起動時に決定するのではなく、**Redis の SETNX ロックで動的に選出**する。
サーバーが落ちると最大 30 秒で別サーバーが自動昇格する。

```
Redis キー: "majak4:primary-leader"
Redis 値:   ServerUrl + machine name + process ID + GUID のプロセス固有ID
TTL:        30 秒
更新間隔:   8 秒 (ServerStatusBackgroundService 内)
```

#### ライフサイクル

```
起動時
  └─ SETNX "majak4:primary-leader" = process-specific serverId (TTL 30秒)
       ├─ 取得成功 → IsLeader = true  (プライマリ)
       └─ 取得失敗 → IsLeader = false (セカンダリ)

8秒ごと (ServerStatusBackgroundService.TryAcquireOrRenewAsync)
  ├─ IsLeader=true  : 自分の値ならTTL延長 / 値が異なれば IsLeader=false に降格
  └─ IsLeader=false : NX で取得試行 → 成功したら IsLeader=true に昇格

グレースフルシャットダウン (PrimaryLeaderService.Release)
  └─ Lua atomic でキーを即削除 → 別サーバーが TTL 待たず即昇格
```

#### バックグラウンドサービスの制御方法

```csharp
// CupChannelBackgroundService / TournamentBackgroundService の tick ループ
if (!_leader.IsLeader)
{
    _logger.LogDebug("not leader, skip.");
    continue;   // tick をスキップするだけ。スレッドは起動し続ける
}
```

旧実装 (`IsPrimaryServer=false` で即 `return`) と異なり、
**タイマーループは全サーバーで動き続ける**。
リーダーが変わったタイミングで自動的に次の tick から処理が始まる。

#### Redis 未接続時のフォールバック

Redis に接続できない場合 (開発環境など) は
`appsettings.json` の `IsPrimaryServer` フラグを使用する。

```json
"ChannelServerSettings": {
  "ServerUrl": "https://app-majak4.studio35app.net",
  "IsPrimaryServer": true   // Redis 未接続時のフォールバック値
}
```

- 本番環境では Redis が必ず起動していること
- `IsPrimaryServer` は **Redis 未接続時の開発用フォールバック** であり、
  本番マルチサーバー構成でのプライマリ制御には使用しない

#### なぜ Primary 制御が必要か
- `CupChannelBackgroundService`: `_states` がインメモリのため、複数サーバーが同時実行すると DB 更新・`mjkc13e` パケット送信が重複する
- `TournamentBackgroundService`: `TournamentService._plans` がインメモリ Singleton のため、複数サーバーで同時実行するとマッチング・結果処理が重複する
- レガシー相当: `HMajChnlServer` が `CHANELMAST.MACHINE` で自サーバー担当チャンネルのみタイマーを起動していた

#### `AutoMatchingBackgroundService` が全サーバーで実行される理由
- マッチングキューは `PlayerSessionService` (インメモリ) で管理されるため、各サーバーは自分に接続しているプレイヤーのキューのみ処理する
- サーバーをまたいだ重複処理が構造的に発生しない

#### `ServerStatusBackgroundService` が全サーバーで実行される理由
- 各サーバーが**自分自身の**ルーム数と生存確認を Redis に登録するのが目的
- 全サーバーが実行しなければ DB で割り当てられた接続先の生存確認と障害検知が機能しない

### 9-3. グレースフルシャットダウン

`ServerStatusBackgroundService` は `IHostApplicationLifetime.ApplicationStopping` フックで以下を即座に実行する:

```
ApplicationStopping フック (同期):
  1. UnregisterSelfAsync(serverUrl)           → game:servers / game:server:roomcounts から削除
  2. RemoveAllRoomsAsync([(roomId, chanelId)]) → 全 room:{chanelId}:{roomId} キーを削除 (ゴーストルーム防止)
```

サーバークラッシュ時は TTL 更新が止まり、最大 30 秒後にルームエントリが自動消滅する。

---

## 10. ゲームモード別ランタイム契約

### 10-1. 練習卓

- 空席は練習NPCとして扱い、roomで選択した`Legacy`または`Advanced` AIを使う。新規roomの既定は`Advanced`とする。
- 高性能AIのチー／ポンは、次の必須打牌まで評価してシャンテンが厳密に改善する場合だけ行う。
- 人間プレイヤーの推奨打牌は現在の`actionSeq`にだけ有効で、通常の入力検証を変更しない。
- 練習結果はGP、龍珠、rating、grade、通常戦績、称号、result missionを変更せず、通常history・training history・replay paifu archiveへ保存しない。
- 保存や精算を行わなくても、接続中ユーザーには通常の開始・action・result packet順序を維持する。

### 10-2. 交流戦・段位戦

- `0086B`は東風、`0082B`はクイタンあり半荘で、`unit_money=20`の通常historyと4人分の通常resultを保存する。
- `0085F`は`unit_money=100`で、通常resultに加えてhigh-class resultを更新し、GP精算額は100単位とする。
- `0075B`はwaremeを有効にし、wareme seatに関係する和了・放銃・被ツモの移動点だけを2倍にする。honba、noten、nagashiは2倍にしない。
- grade channelは東風／半荘のchannel種別に従い、クイタンあり、赤牌2枚、yakitori・wareme・nagashi・chipなしの固定ruleを使う。入場境界はAP-07を正本とする。
- custom normal roomのruleは`RoomOption`から決定し、局数、クイタン、赤牌、yakitori、wareme、chip、ron mode、umaを対局終了まで保持する。

### 10-3. オートマッチング

- `mjkc2e`はauto-matching channelだけが受け付け、enqueue前にGP、beginner、grade、cup条件を検証する。
- queueはchannelごとに分離し、pre-match exclusionに抵触する組み合わせを作らない。
- 予約roomは期待する全員が`mjkc6e`を確認してから開始し、未完了予約はFAILEROOM期限で解消する。
- 自動roomの東風／半荘設定はchannelのgame-type文字から決定する。

## 11. 性能・同時実行原則

- 同じroom engineへのaction適用はroom単位の`SemaphoreSlim`で直列化し、複数接続から同時に状態を変更させない。
- 相互に独立したDB readだけを`Task.WhenAll`で並列化する。同じtransactionや更新順序に依存する処理は並列化しない。
- channel member検索は全接続scanではなくchannel indexを使用する。
- 周期的な複数Redis TTL更新はbatch/pipelineで送信し、room数に比例したnetwork round tripを作らない。
- MySQL game/log DBは別poolを使い、接続上限は想定同時接続・room数とquery fan-outを測定して決める。過去の固定台数試算をcapacity保証として扱わない。
