-- Admin console: reports on housing posts, admin takedowns of housing and
-- marketplace posts, verification fees, posting plans, admin accounts and the
-- audit log every admin action is written to.
--
-- Needs users (Autthintication.sql), housing_posts (Housing.sql) and
-- marketplace_listings (Marketplace.sql) to exist first.

DROP TABLE IF EXISTS admin_audit_log        CASCADE;
DROP TABLE IF EXISTS admin_accounts         CASCADE;
DROP TABLE IF EXISTS plan_purchases         CASCADE;
DROP TABLE IF EXISTS post_plans             CASCADE;
DROP TABLE IF EXISTS fee_settings           CASCADE;
DROP TABLE IF EXISTS post_takedowns         CASCADE;
DROP TABLE IF EXISTS housing_reports        CASCADE;
DROP TABLE IF EXISTS housing_report_reasons CASCADE;


-- ========================= Housing report reasons =========================
-- Same idea as marketplace_report_reasons: adding a reason is an INSERT.

CREATE TABLE housing_report_reasons (
    id         smallint    PRIMARY KEY,
    name       varchar(60) NOT NULL,
    sort_order smallint    NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX ux_housing_report_reasons_name ON housing_report_reasons (name);

INSERT INTO housing_report_reasons (id, name, sort_order) VALUES
    (1, 'Fake listing',                    1),
    (2, 'Misleading photos or rent',       2),
    (3, 'Duplicate post',                  3),
    (4, 'Abusive or improper content',     4),
    (5, 'Scam or advance payment demand',  5),
    (6, 'Wrong rent',                      6),
    (7, 'Other',                           7);


-- ========================= Housing reports =========================
-- A user reports a housing post; admins see the queue on the moderation page
-- and either take the post down or dismiss the report.
-- state: 1 Open, 2 Resolved (post taken down), 3 Dismissed.

CREATE TABLE housing_reports (
    id                   bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    post_id              bigint       NOT NULL REFERENCES housing_posts (id) ON DELETE CASCADE,
    reported_by_user_id  bigint       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    reason_id            smallint     NOT NULL REFERENCES housing_report_reasons (id) ON DELETE RESTRICT,
    details              varchar(300),
    state                smallint     NOT NULL DEFAULT 1,   -- 1 Open, 2 Resolved, 3 Dismissed
    raised_at_utc        timestamptz  NOT NULL DEFAULT now(),
    decided_at_utc       timestamptz,
    decided_by_admin_id  bigint       REFERENCES users (id) ON DELETE SET NULL,

    CONSTRAINT ck_housing_report_state CHECK (state BETWEEN 1 AND 3)
);

CREATE INDEX        ix_housing_reports_queue   ON housing_reports (state, raised_at_utc);
CREATE INDEX        ix_housing_reports_post    ON housing_reports (post_id);
CREATE UNIQUE INDEX ux_housing_report_one_open ON housing_reports (post_id, reported_by_user_id) WHERE state = 1;


-- ========================= Takedowns =========================
-- One row each time an admin strikes a post down, for housing (scope 1) and
-- marketplace (scope 2) alike. post_id points at housing_posts or
-- marketplace_listings depending on scope, so there is no foreign key.
-- A housing post with an open takedown (restored_at_utc IS NULL) is hidden
-- from browse; a marketplace listing is also set to status 3 (Removed) and
-- put back to 1 (Active) on restore.

CREATE TABLE post_takedowns (
    id                   bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    scope                smallint     NOT NULL,   -- 1 Housing, 2 Marketplace
    post_id              bigint       NOT NULL,
    admin_user_id        bigint       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    reason               varchar(400) NOT NULL,
    taken_down_at_utc    timestamptz  NOT NULL DEFAULT now(),
    restored_at_utc      timestamptz,
    restored_by_admin_id bigint       REFERENCES users (id) ON DELETE SET NULL,

    CONSTRAINT ck_post_takedown_scope CHECK (scope BETWEEN 1 AND 2)
);

CREATE INDEX        ix_post_takedowns_post     ON post_takedowns (scope, post_id);
CREATE UNIQUE INDEX ux_post_takedown_one_open  ON post_takedowns (scope, post_id) WHERE restored_at_utc IS NULL;


-- ========================= Fee settings =========================
-- One-off charges the platform takes. code is what the API looks a fee up by;
-- the amount is the only thing the admin page changes.

CREATE TABLE fee_settings (
    code           varchar(40)   PRIMARY KEY,
    label          varchar(80)   NOT NULL,
    description    varchar(200)  NOT NULL,
    amount_bdt     numeric(10,2) NOT NULL,
    sort_order     smallint      NOT NULL DEFAULT 0,
    updated_at_utc timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_fee_amount CHECK (amount_bdt >= 0)
);

INSERT INTO fee_settings (code, label, description, amount_bdt, sort_order) VALUES
    ('user_verification',   'User verification',            'Charged once when a user submits an NID or student ID for verification.', 100, 1),
    ('helper_verification', 'Domestic helper verification', 'Charged when a helper applies, covers the manual document check.',        150, 2),
    ('helper_reverify',     'Helper re-verification',       'Charged when a declined helper applies again with new documents.',        80,  3),
    ('featured_listing',    'Featured listing (per week)',  'Pins a housing or marketplace post to the top of search results.',        250, 4);


-- ========================= Posting plans =========================
-- A plan is a bundle of posts someone buys once. scope: 1 Housing, 2 Marketplace.
-- A disabled plan stays so old purchases still point at it.

CREATE TABLE post_plans (
    id             bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name           varchar(60)   NOT NULL,
    scope          smallint      NOT NULL,   -- 1 Housing, 2 Marketplace
    posts          int           NOT NULL,
    price_bdt      numeric(10,2) NOT NULL,
    valid_days     int           NOT NULL,
    is_active      boolean       NOT NULL DEFAULT true,
    created_at_utc timestamptz   NOT NULL DEFAULT now(),
    updated_at_utc timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_post_plan_scope CHECK (scope BETWEEN 1 AND 2),
    CONSTRAINT ck_post_plan_posts CHECK (posts >= 1),
    CONSTRAINT ck_post_plan_price CHECK (price_bdt >= 0),
    CONSTRAINT ck_post_plan_days  CHECK (valid_days >= 1)
);

CREATE INDEX ix_post_plans_scope ON post_plans (scope, is_active);

INSERT INTO post_plans (name, scope, posts, price_bdt, valid_days) VALUES
    ('Starter',        1, 10,  20, 30),
    ('Landlord',       1, 30,  50, 60),
    ('Casual seller',  2, 20,  10, 30),
    ('Regular seller', 2, 60,  25, 60);


-- ========================= Plan purchases =========================
-- One row per plan bought. posts_left goes down as the buyer posts.

CREATE TABLE plan_purchases (
    id               bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    plan_id          bigint      NOT NULL REFERENCES post_plans (id) ON DELETE RESTRICT,
    user_id          bigint      NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    posts_left       int         NOT NULL,
    purchased_at_utc timestamptz NOT NULL DEFAULT now(),
    expires_at_utc   timestamptz NOT NULL,

    CONSTRAINT ck_plan_purchase_posts_left CHECK (posts_left >= 0)
);

CREATE INDEX ix_plan_purchases_plan ON plan_purchases (plan_id);
CREATE INDEX ix_plan_purchases_user ON plan_purchases (user_id, expires_at_utc DESC);


-- ========================= Admin accounts =========================
-- Extra details for a users row with account_type 3. An admin with no row
-- here counts as active with full access. A suspended admin cannot sign in.

CREATE TABLE admin_accounts (
    user_id            bigint      PRIMARY KEY REFERENCES users (id) ON DELETE CASCADE,
    scope              varchar(40) NOT NULL DEFAULT 'Full access',   -- Full access, Verification, Moderation, Finance
    is_active          boolean     NOT NULL DEFAULT true,
    created_by_user_id bigint      REFERENCES users (id) ON DELETE SET NULL,
    created_at_utc     timestamptz NOT NULL DEFAULT now(),
    suspended_at_utc   timestamptz
);


-- ========================= Audit log =========================
-- Every admin decision, written by the API. Rows are never updated or deleted.
-- kind is a hint for the UI colour: 'ok', 'danger' or 'neutral'.

CREATE TABLE admin_audit_log (
    id            bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    admin_user_id bigint       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    action        varchar(60)  NOT NULL,
    target        varchar(200) NOT NULL,
    note          varchar(500),
    kind          varchar(10)  NOT NULL DEFAULT 'neutral',
    at_utc        timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_admin_audit_kind CHECK (kind IN ('ok', 'danger', 'neutral'))
);

CREATE INDEX ix_admin_audit_time  ON admin_audit_log (at_utc DESC);
CREATE INDEX ix_admin_audit_admin ON admin_audit_log (admin_user_id, at_utc DESC);
