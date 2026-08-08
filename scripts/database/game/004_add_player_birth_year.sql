-- 既存の majak_game に1回だけ適用する。
ALTER TABLE player_account
    ADD COLUMN birth_year SMALLINT UNSIGNED NULL AFTER sex_code;