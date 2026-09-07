-- Homes created by users, and the rows that say who lives in which home.
--
-- A user creates a home and becomes its manager. Everyone else either joins with
-- the home's join code or is added by the manager / a co-manager. A user is in at
-- most one home at a time.
--
-- Needs users (Autthintication.sql) to exist first.

DROP TABLE IF EXISTS home_members CASCADE;
DROP TABLE IF EXISTS homes CASCADE;


CREATE TABLE homes (
    id                 bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name               varchar(120)  NOT NULL,
    address_line       varchar(300)  NOT NULL,
    area_name          varchar(120)  NOT NULL DEFAULT '',
    division           varchar(60)   NOT NULL DEFAULT '',
    latitude           numeric(9,6)  NOT NULL DEFAULT 0,
    longitude          numeric(9,6)  NOT NULL DEFAULT 0,
    join_code          varchar(12)   NOT NULL,      -- short code a housemate types to join
    created_by_user_id bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    created_at_utc     timestamptz   NOT NULL DEFAULT now(),
    updated_at_utc     timestamptz   NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_homes_join_code ON homes (join_code);
CREATE INDEX        ix_homes_creator   ON homes (created_by_user_id);


-- One row per user per home. left_at_utc NULL means the user still lives there;
-- a member who leaves keeps the old row so past records still make sense.
CREATE TABLE home_members (
    id            bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    home_id       bigint      NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    user_id       bigint      NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    role          smallint    NOT NULL DEFAULT 3,   -- 1 Manager, 2 Co-manager, 3 Member
    joined_at_utc timestamptz NOT NULL DEFAULT now(),
    left_at_utc   timestamptz,

    CONSTRAINT ck_home_member_role CHECK (role BETWEEN 1 AND 3)
);

-- One active row per user per home, one manager per home, and one home per user.
CREATE UNIQUE INDEX ux_home_member_active  ON home_members (home_id, user_id) WHERE left_at_utc IS NULL;
CREATE UNIQUE INDEX ux_home_single_manager ON home_members (home_id)          WHERE role = 1 AND left_at_utc IS NULL;
CREATE UNIQUE INDEX ux_home_member_user    ON home_members (user_id)          WHERE left_at_utc IS NULL;


-- ========================= Join requests =========================
--
-- Typing a join code does not put a user in the house. It files a request here,
-- which the manager or a co-manager approves or rejects. A user has at most one
-- pending request at a time, and rejected or cancelled rows are kept so the
-- history stays readable.
--
-- Safe to run on its own if the two tables above already exist.

DROP TABLE IF EXISTS home_join_requests CASCADE;

CREATE TABLE home_join_requests (
    id                 bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    home_id            bigint      NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    user_id            bigint      NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    status             smallint    NOT NULL DEFAULT 1,   -- 1 Pending, 2 Approved, 3 Rejected, 4 Cancelled
    requested_at_utc   timestamptz NOT NULL DEFAULT now(),
    decided_at_utc     timestamptz,
    decided_by_user_id bigint      REFERENCES users (id) ON DELETE SET NULL,

    CONSTRAINT ck_join_request_status CHECK (status BETWEEN 1 AND 4)
);

CREATE UNIQUE INDEX ux_join_request_pending ON home_join_requests (user_id) WHERE status = 1;
CREATE INDEX        ix_join_requests_home   ON home_join_requests (home_id, status);
