# サーバー負荷分析 — MajakServer (HgMajak2)

> 前提: 日次ユーザー 2万人 / ピーク同時接続 2,000〜4,000 人

---

## 1. 負荷要因一覧

### 🔴 危険度: 高

#### 1-1. `EnterChannel` — Oracle クエリ逐次実行 (最大 8 本)

```
ExistsCommonRatAsync    SELECT  (新規チェック)
CreateCommonRatAsync    INSERT  (新規の場合のみ)
LoadCommonRatAsync      SELECT  ┐
LoadHangeRatAsync       SELECT  │ Task.WhenAll で並列化可能
LoadSkinListAsync       SELECT  │
EnsureDefaultItemsAsync SELECT+INSERT
GetTitleListAsync       SELECT  ┘
GetUserPresentAsync     SELECT
```

- ピーク時 100 同時接続 → **700 Oracle クエリ直列処理**
- 改善: `LoadCommonRatAsync` / `LoadHangeRatAsync` / `LoadSkinListAsync` / `GetTitleListAsync` を `Task.WhenAll` で並列化 → 応答時間 **約 3 倍高速化**
- **対応済み**: [MajakGameHub.cs] EnterChannel 内の独立クエリを並列化

#### 1-2. `GameRoom.Engine` — 並行アクセス保護なし

```csharp
// GameLogicService.GamePlayProcessAsync
room.Engine.ProcessAction(order, action, bipaiIndex, bipaiCount);  // lock なし
```

- 4 人が同時にパケットを送信すると同一 `MajakGameLogic` インスタンスに並行アクセス
- ターン制のため実際の衝突頻度は低いが、マルチスレッド起因のクラッシュリスクが残存
- **対応済み**: `GameRoom` に `SemaphoreSlim(1,1)` を追加し `GamePlayProcessAsync` 内でロック取得

#### 1-3. `GameReportProcessAsync` — 局終了時 Oracle 連打 (最大 15 本)

```
InsertGameHistAsync     INSERT (Oracle)
InsertGameHistAsync     INSERT (MySQL)
UpdateCommonRatAsync    UPDATE × 4人 (Oracle)
SetDailyMissionAsync    UPDATE × 4人 (Oracle)
CheckTitleClearAsync    SELECT+INSERT × 4人 (Oracle)
UpdateGemCountAsync     UPDATE (Oracle)
```

- 局終了が同時発生するピーク時に Oracle コネクション圧迫
- 改善: プレイヤー別処理 (4人分) を `Task.WhenAll` で並列化 → クエリ数は変わらないが待機時間を圧縮

---

### 🟡 危険度: 中

#### 2-1. `PlayerSessionService.GetChannelMembers` — O(N) 全体スキャン

```csharp
_byConnId.Values.Where(p => p.ChannelId == channelId && p.RoomId == null)
```

- `GetRoomList` / `GetMemberList` コマンド実行のたびに全接続者辞書をフルスキャン
- 1,000 人: 問題なし。5,000 人超: 処理時間が顕在化
- **対応済み**: `_byChannel` (チャンネル別 `ConcurrentDictionary<string, ConcurrentDictionary<string,MajakPlayer>>`) を追加してインデックス化

#### 2-2. `ServerStatusBackgroundService` — Redis EXPIRE を N 回個別発行

```csharp
foreach (var room in _session.GetAllRooms())
    await _roomRegistry.RefreshTtlAsync(room.RoomId);  // 1ルーム = 1 Redis 往復
```

- 100 ルーム × 8 秒ごと = **秒間 12.5 Redis コマンド**
- 改善: `IBatch` / `ITransaction` でパイプライン送信 → Redis 往復を 1 回に削減
- **対応済み**: `RoomRegistryService.RefreshTtlBatchAsync` を追加しバッチ処理へ変更

#### 2-3. Oracle Connection Pool — 明示的サイズ設定なし

- `OracleDbContext` は Singleton だが毎クエリで `CreateConnectionAsync()` を呼ぶ
- デフォルト Pool Size = 100。ピーク時に `EnterChannel` 並列処理が重なると Pool 枯渇
- **対応済み**: `appsettings.json` の ConnectionStrings に `Pooling=true;Min Pool Size=5;Max Pool Size=200;` を追加

---

### 🟢 危険度: 低

| 項目 | 理由 |
|------|------|
| `AutoMatchingBackgroundService` | インメモリのみ、3 秒間隔 |
| SignalR ゲームブロードキャスト | ルーム 4 人 / チャンネル 100 人以下 |
| `MajakGameLogic` CPU 負荷 | 1 アクションあたり O(1)、軽量 |
| Redis チャンネルメンバー管理 | HSET 単一キー、低コスト |
| Cup / Tournament タイマー | Primary サーバーのみ、低頻度 |

---

## 2. サーバー台数試算

| 指標 | 値 |
|------|-----|
| 日次ユーザー | 20,000 人 |
| ピーク同時接続 | 2,000〜4,000 人 (10〜20%) |
| 対局 1 ゲーム平均時間 | 35 分 (東風 25 分 / 半荘 50 分) |
| ピーク活性ルーム数 | 3,000 人 / 4 人 × 60% = **450 ルーム** |
| サーバー 1 台の適正ルーム数 | 150〜200 ルーム (Oracle コネクション・メモリ基準) |
| サーバー 1 台のメモリ使用量目安 | ベース 300MB + 接続者 × 200KB ≈ 1,500 人で 600MB |

```
必要台数 = 450 ルーム ÷ 175 ルーム/台 ≈ 2.6 台
```

**推奨構成: 3 台** (1 Primary + 2 Secondary)
- 1 台障害時も 2 台で継続運用可能
- ピーク時 25% 余裕

---

## 3. 改善項目と優先度

| 優先 | 項目 | 期待効果 |
|:---:|------|---------|
| 1 | `EnterChannel` Oracle 並列化 | 接続応答 3× 高速化 |
| 2 | Oracle Connection Pool サイズ明示 | ピーク時 Pool 枯渇防止 |
| 3 | `GameRoom.Engine` SemaphoreSlim ロック | マルチスレッド安定性 |
| 4 | `RefreshTtlAsync` Redis パイプライン化 | Redis 負荷 1/N |
| 5 | `GetChannelMembers` チャンネル別インデックス | 5,000 人超スケール対応 |
