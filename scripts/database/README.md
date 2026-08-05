# MySQL データベース構築スクリプト

ゲームデータとログデータは、物理的に分離した2つのMySQLデータベースと接続プールを使用する。

## 新規構築手順

1. ゲームDBを作成し、`game/001_create_tables.sql` を実行する。
2. ゲームDBに `game/002_seed_data.sql` を実行する。
3. ログDBを作成し、`log/001_create_tables.sql` を実行する。
4. ログDBに `log/002_seed_data.sql` を実行する。現在、ログDBの初期データはない。
5. `ConnectionStrings:GameDatabase` と `ConnectionStrings:LogDatabase`、または同等のAWS Parameter Store項目を設定する。

推奨データベース名:

```sql
CREATE DATABASE majak_game CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE DATABASE majak_log  CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
```

## 運用原則

- 4つのSQLファイルは新規構築用の最終基準であり、既存の運用DBには再適用しない。
- 既存運用DBには、作成番号順に追加マイグレーションを1回だけ適用する。
- ゲームDB・ログDBともに外部キー制約を使用しない。
- ログDB用ユーザーにはゲームDBへの権限を付与しない。
- ゲームDB用ユーザーにはゲーム状態の読み書きを許可する。
- ログDB用ユーザーには追記型履歴のINSERTと参照に必要な権限のみを付与する。

## 通貨モデル移行

- 新規DBは `001_create_tables.sql` と `002_seed_data.sql` により、キャッシュ (`cash_*`) と龍珠 (`gem_count`) を別の残高として作成する。
- テーブル名だけが `cash_*` へ変更済みで、`gem_amount` が残っている既存DBは、`game/003_recover_cash_amount_columns.sql` を1回だけ適用して `cash_amount` へ変更する。
- ログDBは `log/003_add_gem_balance_breakdown.sql` を1回適用し、キャッシュ取引ログへ改名して内訳カラムを追加する。