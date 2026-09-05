-- ========================= Users and roles =========================

CREATE TABLE roles (
    id          smallint     PRIMARY KEY,
    code        varchar(40)  NOT NULL,
    name        varchar(60)  NOT NULL,
    description varchar(200)
);

CREATE UNIQUE INDEX ux_roles_code ON roles (code);

INSERT INTO roles (id, code, name, description) VALUES
    (1, 'User',           'User',            'Normal account, given at registration'),
    (2, 'DomesticHelper', 'Domestic Helper', 'Helper account, added on top of User'),
    (3, 'Admin',          'Administrator',   'Manages verification, reports and bans');


-- Only the fields entered on the registration form on /auth. Anything else
-- about a user lives in user_profiles and is joined by user_id.
CREATE TABLE users (
    id             bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    full_name      varchar(120) NOT NULL,
    email          varchar(256) NOT NULL,          -- always stored lower-case by the API
    password_hash  text         NOT NULL,
    phone_number   varchar(20)  NOT NULL,
    account_type   smallint     NOT NULL DEFAULT 1,   -- 1 = User, 2 = DomesticHelper, 3 = Admin
    created_at_utc timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_users_account_type CHECK (account_type BETWEEN 1 AND 3)
);

CREATE UNIQUE INDEX ux_users_email ON users (email);


-- Extra profile details, filled in after registration. One row per user.
CREATE TABLE user_profiles (
    user_id            bigint        PRIMARY KEY REFERENCES users (id) ON DELETE CASCADE,
    date_of_birth      date,
    gender             smallint,                          -- 1 Male, 2 Female, 3 Other
    occupation         smallint,                          -- 1 Student, 2 JobHolder, 3 Both, 4 Other
    marital_status     smallint,                          -- 1 Single, 2 Married, 3 Other
    profile_upazila_id int,
    is_verified        boolean     NOT NULL DEFAULT false,
    is_banned          boolean     NOT NULL DEFAULT false,
    updated_at_utc     timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_profile_gender         CHECK (gender IS NULL OR gender BETWEEN 1 AND 3),
    CONSTRAINT ck_profile_occupation     CHECK (occupation IS NULL OR occupation BETWEEN 1 AND 4),
    CONSTRAINT ck_profile_marital_status CHECK (marital_status IS NULL OR marital_status BETWEEN 1 AND 3)
);

CREATE INDEX ix_profiles_is_verified ON user_profiles (is_verified) WHERE is_verified;


CREATE TABLE user_roles (
    user_id        bigint      NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    role_id        smallint    NOT NULL REFERENCES roles (id) ON DELETE RESTRICT,
    granted_at_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, role_id)
);

CREATE INDEX ix_user_roles_role ON user_roles (role_id);


-- Contact details kept apart from users so they are not read by accident.
CREATE TABLE user_contact_info (
    user_id          bigint         PRIMARY KEY REFERENCES users (id) ON DELETE CASCADE,
    phone_number     varchar(20),
    whatsapp_number  varchar(20),
    facebook_handle  varchar(100),
    messenger_handle varchar(100),
    updated_at_utc   timestamptz  NOT NULL DEFAULT now()
);


CREATE TABLE refresh_tokens (
    id                   bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id              bigint      NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    token_hash           bytea       NOT NULL,       -- SHA-256 of the token, 32 bytes
    family_id            bigint,                     -- NULL on the first token of a login; children point at that root id
    expires_at_utc       timestamptz NOT NULL,
    created_at_utc       timestamptz NOT NULL DEFAULT now(),
    revoked_at_utc       timestamptz,
    replaced_by_token_id bigint      REFERENCES refresh_tokens (id),
    created_by_ip        inet,

    CONSTRAINT ck_refresh_token_hash_len CHECK (octet_length(token_hash) = 32)
);

CREATE UNIQUE INDEX ux_refresh_token_hash  ON refresh_tokens (token_hash);
CREATE INDEX        ix_refresh_user_active ON refresh_tokens (user_id) WHERE revoked_at_utc IS NULL;
CREATE INDEX        ix_refresh_family      ON refresh_tokens (family_id);


-- ========================= Area reference data =========================

CREATE TABLE divisions (
    id      int         PRIMARY KEY,
    name    varchar(60) NOT NULL,
    bn_name varchar(60) NOT NULL
);

CREATE TABLE districts (
    id          int          PRIMARY KEY,
    division_id int          NOT NULL REFERENCES divisions (id) ON DELETE RESTRICT,
    name        varchar(60)  NOT NULL,
    bn_name     varchar(60)  NOT NULL,
    latitude    numeric(9,6),
    longitude   numeric(9,6)
);

CREATE INDEX        ix_districts_division      ON districts (division_id);
CREATE UNIQUE INDEX ux_districts_division_name ON districts (division_id, name);

CREATE TABLE upazilas (
    id                    int          PRIMARY KEY,
    district_id           int          NOT NULL REFERENCES districts (id) ON DELETE RESTRICT,
    name                  varchar(80)  NOT NULL,
    bn_name               varchar(80)  NOT NULL,
    is_metropolitan_thana boolean      NOT NULL DEFAULT false
);

CREATE INDEX        ix_upazilas_district      ON upazilas (district_id);
CREATE UNIQUE INDEX ux_upazilas_district_name ON upazilas (district_id, name);

ALTER TABLE user_profiles
    ADD CONSTRAINT fk_profiles_upazila
    FOREIGN KEY (profile_upazila_id) REFERENCES upazilas (id) ON DELETE SET NULL;


-- ========================= M1: Housing =========================

CREATE TABLE houses (
    id                bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name              varchar(120) NOT NULL,
    address_line      varchar(300) NOT NULL,
    upazila_id        int          NOT NULL REFERENCES upazilas (id) ON DELETE RESTRICT,
    latitude          numeric(9,6),
    longitude         numeric(9,6),
    created_by_user_id bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    created_at_utc    timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX ix_houses_upazila ON houses (upazila_id);


-- House role of a user. This is per house, not a global role.
CREATE TABLE house_memberships (
    id            bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id      bigint        NOT NULL REFERENCES houses (id) ON DELETE CASCADE,
    user_id       bigint        NOT NULL REFERENCES users (id)  ON DELETE RESTRICT,
    role          smallint    NOT NULL,            -- 1 Manager, 2 CoManager, 3 Member
    joined_at_utc timestamptz NOT NULL DEFAULT now(),
    left_at_utc   timestamptz,                     -- NULL means still a member

    CONSTRAINT ck_membership_role CHECK (role BETWEEN 1 AND 3)
);

CREATE UNIQUE INDEX ux_membership_active    ON house_memberships (house_id, user_id) WHERE left_at_utc IS NULL;
CREATE INDEX        ix_membership_user      ON house_memberships (user_id)           WHERE left_at_utc IS NULL;
CREATE UNIQUE INDEX ux_house_single_manager ON house_memberships (house_id)          WHERE role = 1 AND left_at_utc IS NULL;


CREATE TABLE housing_posts (
    id                 bigint          GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id           bigint          NOT NULL REFERENCES houses (id) ON DELETE CASCADE,
    created_by_user_id bigint          NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    title              varchar(150)  NOT NULL,
    description        text          NOT NULL,
    listing_type       smallint      NOT NULL,          -- 1 SingleSeat, 2 MultipleSeats, 3 EntireHouse
    seats_available    int           NOT NULL,
    monthly_rent       numeric(18,2) NOT NULL,
    upazila_id         int           NOT NULL REFERENCES upazilas (id) ON DELETE RESTRICT,
    status             smallint      NOT NULL DEFAULT 1, -- 1 Active, 2 Closed
    req_gender         smallint,
    req_occupation     smallint,
    req_min_age        int,
    req_max_age        int,
    req_verified_only  boolean       NOT NULL DEFAULT false,
    req_student_only   boolean       NOT NULL DEFAULT false,
    req_marital_status smallint,
    created_at_utc     timestamptz   NOT NULL DEFAULT now(),
    updated_at_utc     timestamptz,

    CONSTRAINT ck_posts_listing_type   CHECK (listing_type BETWEEN 1 AND 3),
    CONSTRAINT ck_posts_status         CHECK (status BETWEEN 1 AND 2),
    CONSTRAINT ck_posts_seats          CHECK (seats_available >= 1),
    CONSTRAINT ck_posts_rent           CHECK (monthly_rent >= 0),
    CONSTRAINT ck_posts_age_range      CHECK (req_min_age IS NULL OR req_max_age IS NULL OR req_min_age <= req_max_age),
    CONSTRAINT ck_posts_req_gender     CHECK (req_gender IS NULL OR req_gender BETWEEN 1 AND 3),
    CONSTRAINT ck_posts_req_occupation CHECK (req_occupation IS NULL OR req_occupation BETWEEN 1 AND 4),
    CONSTRAINT ck_posts_req_marital    CHECK (req_marital_status IS NULL OR req_marital_status BETWEEN 1 AND 3)
);

CREATE INDEX ix_posts_area_active ON housing_posts (upazila_id, status) WHERE status = 1;
CREATE INDEX ix_posts_owner       ON housing_posts (created_by_user_id);
CREATE INDEX ix_posts_house       ON housing_posts (house_id);


CREATE TABLE booking_requests (
    id                 bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    housing_post_id    bigint         NOT NULL REFERENCES housing_posts (id) ON DELETE CASCADE,
    requester_user_id  bigint         NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    message            varchar(500),
    status             smallint     NOT NULL DEFAULT 1, -- 1 Pending, 2 Accepted, 3 Rejected, 4 Withdrawn
    created_at_utc     timestamptz  NOT NULL DEFAULT now(),
    decided_at_utc     timestamptz,
    decided_by_user_id bigint         REFERENCES users (id) ON DELETE SET NULL,

    CONSTRAINT ck_booking_status CHECK (status BETWEEN 1 AND 4)
);

CREATE UNIQUE INDEX ux_booking_open        ON booking_requests (housing_post_id, requester_user_id) WHERE status IN (1, 2);
CREATE INDEX        ix_booking_post_status ON booking_requests (housing_post_id, status);
CREATE INDEX        ix_booking_requester   ON booking_requests (requester_user_id);


-- ========================= M2: Domestic help =========================

CREATE TABLE domestic_helper_profiles (
    id               bigint          GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id          bigint          NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    display_name     varchar(120)  NOT NULL,
    upazila_id       int           NOT NULL REFERENCES upazilas (id) ON DELETE RESTRICT,
    latitude         numeric(9,6)  NOT NULL,
    longitude        numeric(9,6)  NOT NULL,
    service_radius_km numeric(5,2)  NOT NULL DEFAULT 3.00,
    monthly_rate     numeric(18,2) NOT NULL,
    available_from   time          NOT NULL,
    available_to     time          NOT NULL,
    years_experience int           NOT NULL,
    bio              varchar(1000),
    is_verified      boolean       NOT NULL DEFAULT false,
    is_active        boolean       NOT NULL DEFAULT true,
    average_rating   numeric(3,2),
    review_count     int           NOT NULL DEFAULT 0,
    created_at_utc   timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_helper_rate       CHECK (monthly_rate >= 0),
    CONSTRAINT ck_helper_experience CHECK (years_experience >= 0),
    CONSTRAINT ck_helper_reviews    CHECK (review_count >= 0)
);

CREATE UNIQUE INDEX ux_helper_user        ON domestic_helper_profiles (user_id);
CREATE INDEX        ix_helper_area_active ON domestic_helper_profiles (upazila_id, is_active) WHERE is_active;
CREATE INDEX        ix_helper_rating      ON domestic_helper_profiles (average_rating DESC);


CREATE TABLE helper_services (
    id                bigint          GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    helper_profile_id bigint          NOT NULL REFERENCES domestic_helper_profiles (id) ON DELETE CASCADE,
    service_type      smallint      NOT NULL,          -- 1 Cooking, 2 Cleaning, 3 Laundry, 4 Dishwashing, 5 Childcare
    rate_per_month    numeric(18,2) NOT NULL,

    CONSTRAINT ck_helper_service_type CHECK (service_type BETWEEN 1 AND 5),
    CONSTRAINT ck_helper_service_rate CHECK (rate_per_month >= 0)
);

CREATE UNIQUE INDEX ux_helper_service ON helper_services (helper_profile_id, service_type);


CREATE TABLE service_engagements (
    id                      bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    helper_profile_id       bigint        NOT NULL REFERENCES domestic_helper_profiles (id) ON DELETE RESTRICT,
    client_user_id          bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    house_id                bigint        REFERENCES houses (id) ON DELETE SET NULL,
    status                  smallint    NOT NULL DEFAULT 1, -- 1 Requested, 2 HelperConfirmed, 3 Active, 4 Completed, 5 Cancelled
    start_date              date        NOT NULL,
    end_date                date,
    requested_at_utc        timestamptz NOT NULL DEFAULT now(),
    helper_confirmed_at_utc timestamptz,
    client_completed_at_utc timestamptz,
    helper_completed_at_utc timestamptz,
    completed_at_utc        timestamptz,
    cancelled_at_utc        timestamptz,

    CONSTRAINT ck_engagement_status CHECK (status BETWEEN 1 AND 5)
);

CREATE INDEX        ix_engagement_helper ON service_engagements (helper_profile_id, status);
CREATE INDEX        ix_engagement_client ON service_engagements (client_user_id, status);
CREATE UNIQUE INDEX ux_engagement_open   ON service_engagements (helper_profile_id, client_user_id) WHERE status IN (1, 2, 3);


CREATE TABLE helper_reviews (
    id                    bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    service_engagement_id bigint        NOT NULL REFERENCES service_engagements (id) ON DELETE RESTRICT,
    helper_profile_id     bigint        NOT NULL REFERENCES domestic_helper_profiles (id) ON DELETE CASCADE,
    reviewer_user_id      bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    rating                smallint    NOT NULL,
    comment               varchar(1000),
    created_at_utc        timestamptz NOT NULL DEFAULT now(),
    is_hidden             boolean     NOT NULL DEFAULT false,

    CONSTRAINT ck_review_rating CHECK (rating BETWEEN 1 AND 5)
);

CREATE UNIQUE INDEX ux_review_engagement ON helper_reviews (service_engagement_id);
CREATE INDEX        ix_review_helper     ON helper_reviews (helper_profile_id) WHERE NOT is_hidden;


-- ========================= M3: Expenses and settlement =========================
-- expenses, contributions and meal_entries are append-only: fix a mistake by
-- inserting a correcting row, never by UPDATE or DELETE.

CREATE TABLE expenses (
    id                  bigint          GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id            bigint          NOT NULL REFERENCES houses (id) ON DELETE CASCADE,
    category            smallint      NOT NULL,          -- 1 EqualSplit, 2 MealPurchase
    description         varchar(200)  NOT NULL,
    amount              numeric(18,2) NOT NULL,          -- can be negative on a correcting row
    spent_by_user_id    bigint          NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    spent_on            date          NOT NULL,
    period_year         int           NOT NULL,
    period_month        int           NOT NULL,
    corrects_expense_id bigint          REFERENCES expenses (id) ON DELETE RESTRICT,
    created_by_user_id  bigint          NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    created_at_utc      timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_expense_category CHECK (category BETWEEN 1 AND 2),
    CONSTRAINT ck_expense_year     CHECK (period_year BETWEEN 2020 AND 2100),
    CONSTRAINT ck_expense_month    CHECK (period_month BETWEEN 1 AND 12)
);

CREATE INDEX ix_expense_house_period ON expenses (house_id, period_year, period_month, category);
CREATE INDEX ix_expense_corrects     ON expenses (corrects_expense_id) WHERE corrects_expense_id IS NOT NULL;


CREATE TABLE expense_shares (
    id           bigint          GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    expense_id   bigint          NOT NULL REFERENCES expenses (id) ON DELETE CASCADE,
    user_id      bigint          NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    share_amount numeric(18,2) NOT NULL
);

CREATE UNIQUE INDEX ux_expense_share ON expense_shares (expense_id, user_id);


-- Current value of a cell = the row with the latest recorded_at_utc.
CREATE TABLE meal_entries (
    id                       bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id                 bigint         NOT NULL REFERENCES houses (id) ON DELETE CASCADE,
    user_id                  bigint         NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    meal_date                date         NOT NULL,
    meal_count               numeric(4,1) NOT NULL,      -- one decimal allows half meals
    period_year              int          NOT NULL,
    period_month             int          NOT NULL,
    supersedes_meal_entry_id bigint         REFERENCES meal_entries (id) ON DELETE RESTRICT,
    recorded_by_user_id      bigint         NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    recorded_at_utc          timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_meal_count CHECK (meal_count >= 0 AND meal_count <= 10),
    CONSTRAINT ck_meal_month CHECK (period_month BETWEEN 1 AND 12)
);

CREATE INDEX ix_meal_current      ON meal_entries (house_id, user_id, meal_date, recorded_at_utc DESC);
CREATE INDEX ix_meal_house_period ON meal_entries (house_id, period_year, period_month);


CREATE TABLE meal_entry_audits (
    id              bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    meal_entry_id   bigint         NOT NULL REFERENCES meal_entries (id) ON DELETE RESTRICT,
    house_id        bigint         NOT NULL,
    target_user_id  bigint         NOT NULL,
    actor_user_id   bigint         NOT NULL,
    old_meal_count  numeric(4,1),
    new_meal_count  numeric(4,1) NOT NULL,
    reason          varchar(200),
    occurred_at_utc timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX ix_meal_audit_house ON meal_entry_audits (house_id, occurred_at_utc DESC);


CREATE TABLE contributions (
    id                       bigint          GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id                 bigint          NOT NULL REFERENCES houses (id) ON DELETE CASCADE,
    user_id                  bigint          NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    amount                   numeric(18,2) NOT NULL,
    paid_on                  date          NOT NULL,
    period_year              int           NOT NULL,
    period_month             int           NOT NULL,
    source                   smallint      NOT NULL,     -- 1 DerivedFromExpense, 2 DirectCashIn
    source_expense_id        bigint          REFERENCES expenses (id) ON DELETE RESTRICT,
    corrects_contribution_id bigint          REFERENCES contributions (id) ON DELETE RESTRICT,
    recorded_by_user_id      bigint          NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    created_at_utc           timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_contribution_source CHECK (source BETWEEN 1 AND 2),
    CONSTRAINT ck_contribution_month  CHECK (period_month BETWEEN 1 AND 12)
);

CREATE INDEX        ix_contribution_house_period ON contributions (house_id, period_year, period_month);
CREATE UNIQUE INDEX ux_contribution_expense      ON contributions (source_expense_id) WHERE source_expense_id IS NOT NULL;


CREATE TABLE settlement_runs (
    id                         bigint          GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id                   bigint          NOT NULL REFERENCES houses (id) ON DELETE CASCADE,
    period_year                int           NOT NULL,
    period_month               int           NOT NULL,
    total_meal_spending        numeric(18,2) NOT NULL,
    total_meals                numeric(10,1) NOT NULL,
    per_meal_rate              numeric(18,6) NOT NULL,
    total_equal_costs          numeric(18,2) NOT NULL,
    member_count_at_settlement int           NOT NULL,
    status                     smallint      NOT NULL DEFAULT 1,  -- 1 Draft, 2 Finalized
    computed_by_user_id        bigint          NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    computed_at_utc            timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_settlement_status CHECK (status BETWEEN 1 AND 2),
    CONSTRAINT ck_settlement_month  CHECK (period_month BETWEEN 1 AND 12)
);

CREATE UNIQUE INDEX ux_settlement_finalized ON settlement_runs (house_id, period_year, period_month) WHERE status = 2;


CREATE TABLE settlement_lines (
    id                  bigint          GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    settlement_run_id   bigint          NOT NULL REFERENCES settlement_runs (id) ON DELETE CASCADE,
    user_id             bigint          NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    meal_count          numeric(10,1) NOT NULL,
    meal_cost           numeric(18,2) NOT NULL,
    equal_share         numeric(18,2) NOT NULL,
    contributions       numeric(18,2) NOT NULL,
    rounding_adjustment numeric(18,2) NOT NULL DEFAULT 0.00,
    net_amount          numeric(18,2) NOT NULL   -- positive means the house owes the member
);

CREATE UNIQUE INDEX ux_settlement_line ON settlement_lines (settlement_run_id, user_id);


CREATE TABLE settlement_transfers (
    id                bigint          GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    settlement_run_id bigint          NOT NULL REFERENCES settlement_runs (id) ON DELETE CASCADE,
    from_user_id      bigint          NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    to_user_id        bigint          NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    amount            numeric(18,2) NOT NULL,

    CONSTRAINT ck_transfer_amount   CHECK (amount > 0),
    CONSTRAINT ck_transfer_distinct CHECK (from_user_id <> to_user_id)
);

CREATE INDEX ix_settlement_transfer_run ON settlement_transfers (settlement_run_id);


-- ========================= M4: Marketplace =========================

CREATE TABLE marketplace_categories (
    id                 int           PRIMARY KEY,
    name               varchar(60)   NOT NULL,
    slug               varchar(60)   NOT NULL,
    default_price_low  numeric(18,2) NOT NULL,
    default_price_high numeric(18,2) NOT NULL
);

CREATE UNIQUE INDEX ux_marketplace_category_slug ON marketplace_categories (slug);


CREATE TABLE marketplace_items (
    id             bigint          GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    seller_user_id bigint          NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    category_id    int           NOT NULL REFERENCES marketplace_categories (id) ON DELETE RESTRICT,
    title          varchar(150)  NOT NULL,
    description    text          NOT NULL,
    condition      smallint      NOT NULL,           -- 1 New, 2 LikeNew, 3 Good, 4 Fair, 5 Poor
    asking_price   numeric(18,2) NOT NULL,
    age_months     int,
    upazila_id     int           NOT NULL REFERENCES upazilas (id) ON DELETE RESTRICT,
    status         smallint      NOT NULL DEFAULT 1, -- 1 Active, 2 Sold, 3 Removed
    created_at_utc timestamptz   NOT NULL DEFAULT now(),
    updated_at_utc timestamptz,

    CONSTRAINT ck_item_condition CHECK (condition BETWEEN 1 AND 5),
    CONSTRAINT ck_item_status    CHECK (status BETWEEN 1 AND 3),
    CONSTRAINT ck_item_price     CHECK (asking_price >= 0),
    CONSTRAINT ck_item_age       CHECK (age_months IS NULL OR age_months >= 0)
);

CREATE INDEX ix_items_area_active ON marketplace_items (upazila_id, status) WHERE status = 1;
CREATE INDEX ix_items_seller      ON marketplace_items (seller_user_id);
CREATE INDEX ix_items_training    ON marketplace_items (category_id, condition, created_at_utc DESC);


CREATE TABLE marketplace_item_images (
    id               bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    item_id          bigint        NOT NULL REFERENCES marketplace_items (id) ON DELETE CASCADE,
    stored_file_name varchar(64) NOT NULL,
    content_type     varchar(60) NOT NULL,
    size_bytes       int         NOT NULL,
    sort_order       int         NOT NULL DEFAULT 0
);

CREATE INDEX ix_item_images_item ON marketplace_item_images (item_id, sort_order);


CREATE TABLE buy_interests (
    id             bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    item_id        bigint        NOT NULL REFERENCES marketplace_items (id) ON DELETE CASCADE,
    buyer_user_id  bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    message        varchar(500),
    status         smallint    NOT NULL DEFAULT 1,  -- 1 Pending, 2 Accepted, 3 Declined, 4 Withdrawn
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    decided_at_utc timestamptz,

    CONSTRAINT ck_buy_interest_status CHECK (status BETWEEN 1 AND 4)
);

CREATE UNIQUE INDEX ux_buy_open        ON buy_interests (item_id, buyer_user_id) WHERE status IN (1, 2);
CREATE INDEX        ix_buy_item_status ON buy_interests (item_id, status);
CREATE INDEX        ix_buy_buyer       ON buy_interests (buyer_user_id);


-- ========================= M5 / M6: Verification, reports, admin, ML =========================

CREATE TABLE verification_requests (
    id                      bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id                 bigint        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    subject_type            smallint    NOT NULL,          -- 1 User, 2 DomesticHelper
    status                  smallint    NOT NULL DEFAULT 1, -- 1 Pending, 2 Approved, 3 Rejected
    submitted_at_utc        timestamptz NOT NULL DEFAULT now(),
    decided_at_utc          timestamptz,
    decided_by_admin_id     bigint        REFERENCES users (id) ON DELETE SET NULL,
    rejection_reason        varchar(500),
    documents_purged_at_utc timestamptz,

    CONSTRAINT ck_verification_subject CHECK (subject_type BETWEEN 1 AND 2),
    CONSTRAINT ck_verification_status  CHECK (status BETWEEN 1 AND 3)
);

CREATE UNIQUE INDEX ux_verification_one_open ON verification_requests (user_id) WHERE status = 1;
CREATE INDEX        ix_verification_queue    ON verification_requests (status, submitted_at_utc);


CREATE TABLE verification_documents (
    id                           bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    verification_request_id      bigint         NOT NULL REFERENCES verification_requests (id) ON DELETE CASCADE,
    document_type                smallint     NOT NULL,    -- 1 NationalId, 2 StudentId, 3 Passport, 4 BirthCertificate
    stored_file_name             varchar(64)  NOT NULL,
    original_file_name_sanitized varchar(120) NOT NULL,
    content_type                 varchar(60)  NOT NULL,
    size_bytes                   int          NOT NULL,
    sha256_hash                  bytea        NOT NULL,
    scan_status                  smallint     NOT NULL DEFAULT 1,  -- 1 Pending, 2 Clean, 3 Infected, 4 ScanFailed
    scanned_at_utc               timestamptz,
    uploaded_at_utc              timestamptz  NOT NULL DEFAULT now(),
    deleted_at_utc               timestamptz,

    CONSTRAINT ck_vdoc_type        CHECK (document_type BETWEEN 1 AND 4),
    CONSTRAINT ck_vdoc_scan_status CHECK (scan_status BETWEEN 1 AND 4),
    CONSTRAINT ck_vdoc_hash_len    CHECK (octet_length(sha256_hash) = 32)
);

CREATE INDEX ix_vdoc_request ON verification_documents (verification_request_id);


CREATE TABLE reports (
    id                   bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    reporter_user_id     bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    target_type          smallint    NOT NULL,   -- 1 HousingPost, 2 MarketplaceItem, 3 HelperProfile, 4 HelperReview, 5 User
    target_id            bigint        NOT NULL,
    reason               smallint    NOT NULL,   -- 1 Spam, 2 Fraud, 3 Offensive, 4 Misleading, 5 Other
    details              varchar(1000),
    status               smallint    NOT NULL DEFAULT 1, -- 1 Open, 2 UnderReview, 3 ActionTaken, 4 Dismissed
    created_at_utc       timestamptz NOT NULL DEFAULT now(),
    resolved_at_utc      timestamptz,
    resolved_by_admin_id bigint        REFERENCES users (id) ON DELETE SET NULL,
    resolution_note      varchar(1000),

    CONSTRAINT ck_report_target_type CHECK (target_type BETWEEN 1 AND 5),
    CONSTRAINT ck_report_reason      CHECK (reason BETWEEN 1 AND 5),
    CONSTRAINT ck_report_status      CHECK (status BETWEEN 1 AND 4)
);

CREATE UNIQUE INDEX ux_report_once   ON reports (reporter_user_id, target_type, target_id);
CREATE INDEX        ix_report_queue  ON reports (status, created_at_utc);
CREATE INDEX        ix_report_target ON reports (target_type, target_id);


CREATE TABLE admin_audit_logs (
    id              bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    admin_user_id   bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    action          varchar(60) NOT NULL,
    target_type     varchar(40) NOT NULL,
    target_id       bigint,
    before_json     jsonb,
    after_json      jsonb,
    ip_address      inet,
    occurred_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_audit_admin_time ON admin_audit_logs (admin_user_id, occurred_at_utc DESC);
CREATE INDEX ix_audit_target     ON admin_audit_logs (target_type, target_id);


CREATE TABLE notifications (
    id                bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    recipient_user_id bigint         NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    type              smallint     NOT NULL,   -- 1 BookingRequested .. 9 ReportResolved
    title             varchar(150) NOT NULL,
    body              varchar(500) NOT NULL,
    link_path         varchar(200),
    source_type       smallint     NOT NULL,
    source_id         bigint         NOT NULL,
    is_read           boolean      NOT NULL DEFAULT false,
    created_at_utc    timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_notification_type CHECK (type BETWEEN 1 AND 9)
);

CREATE INDEX        ix_notif_inbox  ON notifications (recipient_user_id, is_read, created_at_utc DESC);
CREATE UNIQUE INDEX ux_notif_dedupe ON notifications (recipient_user_id, source_type, source_id, type);


CREATE TABLE ml_model_versions (
    id                  bigint          GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    model_name          varchar(60)   NOT NULL,
    version             int           NOT NULL,
    trained_at_utc      timestamptz   NOT NULL DEFAULT now(),
    training_row_count  int           NOT NULL,
    mean_absolute_error numeric(18,4) NOT NULL,
    r_squared           numeric(6,4)  NOT NULL,
    stored_file_name    varchar(64)   NOT NULL,
    is_active           boolean       NOT NULL DEFAULT false
);

CREATE UNIQUE INDEX ux_model_active ON ml_model_versions (model_name) WHERE is_active;
