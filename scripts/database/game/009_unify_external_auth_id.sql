-- Unify Google and Hange identities without sharing their external IDs with member_no.
-- Apply once to an existing majak_game database before deploying the matching server build.

ALTER TABLE player_account
    CHANGE COLUMN google_sub external_auth_id VARCHAR(255) NULL;

UPDATE player_account
SET external_auth_id = CONCAT('google:', external_auth_id)
WHERE external_auth_id IS NOT NULL
  AND external_auth_id NOT LIKE 'google:%'
  AND external_auth_id NOT LIKE 'hange:%';

-- Before this migration, Hange userno was stored directly as member_no.
UPDATE player_account
SET external_auth_id = CONCAT('hange:', member_no)
WHERE external_auth_id IS NULL
  AND source_environment IN ('test', 'production');

ALTER TABLE player_account
    DROP INDEX idx_player_account_google_sub,
    ADD UNIQUE KEY idx_player_account_external_auth_id (external_auth_id);
