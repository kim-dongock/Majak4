-- 既存 majak_game DB 用: 対局GP精算の履歴コード
INSERT INTO transaction_code_master
    (transaction_code, code_title, is_history_enabled, is_cumulative, open_status, start_date, content, service_code, service_name, is_service_enabled, game_id, registrant_name, planner_name, developer_name, direction_code, avatar_code)
VALUES
    ('JM00071', '対局精算', TRUE, TRUE, 'P', '2026-08-24', NULL, NULL, '麻雀４', TRUE, 'MAJAK4', 'system', 'game', 'system', 'I', NULL)
ON DUPLICATE KEY UPDATE transaction_code = VALUES(transaction_code);