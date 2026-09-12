-- Nestify M3: expenses, meals, contributions, and monthly settlement.
--
-- Run this after the users and homes tables exist:
--   1. Autthintication.sql
--   2. User_Home.sql
--
-- The statements are safe to run after nestify-schema.sql as well. Existing
-- rows are preserved, and the missing payment columns are added below.
--
-- Categories:
--   expenses.category: 1 = EqualSplit, 2 = MealPurchase
--   contributions.fund_type: 1 = MealFund, 2 = SharedBills
--   contributions.source: 1 = DerivedFromExpense, 2 = DirectCashIn

BEGIN;

CREATE TABLE IF NOT EXISTS expenses (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id            bigint NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    category            smallint NOT NULL,
    description         varchar(200) NOT NULL,
    amount              numeric(18,2) NOT NULL,
    spent_by_user_id    bigint NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    spent_on            date NOT NULL,
    period_year         int NOT NULL,
    period_month        int NOT NULL,
    corrects_expense_id bigint REFERENCES expenses (id) ON DELETE RESTRICT,
    created_by_user_id  bigint NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    created_at_utc      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_expense_category CHECK (category BETWEEN 1 AND 2),
    CONSTRAINT ck_expense_year CHECK (period_year BETWEEN 2020 AND 2100),
    CONSTRAINT ck_expense_month CHECK (period_month BETWEEN 1 AND 12)
);

CREATE TABLE IF NOT EXISTS expense_shares (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    expense_id   bigint NOT NULL REFERENCES expenses (id) ON DELETE CASCADE,
    user_id      bigint NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    share_amount numeric(18,2) NOT NULL
);

CREATE TABLE IF NOT EXISTS meal_entries (
    id                       bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id                 bigint NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    user_id                  bigint NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    meal_date                date NOT NULL,
    meal_count               numeric(4,1) NOT NULL,
    period_year              int NOT NULL,
    period_month             int NOT NULL,
    supersedes_meal_entry_id bigint REFERENCES meal_entries (id) ON DELETE RESTRICT,
    recorded_by_user_id      bigint NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    recorded_at_utc          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_meal_count CHECK (meal_count BETWEEN 0 AND 10),
    CONSTRAINT ck_meal_month CHECK (period_month BETWEEN 1 AND 12)
);

CREATE TABLE IF NOT EXISTS meal_entry_audits (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    meal_entry_id  bigint NOT NULL REFERENCES meal_entries (id) ON DELETE RESTRICT,
    house_id       bigint NOT NULL,
    target_user_id bigint NOT NULL,
    actor_user_id  bigint NOT NULL,
    old_meal_count numeric(4,1),
    new_meal_count numeric(4,1) NOT NULL,
    reason         varchar(200),
    occurred_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS contributions (
    id                       bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id                 bigint NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    user_id                  bigint NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    amount                   numeric(18,2) NOT NULL,
    paid_on                  date NOT NULL,
    period_year              int NOT NULL,
    period_month             int NOT NULL,
    source                   smallint NOT NULL,
    fund_type                smallint NOT NULL DEFAULT 1,
    note                     varchar(200) NOT NULL DEFAULT '',
    source_expense_id        bigint REFERENCES expenses (id) ON DELETE RESTRICT,
    corrects_contribution_id bigint REFERENCES contributions (id) ON DELETE RESTRICT,
    recorded_by_user_id      bigint NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    created_at_utc           timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_contribution_source CHECK (source BETWEEN 1 AND 2),
    CONSTRAINT ck_contribution_fund_type CHECK (fund_type BETWEEN 1 AND 2),
    CONSTRAINT ck_contribution_month CHECK (period_month BETWEEN 1 AND 12)
);

-- Existing databases created before fund_type/note were introduced.
ALTER TABLE contributions
    ADD COLUMN IF NOT EXISTS fund_type smallint NOT NULL DEFAULT 1;

ALTER TABLE contributions
    ADD COLUMN IF NOT EXISTS note varchar(200) NOT NULL DEFAULT '';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_contribution_fund_type'
          AND conrelid = 'contributions'::regclass
    ) THEN
        ALTER TABLE contributions
            ADD CONSTRAINT ck_contribution_fund_type CHECK (fund_type BETWEEN 1 AND 2);
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS settlement_runs (
    id                         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id                   bigint NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    period_year                int NOT NULL,
    period_month               int NOT NULL,
    total_meal_spending        numeric(18,2) NOT NULL,
    total_meals                numeric(10,1) NOT NULL,
    per_meal_rate              numeric(18,6) NOT NULL,
    total_equal_costs          numeric(18,2) NOT NULL,
    member_count_at_settlement int NOT NULL,
    status                     smallint NOT NULL DEFAULT 1,
    computed_by_user_id        bigint NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    computed_at_utc            timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_settlement_status CHECK (status BETWEEN 1 AND 2),
    CONSTRAINT ck_settlement_month CHECK (period_month BETWEEN 1 AND 12)
);

CREATE TABLE IF NOT EXISTS settlement_lines (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    settlement_run_id   bigint NOT NULL REFERENCES settlement_runs (id) ON DELETE CASCADE,
    user_id             bigint NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    meal_count          numeric(10,1) NOT NULL,
    meal_cost           numeric(18,2) NOT NULL,
    equal_share         numeric(18,2) NOT NULL,
    contributions       numeric(18,2) NOT NULL,
    rounding_adjustment numeric(18,2) NOT NULL DEFAULT 0.00,
    net_amount          numeric(18,2) NOT NULL
);

CREATE TABLE IF NOT EXISTS settlement_transfers (
    id                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    settlement_run_id bigint NOT NULL REFERENCES settlement_runs (id) ON DELETE CASCADE,
    from_user_id      bigint NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    to_user_id        bigint NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    amount            numeric(18,2) NOT NULL,
    CONSTRAINT ck_transfer_amount CHECK (amount > 0),
    CONSTRAINT ck_transfer_distinct CHECK (from_user_id <> to_user_id)
);

CREATE INDEX IF NOT EXISTS ix_expense_house_period
    ON expenses (house_id, period_year, period_month, category);
CREATE INDEX IF NOT EXISTS ix_expense_corrects
    ON expenses (corrects_expense_id) WHERE corrects_expense_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_expense_share
    ON expense_shares (expense_id, user_id);
CREATE INDEX IF NOT EXISTS ix_meal_current
    ON meal_entries (house_id, user_id, meal_date, recorded_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_meal_house_period
    ON meal_entries (house_id, period_year, period_month);
CREATE INDEX IF NOT EXISTS ix_meal_audit_house
    ON meal_entry_audits (house_id, occurred_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_contribution_house_period
    ON contributions (house_id, period_year, period_month);
CREATE INDEX IF NOT EXISTS ix_contribution_house_period_fund
    ON contributions (house_id, period_year, period_month, fund_type);
CREATE UNIQUE INDEX IF NOT EXISTS ux_contribution_expense
    ON contributions (source_expense_id) WHERE source_expense_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_settlement_finalized
    ON settlement_runs (house_id, period_year, period_month) WHERE status = 2;
CREATE UNIQUE INDEX IF NOT EXISTS ux_settlement_line
    ON settlement_lines (settlement_run_id, user_id);
CREATE INDEX IF NOT EXISTS ix_settlement_line_user
    ON settlement_lines (user_id);
CREATE INDEX IF NOT EXISTS ix_settlement_run_house_period
    ON settlement_runs (house_id, period_year, period_month);
CREATE INDEX IF NOT EXISTS ix_settlement_transfer_run
    ON settlement_transfers (settlement_run_id);

COMMIT;
