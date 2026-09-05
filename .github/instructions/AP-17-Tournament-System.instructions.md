---
applyTo: "server/**,server.tests/**,client/**"
description: "トーナメントの登録、参加、ブラケット生成、NPC補完、状態遷移、結果・賞金処理を確認または変更するときに参照する"
---

# AP-17 トーナメントシステム

## 1. 対象と正本

- 通信commandはAP-05、DB境界はAP-10、通貨はAP-16、primary実行制御はAP-04を正本とする。
- 主な実装は`TournamentService`、`TournamentBackgroundService`、`TournamentRepository`、`TournamentModels`、`mjkc26e`〜`mjkc30e` commandである。
- clientへ送る参加者識別子は公開`pix`とし、内部`member_no`を公開しない。

## 2. 状態遷移

```text
登録・参加
  -> PreMatching
  -> Wait (ブラケット確定)
  -> Play (room予約・対局開始)
  -> PostMatching (結果集計)
  -> End または次round

不成立・主催者cancel
  -> Reject
```

- `TournamentBackgroundService`はprimary leaderだけが状態遷移を進める。複数serverで同じplanを同時処理してはならない。
- plan/detailの共有collectionを変更する区間は`SemaphoreSlim`で保護する。
- join countがbracket定員の半数以下なら不成立とし、半数を超えた場合は不足seatをNPC placeholderで補完して`Wait`へ進める。
- 予約roomは参加者を固定し、開始時刻に接続中の実playerへ開始通知を送る。未接続playerはexitとして扱う。

## 3. 登録・参加・cancel

- 登録時はrule形式、参加期間、開始日時、定員、参加GP、passwordをserver側で検証する。
- 参加時は募集期間、定員、password、GP、重複参加を再検証する。clientの価格や参加可否を信頼しない。
- 主催者cancelは主催者本人かつ対局開始前だけ許可し、参加費相当のmoney presentを全参加者へ作成してplanを`Reject`、参加者を`Exit`へ更新する。
- 参加費は参加時にGP残高から差し引き、返却は`player_present`の未受取レコードとして配布する。返却時点でGP残高へ直接加算しない。
- GP差引きとpresent受取時の残高反映・履歴はAP-16の規約に従う。

## 4. ブラケットとNPC

- NPC placeholderは`*AI*`として管理し、通知、本人照合、賞金付与、永続player更新の対象から除外する。
- bracket生成後のseat、member、round対応を途中で並べ替えない。
- 二戦制決勝はtotal resultを第一順位条件とし、同点時はレガシーtournament result keyのtie-break順を使う。
- 全detailが終了したときだけplanを`End`へ進める。未完了detailがある状態で賞金を確定しない。

## 5. 結果・賞金

- result更新時の1〜4位member対応を同じindexで保存し、member IDとgrade/member number列を取り違えない。
- 決勝終了時は`GradeMoney[0..3]`を対応順位の`player_present`未受取レコードとして作成する。NPCと空seatには作成しない。受取処理で初めてGP残高へ反映する。
- present作成とオンライン通知を同一視しない。通知不能でも永続処理の結果を明確に記録する。
- result・cancel・強制stop処理は冪等にし、background tickや再起動で同じ賞金・返却・順位を二重反映しない。
- 現行`player_present`にはtournament配布用の一意制約がなく、present追加とplan status更新も同一transactionではない。関連処理を変更するときは、`seqNo + memberNo + present type/kind`等の安定した配布識別子と一意制約または同等の重複防止を追加するまで、再試行を安全とみなしてはならない。

## 6. 検証項目

- 不正schedule、rule、capacity、GP、password、duplicate join、late cancelを拒否する。
- 定員4で参加者3人の場合、実player 3人とNPC 1人のbracketを作る。
- 開始時に欠席者をexitへ更新し、接続中参加者だけへ公開`pix`で通知する。
- 二戦制決勝のtotal scoreとtie-break、全detail終了後の`End`遷移を確認する。
- cancel返却と決勝賞金presentが再実行で重複しないことを確認する。現行実装にはDBレベルの重複防止がないため、この検証なしに冪等と判定しない。