-- 既存の majak_game に1回だけ適用する。
ALTER TABLE player_account
    ADD COLUMN user_color CHAR(7) NOT NULL DEFAULT '#1b6b55' AFTER avatar_id;