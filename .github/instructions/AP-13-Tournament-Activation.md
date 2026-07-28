# トーナメントシステム 有効化ガイド

作成日: 2026-06-25  
最終更新: 2026-06-25 (全項目対応完了)  
対象: server/Services/TournamentService.cs, TournamentRepository.cs, TournamentBackgroundService.cs

---

## 現在の実装状態

コア実装（登録・参加・対戦組み合わせ・状態遷移）は完成済み。  
バグ修正・未実装項目はすべて対応完了。本番稼動可能な状態。

---

## ✅ 修正済み

### 1. トーナメント順位情報の対応ミス — **修正済み**
**ファイル:** `server/Repositories/MySQL/TournamentRepository.cs` の `UpdateDetailResultAsync()`

```csharp
room.Rank1MemberNo = detail.GradeMemberNo[0];
room.Rank2MemberNo = detail.GradeMemberNo[1];
room.Rank3MemberNo = detail.GradeMemberNo[2];
room.Rank4MemberNo = detail.GradeMemberNo[3];
```

**修正前の問題:** `:gn3`/`:gn4` が `GradeMemberId` を参照していた → GRADENO3/4 カラムにMemberIDが書き込まれるデータ破壊バグ。

### 2. 賞金配布ロジック — **実装済み**
**ファイル:** `server/Services/TournamentService.cs` → `PostMatchingCoreAsync()`

決勝終了時に `GradeMoney[0..3]` を各順位プレイヤーへ配布。
- オンライン: `GameMoneyService.AddMoneyAsync` + `mjkk92e` 通知
- オフライン: ログ記録のみ (Present メールシステムは別途対応)

### 3. NPC (`"*AI*"`) プレイヤーのフィルタリング — **実装済み**
**ファイル:** `server/Services/TournamentService.cs` → `GoMatchingAsync()`

`detail.MemberId` から `NpcMemberId` と空文字を除外した実プレイヤーのみ通知。

### 4. 同時アクセス競合対策 — **実装済み**
**ファイル:** `server/Services/TournamentService.cs` → `PostMatchingAsync()`

`_lock` (SemaphoreSlim) を取得してから `PostMatchingCoreAsync()` を実行。
`_plans`/`_details` の同時書き込み区間を保護。

### 5. 主催者によるキャンセル — **実装済み**
**ファイル:** `server/Services/TournamentService.cs` → `CancelPlanAsync(seqNo, organizerId)`

- 主催者確認 → `MATCHSTARTDT` 前かチェック → 全参加者に参加費返金
- プラン状態を `Reject` に変更 + 参加者を `Exit` に一括更新

---

## 動作確認手順 (ステップバイステップ)

```
1. dotnet test で全テスト (1041件) 通過を確認。

2. MySQLゲームDBに以下のテーブルが存在するか確認:
   - tournament_plan
   - tournament_room
   - tournament_participant
   - tournament_limit
   - tournament_session

3. `ConnectionStrings:GameDatabase` がMySQLゲームDBに向いていることを確認

4. サーバー起動ログで TournamentService.InitAsync() が正常完了することを確認
   (例: "Tournament plans loaded: 0")

5. クライアントから mjkc26e (TournamentList) を送信して result=1 が返ることを確認

6. mjkc27e (TournamentRegist) でテスト用トーナメントを登録
   - JoinStartDt を数分後、MatchStartDt をさらに数分後に設定

7. TournamentBackgroundService が30秒ごとに tick する様子をログで確認
   - Join→Wait→Play→End の遷移をログで追跡

8. 決勝終了後: ログに "Tournament {SeqNo} ended." + 賞金配布ログが出ることを確認
```

---

## ファイル参照一覧

| ファイル | 役割 |
|----------|------|
| `server/Services/TournamentService.cs` | 状態遷移ロジック・ブラケット生成 |
| `server/Infrastructure/TournamentBackgroundService.cs` | 30秒タイマー・状態遷移トリガー |
| `server/Repositories/MySQL/TournamentRepository.cs` | DB CRUD (`tournament_*` テーブル) |
| `server/Commands/Channel/MiscCommands.cs` | クライアントコマンド (mjkc26e〜30e) |
| `server/Models/Game/TournamentModels.cs` | 定数・モデルクラス |
| `server/Hubs/MajakGameHub.cs` | コマンドルーティング登録 |
