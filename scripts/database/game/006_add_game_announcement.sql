-- 既存 majak_game DB 用: 管理者が編集するゲーム内お知らせ記事
CREATE TABLE IF NOT EXISTS game_announcement (
    announcement_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    title           VARCHAR(120) NOT NULL,
    body            TEXT NOT NULL,
    is_published    BOOLEAN NOT NULL DEFAULT FALSE,
    is_startup      BOOLEAN NOT NULL DEFAULT FALSE,
    published_at    DATETIME(3) NULL,
    created_at      DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                              ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (announcement_id),
    INDEX idx_game_announcement_startup (is_published, is_startup, updated_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;