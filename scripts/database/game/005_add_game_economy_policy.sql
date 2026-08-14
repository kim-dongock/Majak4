-- 既存 majak_game DB 用: 運営が編集できるGP経済ポリシー
CREATE TABLE IF NOT EXISTS game_economy_policy (
    policy_id                 TINYINT UNSIGNED NOT NULL DEFAULT 1,
    initial_gp                BIGINT NOT NULL DEFAULT 1000,
    free_replenish_target_gp  BIGINT NOT NULL DEFAULT 1000,
    free_replenish_daily_limit INT UNSIGNED NOT NULL DEFAULT 1,
    updated_at                DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                          ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (policy_id),
    CHECK (policy_id = 1),
    CHECK (initial_gp >= 0),
    CHECK (free_replenish_target_gp >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO game_economy_policy
    (policy_id, initial_gp, free_replenish_target_gp, free_replenish_daily_limit)
VALUES
    (1, 1000, 1000, 1)
ON DUPLICATE KEY UPDATE policy_id = VALUES(policy_id);