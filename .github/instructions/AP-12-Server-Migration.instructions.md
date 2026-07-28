---
applyTo: "server/**,server.tests/**"
description: "C++ レガシーサーバーを .NET Core へ移植する際の重要ポイント (試行錯誤から得た知見)"
---

# AP-12 サーバー移植 効率化ガイド

レガシー C++ サーバー (`server/legacy/server/`) を .NET Core C# サーバー (`server/`) へ移植する際の
**全体戦略と実装手順**。試行錯誤を通じて得た実践的な知見をまとめる。

---

## 1-1. 推測実装の禁止 (最重要)

サーバー移植では、レガシー C++ サーバー (`server/legacy/server/`) を唯一の仕様根拠とする。
**推測・既存 C# 実装の都合・一般的な Web/API 設計・見た目の挙動からロジックを作ってはならない**。

- 変更前に、必ず対応するレガシーのコマンド、関数、プロトコルキー、DB アクセス、定数、分岐条件、送信タイミングを特定する。
- 既存 C# 実装にあるロジックでも、レガシー根拠が確認できなければ正しいものとして扱わない。
- 不具合修正では、まずレガシーソースを調査し、レガシーとの差分を特定してから修正する。C# 側だけを見て原因を推測してはならない。
- プロトコルのフィールド名、値、成功/失敗判定、failcode、Push イベント名、送信先、送信順序はレガシーを基準に合わせる。
- DB の SELECT / INSERT / UPDATE、ストアドプロシージャ呼び出し、パラメータ、文字コード、NULL/空文字の扱いはレガシー `HMajDBObject.cpp` などを確認して合わせる。
- レーティング、段位、コイン、マッチング、入室条件、ルーム状態、ミッション、トーナメントなどの計算・判定は、レガシーの定数と式を確認してから実装する。
- レガシー根拠が見つからないロジックは、作成・変更・削除してはならない。必要なら実装を止め、根拠不足としてユーザーに確認する。
- 現在のブラウザ表示、React 実装、SignalR の都合、REST API として自然な形、C# の一般的な設計を仕様根拠にしてはならない。これらは移植先の表現であり、正解ではない。

### 1-2. 修正時のレガシー根拠報告 (必須)

サーバー処理を修正する場合は、作業中および最終報告で、必ずレガシー根拠を明示する。

- 修正前に、どのレガシーファイル・クラス・関数・コマンド・プロトコルキー・DB アクセス・定数を見て判断したかを確認する。
- 修正内容の説明では、主要な変更ごとに「参照したレガシー箇所」と「C# 側で対応した箇所」をセットで示す。
- プロトコル、DB、分岐条件、計算式、送信順序、failcode、Push イベントを変更した場合は、対応するレガシー値や関数名を報告に含める。
- レガシー根拠が未確認のまま修正した場合は作業完了とみなしてはならない。根拠不足としてユーザーに報告し、確認を求める。
- 最終報告では、少なくとも「レガシーで確認した場所」「新規で変更した場所」「ビルド/診断/テストを実行したか」を明記する。

問題があるすべてのサーバーロジックは、レガシーソースを調査して、レガシー基準で修正する。

---

## 1-3. ゲーム進行で特に混同しやすいレガシー契約

- `HMajPlayer::m_nPlayerPos` と `HMajPlayer::m_nSeatPos` は別物である。
  - `m_nPlayerPos`: room/player position。入室・退室・ルーム表示・復帰座席で使う。
  - `m_nSeatPos`: engine order。`MJPID_ACTION.nSeatOrder` 検証、`SendPaiInfo` の open mask、`ProxyPlay(nOrder)` で使う。
  - C# では `SeatPos` を room/player position として保持し、`EngineOrder` など別フィールドで `m_nSeatPos` 相当を保持する。ゲーム開始時に `SeatPos` を engine order で上書きしてはならない。
- `DATAFORMAT_HANCHANINFO.nPlayer[4]` は `engine order -> room/player position` の対応表である。逆引きが必要な場合は `SeatToEngineOrder[playerPos] = engineOrder` のように明示的に作る。
- `DATAFORMAT_ACTIONINFO_RES` は `nSeatOrder`, `eAction`, `nBipaiIndex[]` だけを持つ。打牌 tile code を `MJPID_ACTION` payload に追加してはならない。action 前に送った `smmc4e` / `BIPAIINFO` と `nBipaiIndex` からクライアントが解決する。
- `SendPaiInfoToAll()` は各 player の engine order を `nOpenPos` として `SendPaiInfo()` する。room/player position を open mask に使ってはならない。
- 接続中ユーザーの `MODE_FURO` / `MODE_CHAN` をサーバーが pass-only という理由で送信直後に自動 `PAS` にしてはならない。レガシーではクライアントが期限到達または自動パス設定で `MJPID_ACTION(PAS)` を送る。Web 版では AP-14 の server timeout fallback により、deadline 到達時だけ `PAS` 相当を engine に適用する。
- `ProxyPlay(nOrder)` の `nOrder` は engine order である。disconnect / exit / continue player 処理で room seat から呼ぶ場合は、必ず `nPlayer` / `SeatToEngineOrder` で engine order に変換してから呼ぶ。
- 対局中切断は完全退室ではない。C# では `IsOutPlayer=true` として元座席/`RoomId` を保持し、古い SignalR connection mapping だけを外す。`Remove(connectionId)` は古い切断イベントが新しい同一 memberId の connection mapping を消さないよう、現在 mapping が同じ connectionId の場合だけ削除する。
- 接続断後にユーザーが明示退室し、`c9e` を送れなかった場合、次の `c1e` は `abandonPreviousRoom=true` / `abandonRoomId` を含める。`EnterChannelCommand` は同一 channel・同一 room・同一 memberId の Playing 座席だけを abandon 対象にし、ロビー入場を `USER_MULTI_LOGIN` で拒否してはならない。
- サーバーメモリ内のゴーストルーム判定は Redis TTL 判定と混同してはならない。
  - `GameRoom.IsEmpty == true` は削除可能な空ルームである。
  - `room.State != Playing && room.HasNoActiveMembers == true` は対局外の無人ルームとして削除/非表示対象である。
  - `room.State == Playing && room.HasNoActiveMembers == true` はゴーストではなく続行猶予ルームである。`NoActiveMembersSince` を記録し、`ChannelServerSettings.ContinueRoomGraceSeconds` 経過後に `ServerStatusBackgroundService` が session / Redis room / continue key をまとめて削除する。
  - `room.State == Playing` で一部 seat だけ `IsOutPlayer=true` の場合は正常進行中ルームであり、座席を削除してはならない。切断者は continue player として保持する。
- Redis の `room:{roomId}` TTL は「担当ゲームサーバーが生存しているか」の判定であり、サーバーメモリ内の Playing 続行猶予を短絡して削除する根拠ではない。
- `continue:{memberId}:room` が生存しているユーザーは、必ずその room へ復帰させる。`CreateRoomCommand`、`RoomEnterRoomCommand`、`AutoEnterRoomCommand` は別 roomId の作成・入室・観戦を拒否し、続行対象 roomId への復帰だけを許可する。

---

## 1. 移植の全体フロー — 4 フェーズで進める

```
フェーズ1: 読解     レガシーソースを正確に理解する
フェーズ2: 分類     関数を種別に分けて移植先を決める
フェーズ3: 実装     定数→モデル→ロジック→コマンドの順で積み上げる
フェーズ4: 検証     テストを書いてビルドを壊さず確認する
```

フェーズを飛ばして実装すると、後から定数の誤り・クラス構造の不整合・
テスト破損が重なって手戻りが大きくなる。

---

## 2. フェーズ1: レガシーソースの読解順序

移植対象コマンドが決まったら、以下の順で参照ファイルを特定する。

```
1. HMajProtocol.h      コマンドコード定義 (commandXxx の番号と文字列)
2. HMajChnlServer.cpp  チャンネルコマンドのエントリポイント (ProcessCommand_Xxx)
   HMajRoomServer.cpp  ルームコマンドのエントリポイント
3. HMajDBObject.cpp    DB アクセス関数 (SQL・SP 呼び出しの具体的な引数)
4. HMajDef.h           ゲームロジック定数 (DEALER_BASE, K factor 等)
   HMajCommon.h        ハードコードテーブル (s_stEnterGradeModeCond[] 等)
   MajakDef.h          ACT enum, 点数定数
5. HMajRatingCommon.cpp レーティング計算式
```

> ⚠️ 定数を **推測で実装してはならない**。ゲームバランスやレーティングに直接影響する。
> 必ずヘッダファイルの定義値を確認してから C# コードに書く。

---

## 3. フェーズ2: 関数の5分類と移植先

レガシー C++ 関数は必ず以下のいずれかに分類し、移植先を確定してから実装する。

| 分類 | C++ の特徴 | C# の移植先 |
|------|-----------|------------|
| **① コマンドハンドラ** | `ProcessCommand_Xxx()` — クライアントからの要求を受ける | `Commands/Channel/` または `Commands/Room/` の `IGameCommand.ExecuteAsync()` |
| **② Push 送信** | `DoSendMember()` / `DoSendRoom()` / `DoSendAll()` — サーバー発信 | コマンドハンドラ or BackgroundService 内の `_hub.Clients.X.SendAsync()` |
| **③ バックグラウンド処理** | `TimerProc()` / 定期ループ | `Infrastructure/` の `BackgroundService` 派生クラスの `TickAsync()` |
| **④ ユーティリティ/計算** | `CalcRating_*()` / `CheckEnter*()` — 純粋計算 | `Services/RatingService.cs` 等のサービスクラス |
| **⑤ DB アクセス** | `GetMemberInfo()` / `UpdateResult_*()` | `Repositories/` の Repository クラス |

- **② Push 送信は独立したクラスにしない**。① や ③ の中で呼ぶだけ。
- クライアント側受信ハンドラが未実装でもサーバー側 Push は実装完了させる
  → `// TODO: client handler` コメントを残す。

---

## 4. フェーズ3: 実装の積み上げ順序

1 コマンドを移植するとき、以下の順で実装する。**後工程に依存するものを先に作る**。

```
Step 1. 定数 / Enum
        → サービスクラスやモデルの定数フィールドに追加
        → 例: GameRoom に IsGradeChannel プロパティ、RatingService に DEALER_BASE 定数

Step 2. モデル変更
        → 必要なプロパティを GameRoom / MajakPlayer に追加
        → 例: GameRoom.Viewers リスト追加

Step 3. サービスロジック (④)
        → RatingService / GameLogicService など純粋計算メソッドを実装

Step 4. DB アクセス (⑤)
        → Repository に SELECT / UPDATE メソッドを追加
        → SQL・パラメータはレガシーの HMajDBObject.cpp から転記

Step 5. コマンドハンドラ (①)
        → IGameCommand 実装クラスを作成
        → コンストラクタ依存を DI で受け取る

Step 6. Push 送信 (②)
        → ハンドラ内で _hub.Clients.X.SendAsync() を呼ぶ

Step 7. バックグラウンド処理 (③)
        → 最後。コマンド群がすべて動いてから組み込む
```

---

## 5. コマンドの優先順位 — ゲーム進行軸で決める

移植するコマンドの順序は「ゲームが実際に遊べるようになるまでの最短経路」で決める。

```
必須 (対局が成立しない)
  mjkc2e  AutoMatching       オートマッチング要求
  mjkc3e  CancelAutoMatching キャンセル
  mjkc4e  AutoStart          対局開始合意
  mjkc5e  AutoExitRoom       退室
  mjkc6e  AutoEnterRoom      入室 (観戦含む)
  smmc1e  SendOkButton       ゲーム開始 OK
  mjkroom RoomState          ルーム状態通知

優先 (ゲーム後に必要)
  mjkc1e  GetDetailRec       詳細戦績
  mjkc25e RatingRankInfo     レーティングランキング
  mjkc17e MoneyReplenishment コイン補充
  mjkc18e ApplyEarnedMoney   獲得コイン適用

後回し可 (オプション機能)
  mjkc32e〜mjkc34e  ミッション / 週間報酬
  mjkc35e〜mjkc42e  カスタムショップ
  mjkc26e〜mjkc30e  トーナメント
```

---

## 6. フェーズ4: テスト戦略 — 壊さずに進む

### 6-1. テストは「コマンド単位」で追加する

1 コマンドを実装したら、その場でテストを追加する。
後でまとめて書くと、バグの原因が追えなくなる。

```
テストファイル対応表:
  Protocol_Channel1Tests.cs  mjkc2e〜mjkc6e (AutoMatching 系)
  Protocol_Channel2Tests.cs  mjkc1e, mjkc17e, mjkc18e
  Protocol_Channel3Tests.cs  mjkc19e〜mjkc25e
  GameLogicHelperTests.cs    CalcRating 等の純粋計算
  ServiceTests.cs            BackgroundService の TickAsync
```

### 6-2. コンストラクタを変更したら 3 箇所確認する

コンストラクタに新しい依存を追加した場合、**必ず** 以下 3 箇所を検索して修正する:

| 場所 | 壊れ方 | 修正例 |
|------|-------|-------|
| テストクラスの `new XxxCommand(...)` | CS7036 (コンパイルエラー) | 引数を追加 |
| ヘルパーメソッドの `new XxxBackgroundService(...)` | CS7036 | `IOptions<T>` を `Options.Create(new T())` で追加 |
| リフレクション `GetMethod(...).Invoke(svc, args[])` | `TargetParameterCountException` (実行時エラー、ビルドは通る) | `new object[]` の要素数を合わせる |

### 6-3. ビルドゲート — 毎実装後に必ず実行

```powershell
dotnet test server.tests/ 2>&1 | Select-Object -Last 5
```

失敗したら **他の実装に進まない**。そのコミット内で修正してから次へ。

---

## 7. クラス対応マッピング (C++ → C#)

| レガシー C++ クラス / ファイル | C# 移植先 |
|-------------------------------|-----------|
| `CHMajChnlServer` / `ProcessCommand_Xxx()` | `Commands/Channel/*.cs` の `IGameCommand` |
| `CHMajRoomServer` / ルーム処理 | `Commands/Room/*.cs` の `IGameCommand` |
| `HMajDBObject` / SQL 直接実行 | `Repositories/MySQL/PlayerRepository.cs` 等 |
| `HMajLogDBObject` / ログ DB | `Repositories/MySQL/LogRepository.cs` |
| `CHMajChnlInfo` / チャンネル状態 | `Models/Game/GameRoom.cs` + `PlayerSessionService.cs` |
| タイマースレッド / `TimerProc` | `Infrastructure/AutoMatchingBackgroundService.cs` 等 |
| `DoSendMember` / `DoSendRoom` | `_hub.Clients.X.SendAsync(Cmd.Xxx, packet)` |

---

## 8. チャンネル種別の判定 — subId の文字位置

```csharp
// player.ChannelId から subId を取り出す (ChannelId = "XXXXXX" + subId の構造)
string subId = player.ChannelId.Length >= 11
    ? player.ChannelId.Substring(6, 5)
    : "";

bool isGradeChannel    = subId.Length >= 3 && subId[2] == 'G';
bool isTrainingChannel = subId.Length >= 3 && subId[2] == 'T';
bool isCupChannel      = subId.Length >= 3 && subId[2] == 'C';

// 段位チャンネル内の卓種別 (HMajCommon.h の s_stEnterGradeModeCond[])
char chanelType = subId.Length >= 5 ? subId[4] : '\0';
// 'A'=通常卓, 'B'=段位卓, 'C'=高段位卓, 'D'=十段位卓
```

---

## 9. DB にない情報はハードコードで管理する

以下はレガシー側もコードにハードコードされており、**DB テーブルとして存在しない**。
C# 側でも同様にサービスクラスの定数・switch 文で管理する。

| 情報 | レガシー定義元 | C# 管理場所 |
|------|-------------|-------------|
| 段位戦進入条件 (コイン下限・段位範囲) | `HMajCommon.h` の `s_stEnterGradeModeCond[]` | `RatingService.CheckEnterGradeMode()` |
| レーティング K 係数 (K=20) / スケール (Rs=400) | `HMajRatingCommon.cpp` | `GameLogicService.CalcRating()` 内定数 |
| コイン最低保有額 `DEALER_BASE = 500` | `HMajDef.h` | `RatingService` 内定数 |
| グレードレベル対応 (十級=0〜九段=18) | `HMajDef.h` | `RatingService.GetGradeLevel()` |

---

## 10. Push 送信 (Server→Client) の実装規則

```csharp
// NG: 文字列リテラルを直接書く
await _hub.Clients.Client(connId).SendAsync("mjkc2e", packet);

// OK: Cmd 定数を使う
await _hub.Clients.Client(connId).SendAsync(Cmd.AutoMatching, packet);
await _hub.Clients.Group(roomId).SendAsync(Cmd.RoomState, packet);
await _hub.Clients.All.SendAsync(Cmd.ServerStatus, packet);
```

Push を受け取るクライアント側が未実装の場合は `// TODO: client handler (mjkc2e)` を残す。

---

## 11. サブ関数の処理戦略 — 完全移植を徹底する

**方針: 部分移植はしない。時間がかかっても 1 関数の依存チェーン全体を一度に移植し切る。**
後で「どこまで移植したか」を確認する手戻りが発生するため、
「とりあえず動く状態」で止めない。

### 11-1. C++ サブ関数の分類と .NET での扱い

C++ でサブ関数に見えるものの多くは「.NET の別手段で自動的に解決される」。
移植対象として意識する必要がないケースが大半。

| C++ サブ関数の種類 | .NET での自動代替 | 個別移植は不要 |
|------------------|-----------------|---------------|
| `GDBContext` / `GDBNumber` — Oracle 接続ラッパー | `OracleCommand` + パラメータ | ✅ 不要 |
| `GMetpParser` / `CBinPak` — パケット構築 | `SendAsync(Cmd.Xxx, new { ... })` — SignalR が直接 JSON 化 | ✅ 不要 |
| `pSocket->SendPacket(...)` — 送信 | `_hub.Clients.Client(connId).SendAsync(...)` | ✅ 不要 |
| `GLog::Printf(...)` — ログ | `_logger.LogInformation(...)` | ✅ 不要 |
| `HMajDBObject::GetInstance()->Xxx(...)` — シングルトン呼び出し | DI で `PlayerRepository` を受け取るだけ | ✅ 不要 |
| `InterlockedIncrement` / `InterlockedExchange` — スレッド安全 | `Interlocked.Increment` / `Interlocked.Exchange` — 1:1 置換 | ✅ 直訳 |
| `CTime` / `SYSTEMTIME` — 時刻 | `DateTime.Now` / `DateTime.UtcNow` | ✅ 直訳 |
| **ビジネスロジックのサブ関数** (CalcRating_*, CheckEnter*, etc.) | **個別に C# メソッドとして移植する** | ❌ **要移植** |

### 11-2. 移植手順 — ボトムアップで完全に実装する

ビジネスロジックのサブ関数は**呼ばれる側から先に移植する (ボトムアップ)**。
メインハンドラに着手する前に、依存するすべてのサブ関数を完成させる。

```
例: OnEndGame → CalcCupEvtScore → (内部ループのみ、サブ関数なし)

移植順序:
  Step 1.  CalcCupEvtScore を移植・完成 (サブ関数なし → 即コンパイル可)
  Step 2.  OnEndGameAsync を移植・完成 (Step 1 を呼ぶ)
  → ここで「完了」。後で再訪しない。
```

```
例: GameReportProcess → CalcMoney → GetRoomChargeCommon
                      → CalcRating → CalcRating_MajakType

移植順序:
  Step 1.  GetRoomChargeCommon を移植・完成
  Step 2.  CalcRating_MajakType を移植・完成
  Step 3.  CalcMoney を移植・完成 (Step 1 を呼ぶ)
  Step 4.  CalcRating を移植・完成 (Step 2 を呼ぶ)
  Step 5.  GameReportProcessAsync を移植・完成 (Step 3-4 を呼ぶ)
  → 全ステップ完了後に初めて「このコマンド移植完了」とみなす。
```

### 11-3. チェックリスト — 1 コマンドを移植する前に確認する

1. **呼び出し先サブ関数を列挙する**
   - C++ の関数呼び出しを `Ctrl+F` で探し、全サブ関数をリストアップする
2. **各サブ関数を 11-1 の表で分類する**
   - "自動代替" → リストから除外
   - "要移植" → ボトムアップで先に移植
3. **依存チェーン全体をボトムアップで移植する**
   - 葉 (サブ関数なし) から始めて根 (メインハンドラ) で終わる
   - 途中で止めない
4. **各ステップ後にビルドを確認する**
   - `dotnet build --no-restore` でエラーゼロを維持する
   - エラーが出たら **次のステップに進まない**
5. **全サブ関数が完成してからメインハンドラを実装する**
   - メインハンドラが呼ぶものがすべて存在する状態で書き始める

---

## 12. SJIS エンコード C++ ファイルの文字列リテラル読み取り規則

### 12-1. 問題の背景

`server/legacy/server/` 以下の C++ ファイル (`HMajCommon.h`, `HMajRatingCommon.cpp` 等) は
**Shift-JIS (CP932) エンコード**で保存されている。
VS Code や grep で開くと日本語文字が `'庶民'` → `'���ꕶ'` のように文字化けする。

> ⚠️ 実際の発生例 (2026-06):  
> `HMajCommon.h` の `s_szMajSLevel[]` が文字化けで読めず、  
> "見習い/初心者/平均/中級者..." と **推測で実装**してしまった。  
> 正しくは "無一文/金欠/庶民/平民/一般人/中流/上流/金持ち/富豪/大富豪/財閥" であり、  
> ユーザーに誤ったレベル名が表示され続けた。

### 12-2. 文字列リテラルを読む 3 つの手段 (必ずいずれかを使う)

**手段 A: PowerShell で正しいエンコードで読む (最速)**

```powershell
# SJIS ファイルの特定キーワードを正しく読む
Get-Content "server/legacy/server/HMajCommon.h" -Encoding Default | Select-String "SLevel\|s_sz"
Get-Content "server/legacy/server/HMajRatingCommon.cpp" -Encoding Default | Select-String "GetSLevel\|return"
```

**手段 B: 同じ文字列を使っている SQL ストアドプロシージャを参照する**

`server/legacy/server/PROCEDURE/*.sql` は同じ SJIS だが、
PowerShell `-Encoding Default` で正常に読める。
同一の定数が SQL にも存在することが多い (例: `PC_MAJAK2_HIST.sql` の `V_SLEVEL`)。

```powershell
Get-Content "server/legacy/server/PROCEDURE/PC_MAJAK2_HIST.sql" -Encoding Default |
    Select-String "SLEVEL"
```

**手段 C: `.sql` に存在しない場合 — Python で SJIS デコード**

```python
with open("server/legacy/server/HMajCommon.h", encoding="cp932", errors="replace") as f:
    for line in f:
        if "SLevel" in line or "s_sz" in line:
            print(line, end="")
```

### 12-3. 絶対禁止事項

| ❌ やってはいけないこと | 理由 |
|------------------------|------|
| SJIS ファイルを文字化けのまま読んで文字列を**推測・創作**する | 必ず間違いになる |
| 「日本語ゲームでよくある言葉」を使って定数を埋める | レガシーと一致する保証がない |
| 定数の正しさを確認せずにテストを書かない | バグが長期間気づかれない |

### 12-4. 文字列テーブルの典型的な確認対象ファイル

| C++ ファイル (SJIS) | 確認すべき内容 | 対応 SQL / 代替確認先 |
|---------------------|-------------|---------------------|
| `HMajCommon.h` → `s_szMajSLevel[]` | コインレベル称号 (無一文〜財閥) | `PC_MAJAK2_HIST.sql` の `V_SLEVEL` |
| `HMajCommon.h` → `s_stEnterGradeModeCond[]` | 段位戦進入条件テキスト | `HMajChnlServer.cpp` のエラーメッセージ |
| `HMajCommon.h` → `MAJAK_STR_*` マクロ | 通知メッセージ文字列 | `HMajChnlServer.cpp` の `SendNoticeToAll` 呼び出し |
| `HMajDef.h` → 定数定義 | 数値定数 (読めることが多い) | 直接読めれば OK |
