-- M3 settlement compatibility additions for the current Dapper schema.
-- Run after nestify-schema.sql and User_Home.sql.
-- Existing contributions remain meal-fund entries for backward compatibility.

ALTER TABLE contributions
    ADD COLUMN IF NOT EXISTS fund_type smallint NOT NULL DEFAULT 1;

ALTER TABLE contributions
    ADD COLUMN IF NOT EXISTS note varchar(200) NOT NULL DEFAULT '';

ALTER TABLE contributions
    DROP CONSTRAINT IF EXISTS ck_contribution_fund_type;

ALTER TABLE contributions
    ADD CONSTRAINT ck_contribution_fund_type CHECK (fund_type BETWEEN 1 AND 2);

CREATE INDEX IF NOT EXISTS ix_contribution_house_period_fund
    ON contributions (house_id, period_year, period_month, fund_type);

CREATE INDEX IF NOT EXISTS ix_settlement_run_house_period
    ON settlement_runs (house_id, period_year, period_month);

CREATE INDEX IF NOT EXISTS ix_settlement_line_user
    ON settlement_lines (user_id);

CREATE INDEX IF NOT EXISTS ix_settlement_transfer_run
    ON settlement_transfers (settlement_run_id);
