---
applyTo: "server/**"
description: "Redis キー一覧・TTL・書き込み/無効化タイミング"
---

# AP-13 Redis キャッシュ仕様

## 概要

- Redis 接続は `RedisService` (Singleton) が `StackExchange.Redis` で保持する。
- Redis 未接続時は対応する機能がプロセスメモリまたはMySQLフォールバックで動作する。
- シリアライズは `System.Text.Json` (JSON 形式)。
- キー定数は `MasterCacheService` (static フィールド) と各サービスの `private const` で管理する。
- Redisキーの `{memberNo}` はサーバー内部の永続 `player_account.member_no` を表す。クライアントへその値を公開せず、wire上のプレイヤー識別には `pix` を使う。
- `ChannelServerSettings.ServerUrl` はゲームサーバー負荷・チャンネルリースのサーバー識別子でもあるため、同じ Redis DB を共有する別ゲームサーバープロセスに同じ値を設定してはならない。
- development / alpha / production は異なる Redis 接続先または Redis DB を使用する。同じ DB を共有する場合は全キーが環境間で衝突するため禁止する。

---

## 1. プライマリリーダー選出

| キー | 型 | TTL | 用途 |
|------|----|-----|------|
| `majak4:primary-leader` | STRING | **30 秒** | URL・ホスト・PID・起動nonceを含むプロセス固有IDを保持する |

### 書き込み / 更新タイミング

| タイミング | 処理 | クラス |
|----------|------|--------|
| 起動時 | `SETNX` で取得を試みる | `PrimaryLeaderService.TryAcquireOrRenewAsync()` |
| **8 秒ごと** | 自分の値のままなら `EXPIRE` で TTL 延長 | `ServerStatusBackgroundService` → `PrimaryLeaderService.TryAcquireOrRenewAsync()` |
| グレースフルシャットダウン | 自分が所有していれば Lua で即削除 | `PrimaryLeaderService.Release()` |

---

## 2. マスターデータキャッシュ (`MasterCacheService`)

マスターデータは起動時にプライマリリーダーが一括 Bootstrap する。  
各 `GetXxxAsync()` はキャッシュアサイド (Redis ヒット → ミスなら DB → Redis 書き戻し) で動作する。

### 2-0. マスターデータ参照ポリシー (必須)

- 処理中にマスターデータを参照する場合、対応する `MasterCacheService.GetXxxAsync()` が存在するなら必ずそれを使用する。
- `Command` / `Service` から `PlayerRepository.GetTitleMastAsync()`、`ItemRepository.GetCustomItemMastAsync()`、`GetCustomShopMastAsync()`、`GetCustomSetMastAsync()`、`PlayerRepository.GetWeeklyRewardMastAsync()`、`GetWeeklyRewardMastDirectAsync()`、`ChannelRepository.GetChannelListAsync()`、`GetMaxRoomDirectAsync()` などを直接呼んではならない。
- 例外は `MasterCacheService.BootstrapAsync()`、`MasterCacheService` のキャッシュミス時フォールバック、キャッシュ無効化直後の再ロード処理、またはユーザー別・トランザクション性が必要な更新系処理のみ。
- 新しいマスター性データを追加する場合は、Repository 直呼びを先に増やさず、まず `MasterCacheService` にキー定数・TTL・Bootstrap・`GetXxxAsync()` を追加してから処理側で参照する。
- `*DirectAsync` は Redis を明示的にバイパスする命名である。処理中に使う場合は、レガシー整合性・即時性・トランザクション境界などの理由をコメントで残すこと。

| キー | 型 | TTL | DB テーブル | 用途 |
|------|----|-----|-------------|------|
| `majak4:mast:titles` | STRING (JSON) | **24 時間** | `MJK_TITLEMAST` | 称号 ID → 称号名マップ |
| `majak4:mast:customitems` | STRING (JSON) | **24 時間** | `MJK_CUSTOMITEMMAST` + `MJK_CUSTOMSHOPLIST` | カスタムアイテムマスター (CustomId, Kind, Name, Price) |
| `majak4:mast:customshop` | STRING (JSON) | **5 分** | `MJK_CUSTOMSHOPMAST` | カスタムショップ商品一覧 (販売期間が変わるため短期 TTL) |
| `majak4:mast:customset` | STRING (JSON) | **24 時間** | `MJK_CUSTOMSETMAST` | セット商品 CustomId → 子 CustomId 一覧 |
| `majak4:mast:adminids` | STRING (JSON) | **24 時間** | `MJK_ADMINIDLIST` | 管理者 ID リスト (MemberId, AdminSts) |
| `majak4:mast:dailymission` | STRING (JSON) | **24 時間** | `MJK_DAILYMISSIONMAST` | デイリーミッションマスター (MissionId, ConditionType, ConditionCnt, Point) |
| `majak4:mast:weeklyreward` | STRING (JSON) | **1 時間** | `MJK_WEEKLYREWARDMAST` | 週間報酬マスター (RewardId, RewardType, RewardCnt, MustPoint) |
| `majak4:mast:grademanage` | STRING (JSON) | **1 時間** | `MJK_GRADEMANAGE` | グレードランキング選択可能年月リスト |
| `majak4:mast:cups` | STRING (JSON) | **2 分** | `MAJAKCUPMAST` + `MAJAKCUPCHANELMT` | カップチャンネル設定 (DateFrom, DateTo, IsFestive) |
| `majak4:mast:channels` | STRING (JSON) | **15 分** | `CHANELMAST` + `CHANELWT` | チャンネル一覧 (静的設定 + 現在人数) |

### 書き込み / 無効化タイミング

| キー | 書き込みタイミング | 無効化タイミング |
|------|--------------------|-----------------|
| `majak4:mast:titles` | 起動時 `MasterCacheService.BootstrapAsync()` (プライマリのみ)。キャッシュミス時に各サービスが書き戻す | TTL 切れ (24h) |
| `majak4:mast:customitems` | 同上 | TTL 切れ (24h) |
| `majak4:mast:customshop` | 起動時 Bootstrap + キャッシュミス時 (`MasterCacheService.GetCustomShopMastAsync`) | TTL 切れ (5m) |
| `majak4:mast:customset` | 起動時 Bootstrap + キャッシュミス時 (`MasterCacheService.GetCustomSetMastAsync`) | TTL 切れ (24h) |
| `majak4:mast:adminids` | 同上 | TTL 切れ (24h) |
| `majak4:mast:dailymission` | 起動時 Bootstrap + キャッシュミス時 (`MasterCacheService.GetDailyMissionMastAsync`) | TTL 切れ (24h) |
| `majak4:mast:weeklyreward` | 起動時 Bootstrap + キャッシュミス時 (`PlayerRepository.GetWeeklyRewardMastAsync`) | TTL 切れ (1h) |
| `majak4:mast:grademanage` | 起動時 Bootstrap + キャッシュミス時 (`PlayerRepository.GetGradeManageListAsync`) | TTL 切れ (1h) |
| `majak4:mast:cups` | 起動時 Bootstrap + キャッシュミス時 (`PlayerRepository.GetCupConfigsAsync`) | `CupChannelBackgroundService` が `UpdateCupStatus` 呼び出し後に **即時** `MasterCacheService.InvalidateCupConfigsAsync()` → その後のアクセスで DB 再読み込み + 書き戻し |
| `majak4:mast:channels` | 起動時 Bootstrap + キャッシュミス時 (`ChannelRepository.GetChannelListAsync`) | TTL 切れ (15m)。必要であれば `MasterCacheService.InvalidateChannelsAsync()` で即時無効化可 |

---

## 3. ランキング・スコアキャッシュ (短期 TTL)

| キーパターン | 型 | TTL | DB テーブル | 用途 |
|-------------|----|-----|-------------|------|
| `majak4:ranking:grade:{rankDate}:{rankKind}:{maxCnt}` | STRING (JSON) | **5 分** | `MJK_GRADERAT` | グレードランキングリスト (最大 maxCnt 件) |
| `majak4:ranking:grade:{rankDate}:{rankKind}:{maxCnt}:display-name-v2` | STRING (JSON) | **5 分** | `MJK_GRADERAT` | 表示名を含むグレードランキングリスト。末尾はキャッシュスキーマ版 |
| `majak4:ranking:grade:self:{rankDate}:{memberNo}:{grade}` | STRING (JSON) | **5 分** | `MJK_GRADERAT` | プレイヤー自身のランキング情報 |
| `majak4:graderank:counts:{rankDate}` | STRING (JSON) | **5 分** | `MJK_GRADERANK` | グレード別プレイヤー数 (全サーバー共有) |
| `majak4:mast:proplayers` | STRING (JSON) | **1 時間** | `EVTUSERMAST` (EVTCODE='5333') | プロプレイヤーリスト (全サーバー共有) |
| `majak4:cup:topscore:{channelId}` | STRING (JSON) | **1 分** | `MAJAKCUPRAT` | カップチャンネルの最高スコア |

> `{rankDate}` = `YYYYMM` 形式の整数 (例: `202606`)

### 書き込み / 無効化タイミング

| キー | 書き込みタイミング | 無効化タイミング |
|------|--------------------|-----------------|
| `majak4:ranking:grade:…` | `RatingRankInfoCommand` がランキング表示リクエスト時、DB 照会結果を `PlayerRepository.GetGradeRankListAsync()` / `GetGradeRankSelfAsync()` で書き込む | TTL 切れ (5m) のみ。強制無効化なし |
| `majak4:graderank:counts:{rankDate}` | `GradeRankBackgroundService` が 5 分ごとに `PlayerRepository.GetGradeRankCountsAsync()` で書き込む。キャッシュミス時も書き戻す | TTL 切れ (5m) |
| `majak4:mast:proplayers` | 起動時 Bootstrap + `GradeRankBackgroundService` が 1 時間ごとに `PlayerRepository.GetProPlayerListAsync()` で書き込む | TTL 切れ (1h) |
| `majak4:cup:topscore:{channelId}` | `CupChannelBackgroundService` がフェスティブカップの通知タイミング (11:00 / 23:00) に `PlayerRepository.GetCupTopScoreAsync()` で書き込む | TTL 切れ (1m) |

---

## 4. ルーム管理

| キーパターン | 型 | TTL | 用途 |
|-------------|----|-----|------|
| `room:{chanelId}:{roomId}` | STRING (JSON) | **30 秒** | チャンネルごとのルーム情報 (RoomId, ChanelId, Title, IsPrivate, MemberCnt, MemberMax, ServerUrl, RoomOption)。roomIdはチャンネル間で重複するため単独キーにしない |
| `channel:{chanelId}:rooms` | SET | **90 秒** | チャンネル内のルーム ID セット |
| `continue:{memberNo}:room` | STRING (JSON) | **30 秒** | 対局中切断プレイヤーの続行先ルーム。キーと内部値の会員IDは `member_no` |

`room:{chanelId}:{roomId}` と `channel:{chanelId}:rooms` の `{chanelId}` は Redis Cluster の hash tag でもある。同じチャンネルのルーム本体とindexを同一slotへ配置し、Luaによる原子的なルーム予約を可能にする。

### 書き込み / 更新 / 削除タイミング

| タイミング | 処理 | クラス |
|----------|------|--------|
| ルーム作成時 | `room:{chanelId}:{roomId}` SET + `channel:{chanelId}:rooms` SADD | `RoomRegistryService.RegisterRoomAsync()` |
| 新規ルーム予約時 | 期限切れ roomId の掃除、`SCARD` 相当の有効数確認、`max_room` 判定、ルーム SET + index SADD を Lua で原子的に実行 | `RoomRegistryService.TryRegisterRoomAsync()` |
| プレイヤー入退室時 | `room:{chanelId}:{roomId}` の MemberCnt を更新 (TTL リセット) | `RoomRegistryService.UpdateMemberCountAsync()` |
| 対局中プレイヤーのネットワーク切断時 | `continue:{memberNo}:room` SET。値は `room:{chanelId}:{roomId}` の ServerUrl / RoomOption を参照する。明示退室では作成しない | `MajakGameHub.HandleRoomDisconnectAsync()` |
| 続行プレイヤー復帰時 | `continue:{memberNo}:room` DEL | `AutoEnterRoomCommand` / `RoomEnterRoomCommand` |
| ゲーム終了・無人対局ルーム即時削除 | 対象席の `continue:{memberNo}:room` DEL | `GameLogicService` / `RoomRegistryService` / `MajakGameHub` / `ServerStatusBackgroundService` |
| **8 秒ごと** | 全アクティブルームを現在の MemberCnt / State / RoomPlaying で再登録して TTL を 30 秒にリセットし、アクティブチャンネルの room-index SET TTL を 90 秒にリセット (ハートビート) | `ServerStatusBackgroundService` → `RoomRegistryService.RegisterRoomAsync()` / `RefreshChannelSetTtlBatchAsync()` |
| **8 秒ごと** | 対局中 `IsOutPlayer=true` の座席について、ゲーム終了まで `continue:{memberNo}:room` の TTL を 30 秒へ更新する | `ServerStatusBackgroundService` → `RoomRegistryService.RefreshContinueRoomsAsync()` |
| ルーム解散時 | `room:{chanelId}:{roomId}` DEL + `channel:{chanelId}:rooms` SREM | `RoomRegistryService.RemoveRoomAsync()` |
| グレースフルシャットダウン | 担当全ルームを即削除 | `ServerStatusBackgroundService` → `RoomRegistryService.RemoveAllRoomsAsync()` |

> **ゴーストルーム防止**: サーバーがクラッシュすると TTL 更新が止まり、最大 30 秒後に `room:{chanelId}:{roomId}` が自動消滅する。
> **続行先の整合性**: `/api/player/continue-room` はJWTの `member_no` を正本に検索する。対応する `room:{chanelId}:{roomId}` が存在しない場合は `continue:{memberNo}:room` を削除して未検出として返す。
> **ルーム上限**: 通常作成、オートマッチング、トーナメントの新規ルームはすべて `TryRegisterRoomAsync()` を通す。状態更新とTTL更新だけを行う既存ルームには `RegisterRoomAsync()` を使う。

---

## 5. チャンネルメンバー管理

| キーパターン | 型 | TTL | 用途 |
|-------------|----|-----|------|
| `channel:{chanelId}:members` | HASH | **90 秒** | field=`member_no` → JSON `{memberNo, pix, nickname, rating, sex, avatarId}`。GET応答は内部field / memberNoを公開しない |

### 書き込み / 削除タイミング

| タイミング | 処理 | クラス |
|----------|------|--------|
| チャンネル入室時 | `HSET` + `EXPIRE 90s` | `ChannelMemberService.EnterAsync()` |
| チャンネル退室時 | `HDEL`。空 HASH なら `DEL`、残メンバーありなら `EXPIRE 90s` 更新 | `ChannelMemberService.LeaveAsync()` |
| **8 秒ごと** | 自サーバーがリースを所有するチャンネルだけ、現在のローカルメンバーで HASH を同期して TTL を 90 秒にリセット | `ServerStatusBackgroundService` → `ChannelMemberService.SyncChannelAsync()` |

> **ゴーストメンバー防止**: 正常退室/切断時は `HDEL` する。サーバーがクラッシュして退室処理が走らない場合でも、TTL 更新が止まり最大 90 秒後にチャンネルメンバー HASH が自動消滅する。

---

## 6. サーバー負荷管理

| キー | 型 | TTL | 用途 |
|------|----|-----|------|
| `game:servers` | ZSET | なし (スコアで生存判定) | serverUrl → 最終報告 UnixTime。スコアが `now - 30秒` より古いサーバーは死亡とみなす |
| `game:server:roomcounts` | HASH | なし | serverUrl → 現在のルーム数 |
| `game:server:channelcounts` | HASH | なし | serverUrl → 担当チャンネル数 |
| `channel:{chanelId}:server` | STRING | **60 秒** | DBで割り当てられたサーバーの実行時所有リース。ルーティングの正本は `channel_master.server_url` |

### 書き込み / 更新 / 削除タイミング

| キー | タイミング | 処理 | クラス |
|------|----------|------|--------|
| `game:servers` | **8 秒ごと** | `ZADD` でスコア(最終報告時刻)を更新 | `ServerStatusBackgroundService` → `ServerLoadService.RegisterSelfAsync()` |
| `game:server:roomcounts` | **8 秒ごと** | `HSET` でルーム数を更新 | 同上 |
| `game:server:roomcounts` / `game:server:channelcounts` | **8 秒ごと** | `game:servers` から生存期限切れサーバーを削除する際、同じ serverUrl フィールドを `HDEL` | `ServerLoadService.RegisterSelfAsync()` |
| `game:servers` / `roomcounts` | グレースフルシャットダウン | `ZREM` / `HDEL` で即削除 | `ServerLoadService.UnregisterSelfAsync()` |
| `channel:{chanelId}:server` | チャンネル入室時 | DB割り当て先と自サーバーURLの一致を先に検証し、単一キーLuaで未登録時の取得を原子的に実行。別サーバー所有中なら入室を拒否 | `ServerLoadService.ClaimChannelAsync()` |
| `channel:{chanelId}:server` | **8 秒ごと** | Luaで現在値が自サーバーの場合だけ TTL を更新し、期限切れなら原子的に再取得。実所有数で channelcounts を再計算 | `ServerStatusBackgroundService` → `ServerLoadService.RefreshChannelLeasesBatchAsync()` |
| `channel:{chanelId}:server` / `channelcounts` | グレースフルシャットダウン | 単一キーLuaで自サーバー所有リースだけを削除し、全解放後に自サーバーのchannelcountsを0へ更新 | `ServerLoadService.ReleaseChannelsAsync()` |
| `channel:{chanelId}:server` | 管理画面でチャンネル設定更新 | 単一キーLuaで旧所有リースを削除し、次回入場時に新しいDB割り当てを反映。channelcountsは次回heartbeatで実所有数へ収束 | `ServerLoadService.ResetChannelLeaseAsync()` |

`GET /api/channel/{chanelId}/server` は `channel_master.server_url` を取得した後、`game:servers` の最終報告が30秒以内か確認する。Redis未接続、heartbeatなし、またはチャンネル非公開の場合は接続先を返さず503とする。

### 6-1. グローバルロビー接続リース

| キーパターン | 型 | TTL | 用途 |
|-------------|----|-----|------|
| `player:lobby-session:{memberNo}` | STRING (JSON) | **90 秒** | 全ゲームサーバー共通で同一アカウントのロビー接続を 1 件に制限する |

値は `{ ServerId, ConnectionId, TabId, LeaseToken }`。入場時は Lua で原子的に取得する。キーがない場合は新規取得し、既存値の `TabId` が同じ場合だけ接続 ID とトークンを置き換えて TTL を更新する。異なる `TabId` は新しい接続を拒否する。

| タイミング | 処理 | クラス |
|----------|------|--------|
| チャンネル入室開始時 | Lua で新規取得、または同一ブラウザタブの再接続として原子的に置換。DB 読み込みや入場処理が失敗した場合は条件付きで即削除 | `EnterChannelCommand` → `LobbySessionLeaseService` |
| **8 秒ごと** | Redis の値が自分の JSON 値と完全一致する場合だけ TTL を 90 秒へ更新 | `ServerStatusBackgroundService` → `LobbySessionLeaseService.RefreshAllAsync()` |
| チャンネル退室・SignalR 切断時 | `LeaseToken` を含む JSON 値が完全一致する場合だけ Lua で削除 | `ExitChannelCommand` / `MajakGameHub.OnDisconnectedAsync()` |
| サーバークラッシュ時 | ハートビート停止後、最大 90 秒で自動削除 | Redis TTL |

> **タブ再接続と古い切断イベント対策**: `TabId` はブラウザの `sessionStorage` に保持する。したがって同じタブの更新・履歴復帰は許可され、別タブ・別ブラウザは拒否される。ConnectionId だけで更新・削除してはならない。JSON 値全体を比較することで、遅れて到着した旧接続の終了処理が新しい接続のリースを削除できないようにする。

---

## 7. JSON シリアライズ形式

すべてのキャッシュ値は `System.Text.Json` でシリアライズする。  
`RedisService.GetJsonAsync<T>()` / `SetJsonAsync<T>()` を必ず使用すること。

### `majak4:mast:titles` の値 (例)
```json
{ "mjks001": "鳳凰", "mjks002": "天才", ... }
```

### `majak4:mast:cups` の値 (例)
```json
[
  { "ChannelId": "MAJAK2CUP001", "ChannelName": "春カップ",
    "DateFrom": "2026-04-01T00:00:00", "DateTo": "2026-04-30T23:59:59",
    "IsFestive": true }
]
```

### `room:{chanelId}:{roomId}` の値 (例)
```json
{ "roomId": 101, "chanelId": "MAJAK20090A001", "title": "東風戦",
  "isPrivate": false, "memberCnt": 2, "memberMax": 4,
  "serverUrl": "https://sv1.example.jp", "roomOption": "..." }
```

---

## 8. Google モバイル認証コード

> 現在のサーバーコードには本キーの発行・消費実装がない。実装追加時にのみ下記規約を有効化する。

| キーパターン | 型 | TTL | 用途 |
|-------------|----|-----|------|
| `auth:google-mobile:{sha256(code)}` | STRING (JSON) | **2 分** | 外部ブラウザで検証済みの Google ID token を Android アプリへ安全に引き渡す一回限りコード |

- URL / deep link には ID token を含めず、暗号学的乱数から作ったコードのみを含める。
- Redis キーにはコード原文ではなく SHA-256 hash を使う。
- アプリからの交換時は `GETDEL` で原子的に取得・削除し、同じコードの再利用を拒否する。
- Redis 未接続の開発環境ではプロセス内 `ConcurrentDictionary` へフォールバックする。
- 発行: `GoogleMobileAuthCodeService.IssueAsync()`、消費: `ConsumeAsync()`。

---

## 9. プレイヤー別ミッション・報酬キャッシュ

| キーパターン | 型 | TTL | DBテーブル | 用途 |
|---------|------|-----|-----------|------|
| `majak4:player:{memberNo}:daily:{yyyyMMdd}` | STRING (JSON) | **当日終了まで** | `MJK_DAILYMISSIONLIST` | 当日のデイリーミッション達成状態 (missionId → state) |
| `majak4:player:{memberNo}:weekly:{monDate}` | STRING (JSON) | **次の月曜日まで** | `MJK_WEEKLYREWARDLIST` | 今週の週間報酬受取状態 (rewardId → status) |
| `majak4:player:{memberNo}:weeklypoint:{monDate}` | STRING (JSON) | **次の月曜日まで** | `MJK_DAILYMISSIONLIST` + `MJK_DAILYMISSIONMAST` | 今週の累積ポイント (int) |
| `majak4:player:{memberNo}:dailypoint:{yyyyMMdd}` | STRING (JSON) | **当日終了まで** | `MJK_DAILYMISSIONLIST` + `MJK_DAILYMISSIONMAST` | 当日ポイントと上限 (`[own, max]`) |

---

## 10. Refresh Cookie セッション

| キーパターン | 型 | TTL | 用途 |
|-------------|----|-----|------|
| `auth:refresh:{sha256(token)}` | STRING (JSON) | **1～365日 (設定値)** | Refresh Cookie のサーバー側セッション。ランダムtokenのSHA-256をキーにするため会員間で共有しない |

- 発行は `SET NX` を使用し、暗号学的乱数tokenの衝突時は上書きしない。
- ログアウト時は同じtoken hashのキーだけを削除する。

> `{monDate}` は対象週の月曜日を `yyyyMMdd` で表した値 (例: `20260622`)。

### 書き込み / 無効化タイミング

| キー | 書き込み | 無効化 |
|----|------|--------|
| `…:daily:{today}` | `GetDailyMissionListAsync` のキャッシュミス時にDB照会後記録 | `SetDailyMissionAsync` の更新完了直後に削除 |
| `…:weekly:{monDate}` | `GetWeeklyRewardListAsync` のキャッシュミス時にDB照会後記録 | `TryReceiveWeeklyRewardAsync` の成功直後に削除 |
| `…:weeklypoint:{monDate}` | `GetWeeklyPointAsync` のキャッシュミス時にDB照会後記録 | `SetDailyMissionAsync` の状態変更直後に削除 |

- `MasterCacheService` のキー定数 (`KeyTitles` 等) を文字列でハードコードしてはならない。必ずクラスの `const` / `static string` を参照すること。
- Redis 書き込みエラーは `catch { }` で握りつぶして正常系を継続する (可用性優先)。
- `majak4:mast:cups` は `CupChannelBackgroundService.UpdateCupStatusAsync()` 後に **必ず** `MasterCacheService.InvalidateCupConfigsAsync()` を呼ぶ。呼び忘れると古い状態が最大 2 分間キャッシュされる。
- `channel:{chanelId}:members` は TTL 付き HASH。アクティブ中は 8 秒ごとに延命し、異常終了時は最大 90 秒で消える。
- 開発環境 (Redis 未起動) では全キーがフォールバック動作し、テストに影響しない。
