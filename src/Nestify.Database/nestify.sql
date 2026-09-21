-- ============================================================================
-- Nestify: the whole database in one file.
--
-- Run this once on an empty PostgreSQL database and every table the project
-- needs is created, in dependency order, with its lookup rows. It is the same
-- schema as the per-module files next to it (Autthintication.sql, User_Home.sql,
-- Housing.sql, ...) merged together; those files stay as the documentation of
-- each module and as migrations for databases that already exist.
--
-- Running it again drops and recreates everything, so do not run it on a
-- database whose data you want to keep.
--
-- After this, load the reference data:
--   seed/bangladesh_administrative_seed.sql   divisions, districts, upazilas
--   seed/domestic_help_seed.sql               sample helpers (optional)
--
-- Order of the sections below:
--   1. Accounts             roles, users, user_roles, refresh_tokens
--   2. Areas                divisions, districts, upazilas
--   3. Profiles             user_additional_profile_info
--   4. Homes                homes, home_members, home_join_requests, home_capacity
--   5. Housing              listing types, posts, requirements, images, bookings
--   6. Marketplace          categories, conditions, listings, images, interests, views, reports
--   7. Settlement           expenses, meals, contributions, settlement runs
--   8. Verification         requests, documents, bKash payments
--   9. Admin                report reasons, reports, takedowns, fees, plans, admin accounts, audit log
--  10. Domestic help        helper profiles, addresses, services, availability, engagements, reviews
-- ============================================================================


-- ============================================================================
-- Drop everything, children first
-- ============================================================================

DROP TABLE IF EXISTS helper_reviews               CASCADE;
DROP TABLE IF EXISTS helper_home_placements       CASCADE;
DROP TABLE IF EXISTS service_engagement_slots     CASCADE;
DROP TABLE IF EXISTS service_engagement_services  CASCADE;
DROP TABLE IF EXISTS service_engagement_details   CASCADE;
DROP TABLE IF EXISTS service_engagements          CASCADE;
DROP TABLE IF EXISTS helper_weekly_availability   CASCADE;
DROP TABLE IF EXISTS helper_services              CASCADE;
DROP TABLE IF EXISTS helper_addresses             CASCADE;
DROP TABLE IF EXISTS domestic_helper_profiles     CASCADE;

DROP TABLE IF EXISTS admin_audit_log              CASCADE;
DROP TABLE IF EXISTS admin_accounts               CASCADE;
DROP TABLE IF EXISTS plan_purchases               CASCADE;
DROP TABLE IF EXISTS post_plans                   CASCADE;
DROP TABLE IF EXISTS fee_settings                 CASCADE;
DROP TABLE IF EXISTS post_takedowns               CASCADE;
DROP TABLE IF EXISTS housing_reports              CASCADE;
DROP TABLE IF EXISTS housing_report_reasons       CASCADE;

DROP TABLE IF EXISTS verification_payments        CASCADE;
DROP TABLE IF EXISTS verification_documents       CASCADE;
DROP TABLE IF EXISTS verification_requests        CASCADE;

DROP TABLE IF EXISTS settlement_transfers         CASCADE;
DROP TABLE IF EXISTS settlement_lines             CASCADE;
DROP TABLE IF EXISTS settlement_members           CASCADE;
DROP TABLE IF EXISTS settlement_runs              CASCADE;
DROP TABLE IF EXISTS contributions                CASCADE;
DROP TABLE IF EXISTS meal_entry_audits            CASCADE;
DROP TABLE IF EXISTS meal_entries                 CASCADE;
DROP TABLE IF EXISTS expense_shares               CASCADE;
DROP TABLE IF EXISTS expenses                     CASCADE;

DROP TABLE IF EXISTS marketplace_reports          CASCADE;
DROP TABLE IF EXISTS marketplace_report_reasons   CASCADE;
DROP TABLE IF EXISTS marketplace_listing_views    CASCADE;
DROP TABLE IF EXISTS marketplace_buy_interests    CASCADE;
DROP TABLE IF EXISTS marketplace_listing_images   CASCADE;
DROP TABLE IF EXISTS marketplace_listings         CASCADE;
DROP TABLE IF EXISTS marketplace_conditions       CASCADE;
DROP TABLE IF EXISTS marketplace_categories       CASCADE;

DROP TABLE IF EXISTS housing_bookings             CASCADE;
DROP TABLE IF EXISTS housing_post_images          CASCADE;
DROP TABLE IF EXISTS housing_post_requirements    CASCADE;
DROP TABLE IF EXISTS housing_posts                CASCADE;
DROP TABLE IF EXISTS housing_listing_types        CASCADE;

DROP TABLE IF EXISTS home_capacity                CASCADE;
DROP TABLE IF EXISTS home_join_requests           CASCADE;
DROP TABLE IF EXISTS home_members                 CASCADE;
DROP TABLE IF EXISTS homes                        CASCADE;

DROP TABLE IF EXISTS user_additional_profile_info CASCADE;

DROP TABLE IF EXISTS upazilas                     CASCADE;
DROP TABLE IF EXISTS districts                    CASCADE;
DROP TABLE IF EXISTS divisions                    CASCADE;

DROP TABLE IF EXISTS refresh_tokens               CASCADE;
DROP TABLE IF EXISTS user_roles                   CASCADE;
DROP TABLE IF EXISTS users                        CASCADE;
DROP TABLE IF EXISTS roles                        CASCADE;


-- ============================================================================
-- 1. Accounts  (Autthintication.sql)
-- ============================================================================

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


-- ========================= Users =========================

CREATE TABLE users (
    id             bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    full_name      varchar(120) NOT NULL,
    email          varchar(256) NOT NULL,          -- always stored lower-case by the API
    password_hash  text         NOT NULL,
    phone_number   varchar(20)  NOT NULL,
    account_type   smallint     NOT NULL DEFAULT 1,   -- 1 = User, 2 = DomesticHelp, 3 = Admin
    created_at_utc timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_users_account_type CHECK (account_type BETWEEN 1 AND 3)
);

CREATE UNIQUE INDEX ux_users_email ON users (email);


CREATE TABLE user_roles (
    user_id        bigint      NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    role_id        smallint    NOT NULL REFERENCES roles (id) ON DELETE RESTRICT,
    granted_at_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, role_id)
);

CREATE INDEX ix_user_roles_role ON user_roles (role_id);


-- ========================= Refresh tokens (login sessions) =========================

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


-- ============================================================================
-- 2. Areas  (Bangladesh_Administrative_Structure.sql)
--
--   division -> district -> upazila / thana
--
-- Upazilas and metropolitan thanas share one table; is_metropolitan_thana
-- tells them apart where a label needs to say "Thana" instead of "Upazila".
-- Rows come from seed/bangladesh_administrative_seed.sql.
-- ============================================================================

CREATE TABLE divisions (
    id      int         PRIMARY KEY,
    name    varchar(60) NOT NULL,
    bn_name varchar(60) NOT NULL
);

CREATE UNIQUE INDEX ux_divisions_name ON divisions (name);


CREATE TABLE districts (
    id          int         PRIMARY KEY,
    division_id int         NOT NULL REFERENCES divisions (id) ON DELETE RESTRICT,
    name        varchar(60) NOT NULL,
    bn_name     varchar(60) NOT NULL,
    latitude    numeric(9,6),
    longitude   numeric(9,6)
);

CREATE INDEX        ix_districts_division      ON districts (division_id);
CREATE UNIQUE INDEX ux_districts_division_name ON districts (division_id, name);


-- Rural upazilas keep the ids of the source dataset. Metropolitan thanas are
-- numbered from 90001 up so the two ranges never collide.
CREATE TABLE upazilas (
    id                    int          PRIMARY KEY,
    district_id           int          NOT NULL REFERENCES districts (id) ON DELETE RESTRICT,
    name                  varchar(100) NOT NULL,
    bn_name               varchar(100),
    is_metropolitan_thana boolean      NOT NULL DEFAULT false
);

CREATE INDEX        ix_upazilas_district      ON upazilas (district_id);
CREATE INDEX        ix_upazilas_metro         ON upazilas (district_id) WHERE is_metropolitan_thana;
CREATE UNIQUE INDEX ux_upazilas_district_name ON upazilas (district_id, name);


-- ============================================================================
-- 3. Profiles  (User_Additional_profile_info.sql)
--
-- Extra profile fields that the registration form on /auth does not ask for.
-- One row per user; created at registration, or by the API the first time an
-- older account opens the profile page.
-- ============================================================================

CREATE TABLE user_additional_profile_info (
    user_id             bigint       PRIMARY KEY REFERENCES users (id) ON DELETE CASCADE,
    profile_picture_url text         NOT NULL DEFAULT 'https://res.cloudinary.com/dait0sacc/image/upload/v1774704629/k7ygnoel72ychr8ico6n.png',
    occupation          varchar(120),
    organization_name   varchar(160),                    -- workplace or school / university name
    gender              smallint,                        -- 0 Male, 1 Female
    date_of_birth       date,
    is_smoker           boolean      NOT NULL DEFAULT false,
    is_drinker          boolean      NOT NULL DEFAULT false,
    is_verified         boolean      NOT NULL DEFAULT false,
    address             varchar(250),
    whatsapp_number     varchar(20),
    facebook_url        varchar(250),
    x_url               varchar(250),
    instagram_url       varchar(250),
    created_at_utc      timestamptz  NOT NULL DEFAULT now(),
    updated_at_utc      timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_profile_gender CHECK (gender IS NULL OR gender BETWEEN 0 AND 1)
);


-- ============================================================================
-- 4. Homes  (User_Home.sql)
--
-- A user creates a home and becomes its manager. Everyone else either joins
-- with the home's join code or is added by the manager / a co-manager. A user
-- is in at most one home at a time.
-- ============================================================================

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
-- Typing a join code files a request here, which the manager or a co-manager
-- approves or rejects. A user has at most one pending request at a time.

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


-- ========================= Capacity =========================
-- How many people a home can hold at most. "How many live here now" is counted
-- from home_members where left_at_utc IS NULL; free seats = max_occupants -
-- that count. When the two meet, the home's housing posts are marked filled.

CREATE TABLE home_capacity (
    home_id        bigint      PRIMARY KEY REFERENCES homes (id) ON DELETE CASCADE,
    max_occupants  smallint    NOT NULL DEFAULT 4,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_home_capacity_min CHECK (max_occupants >= 1)
);


-- ============================================================================
-- 5. Housing  (Housing.sql)
--
-- A manager or co-manager posts that their home has room for more bachelors,
-- seekers browse those posts and ask to book a seat. A post always belongs to
-- a home; "seats available" is worked out from home_capacity and home_members
-- whenever the post is read.
-- ============================================================================

-- Ids match the ListingType enum order in Nestify.Shared.
CREATE TABLE housing_listing_types (
    id         smallint    PRIMARY KEY,
    code       varchar(30) NOT NULL,   -- enum name, e.g. 'SingleSeat'
    name       varchar(40) NOT NULL,   -- label shown in the UI
    sort_order smallint    NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX ux_housing_listing_types_code ON housing_listing_types (code);

INSERT INTO housing_listing_types (id, code, name, sort_order) VALUES
    (0, 'SingleSeat',    'Single seat',    1),
    (1, 'MultipleSeats', 'Multiple seats', 2),
    (2, 'EntireHouse',   'Entire house',   3);


-- One row per listing. posted_by_user_id is who wrote it, but any manager or
-- co-manager of the home can edit, close, reopen or delete it.
-- status: 1 Active, 2 Closed, 3 Filled.
CREATE TABLE housing_posts (
    id                bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    home_id           bigint        NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    posted_by_user_id bigint        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    title             varchar(150)  NOT NULL,
    description       varchar(4000) NOT NULL,
    listing_type_id   smallint      NOT NULL REFERENCES housing_listing_types (id) ON DELETE RESTRICT,
    monthly_rent_bdt  numeric(10,2) NOT NULL,
    status            smallint      NOT NULL DEFAULT 1,   -- 1 Active, 2 Closed, 3 Filled
    created_at_utc    timestamptz   NOT NULL DEFAULT now(),
    updated_at_utc    timestamptz   NOT NULL DEFAULT now(),
    closed_at_utc     timestamptz,

    CONSTRAINT ck_housing_post_title_len CHECK (char_length(title) BETWEEN 4 AND 150),
    CONSTRAINT ck_housing_post_desc_len  CHECK (char_length(description) >= 20),
    CONSTRAINT ck_housing_post_rent      CHECK (monthly_rent_bdt >= 0 AND monthly_rent_bdt <= 1000000),
    CONSTRAINT ck_housing_post_status    CHECK (status BETWEEN 1 AND 3)
);

CREATE INDEX ix_housing_posts_home   ON housing_posts (home_id);
CREATE INDEX ix_housing_posts_browse ON housing_posts (created_at_utc DESC) WHERE status = 1;
CREATE INDEX ix_housing_posts_rent   ON housing_posts (monthly_rent_bdt)   WHERE status = 1;
CREATE INDEX ix_housing_posts_type   ON housing_posts (listing_type_id)    WHERE status = 1;


-- Who can apply. NULL / false means no requirement on that trait.
-- gender: 0 Male, 1 Female. occupation: 0 Student, 1 Working.
CREATE TABLE housing_post_requirements (
    post_id          bigint   PRIMARY KEY REFERENCES housing_posts (id) ON DELETE CASCADE,
    gender           smallint,
    occupation       smallint,
    min_age          smallint,
    max_age          smallint,
    verified_only    boolean  NOT NULL DEFAULT false,
    non_smoker_only  boolean  NOT NULL DEFAULT false,
    non_drinker_only boolean  NOT NULL DEFAULT false,

    CONSTRAINT ck_housing_req_gender     CHECK (gender IS NULL OR gender BETWEEN 0 AND 1),
    CONSTRAINT ck_housing_req_occupation CHECK (occupation IS NULL OR occupation BETWEEN 0 AND 1),
    CONSTRAINT ck_housing_req_min_age    CHECK (min_age IS NULL OR min_age BETWEEN 0 AND 120),
    CONSTRAINT ck_housing_req_max_age    CHECK (max_age IS NULL OR max_age BETWEEN 0 AND 120),
    CONSTRAINT ck_housing_req_age_order  CHECK (min_age IS NULL OR max_age IS NULL OR min_age <= max_age)
);


-- Photos of the place, one row per photo. sort_order 0 is the cover image.
CREATE TABLE housing_post_images (
    id              bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    post_id         bigint      NOT NULL REFERENCES housing_posts (id) ON DELETE CASCADE,
    image_url       text        NOT NULL,
    sort_order      smallint    NOT NULL DEFAULT 0,
    uploaded_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_housing_post_images_order ON housing_post_images (post_id, sort_order);


-- A seeker who is in no home asks to book a seat with a short message; a
-- manager or co-manager accepts or rejects. Once accepted both sides see each
-- other's contact. Joining the home sets the row to Joined; when the home
-- fills up every other open booking on its posts becomes HouseFull.
-- status: 1 Pending, 2 Accepted, 3 Rejected, 4 Withdrawn, 5 Joined, 6 HouseFull.
CREATE TABLE housing_bookings (
    id                 bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    post_id            bigint       NOT NULL REFERENCES housing_posts (id) ON DELETE CASCADE,
    requester_user_id  bigint       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    message            varchar(500),                     -- seeker's note to the manager
    reply_message      varchar(500),                     -- manager's note back, mostly on reject
    status             smallint     NOT NULL DEFAULT 1,
    requested_at_utc   timestamptz  NOT NULL DEFAULT now(),
    decided_at_utc     timestamptz,
    decided_by_user_id bigint       REFERENCES users (id) ON DELETE SET NULL,

    CONSTRAINT ck_housing_booking_status CHECK (status BETWEEN 1 AND 6)
);

CREATE INDEX        ix_housing_bookings_post      ON housing_bookings (post_id, status);
CREATE INDEX        ix_housing_bookings_requester ON housing_bookings (requester_user_id, requested_at_utc DESC);
CREATE UNIQUE INDEX ux_housing_booking_one_open   ON housing_bookings (post_id, requester_user_id) WHERE status IN (1, 2);


-- ============================================================================
-- 6. Marketplace  (Marketplace.sql)
--
-- Second-hand marketplace: listings, their photos, buy interests, view counts
-- and reports.
-- ============================================================================

-- Ids match the MarketplaceCategory enum order in Nestify.Shared.
CREATE TABLE marketplace_categories (
    id         smallint    PRIMARY KEY,
    code       varchar(40) NOT NULL,   -- enum name, e.g. 'Furniture'
    name       varchar(60) NOT NULL,   -- label shown in the UI
    sort_order smallint    NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX ux_marketplace_categories_code ON marketplace_categories (code);

INSERT INTO marketplace_categories (id, code, name, sort_order) VALUES
    (0, 'Furniture',   'Furniture',   1),
    (1, 'Electronics', 'Electronics', 2),
    (2, 'Appliances',  'Appliances',  3),
    (3, 'Kitchen',     'Kitchen',     4),
    (4, 'Books',       'Books',       5),
    (5, 'Bedding',     'Bedding',     6),
    (6, 'Other',       'Other',       7);


-- Ids match the ItemCondition enum order in Nestify.Shared.
CREATE TABLE marketplace_conditions (
    id         smallint    PRIMARY KEY,
    code       varchar(20) NOT NULL,   -- enum name, e.g. 'LikeNew'
    name       varchar(40) NOT NULL,   -- label shown in the UI
    sort_order smallint    NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX ux_marketplace_conditions_code ON marketplace_conditions (code);

INSERT INTO marketplace_conditions (id, code, name, sort_order) VALUES
    (0, 'New',     'New',      1),
    (1, 'LikeNew', 'Like new', 2),
    (2, 'Good',    'Good',     3),
    (3, 'Fair',    'Fair',     4);


-- One row per item a user puts up for sale. Location is stored as ids into the
-- administrative tables; area_name is the free-text handover spot.
-- status: 1 Active, 2 Sold, 3 Removed.
CREATE TABLE marketplace_listings (
    id                  bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    seller_user_id      bigint         NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    title               varchar(80)    NOT NULL,
    description         varchar(1200)  NOT NULL,
    category_id         smallint       NOT NULL REFERENCES marketplace_categories (id) ON DELETE RESTRICT,
    condition_id        smallint       NOT NULL REFERENCES marketplace_conditions (id) ON DELETE RESTRICT,
    price_bdt           numeric(10,2)  NOT NULL,
    division_id         int            NOT NULL REFERENCES divisions (id) ON DELETE RESTRICT,
    district_id         int            REFERENCES districts (id) ON DELETE RESTRICT,
    upazila_id          int            REFERENCES upazilas (id) ON DELETE RESTRICT,
    area_name           varchar(80)    NOT NULL,
    status              smallint       NOT NULL DEFAULT 1,   -- 1 Active, 2 Sold, 3 Removed
    posted_at_utc       timestamptz    NOT NULL DEFAULT now(),
    updated_at_utc      timestamptz    NOT NULL DEFAULT now(),
    sold_at_utc         timestamptz,
    sold_to_user_id     bigint         REFERENCES users (id) ON DELETE SET NULL,   -- buyer picked at "mark as sold"; NULL if sold outside Nestify
    removed_at_utc      timestamptz,
    removed_by_user_id  bigint         REFERENCES users (id) ON DELETE SET NULL,   -- seller or the admin who struck it down

    CONSTRAINT ck_marketplace_listing_title_len CHECK (char_length(title) BETWEEN 4 AND 80),
    CONSTRAINT ck_marketplace_listing_desc_len  CHECK (char_length(description) >= 20),
    CONSTRAINT ck_marketplace_listing_price     CHECK (price_bdt >= 1 AND price_bdt <= 1000000),
    CONSTRAINT ck_marketplace_listing_status    CHECK (status BETWEEN 1 AND 3)
);

CREATE INDEX ix_marketplace_listings_seller    ON marketplace_listings (seller_user_id);
CREATE INDEX ix_marketplace_listings_browse    ON marketplace_listings (posted_at_utc DESC) WHERE status = 1;
CREATE INDEX ix_marketplace_listings_price     ON marketplace_listings (price_bdt) WHERE status = 1;
CREATE INDEX ix_marketplace_listings_category  ON marketplace_listings (category_id) WHERE status = 1;
CREATE INDEX ix_marketplace_listings_condition ON marketplace_listings (condition_id) WHERE status = 1;
CREATE INDEX ix_marketplace_listings_division  ON marketplace_listings (division_id) WHERE status = 1;
CREATE INDEX ix_marketplace_listings_district  ON marketplace_listings (district_id) WHERE status = 1;
CREATE INDEX ix_marketplace_listings_upazila   ON marketplace_listings (upazila_id) WHERE status = 1;


-- Photos of a listing, one row per photo. sort_order 0 is the cover image.
CREATE TABLE marketplace_listing_images (
    id              bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    listing_id      bigint      NOT NULL REFERENCES marketplace_listings (id) ON DELETE CASCADE,
    image_url       text        NOT NULL,
    sort_order      smallint    NOT NULL DEFAULT 0,
    uploaded_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_marketplace_listing_images_order ON marketplace_listing_images (listing_id, sort_order);


-- A buyer says "I want this" with a short message; the seller accepts or
-- declines. Phone numbers are shown to both sides once Accepted. Marking the
-- listing sold makes the chosen buyer's request Fulfilled and closes the rest.
-- status: 1 Pending, 2 Accepted, 3 Declined, 4 Withdrawn, 5 Fulfilled, 6 Closed.
CREATE TABLE marketplace_buy_interests (
    id                      bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    listing_id              bigint       NOT NULL REFERENCES marketplace_listings (id) ON DELETE CASCADE,
    buyer_user_id           bigint       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    message                 varchar(500) NOT NULL,
    status                  smallint     NOT NULL DEFAULT 1,
    preferred_handover_area varchar(120),                     -- seller's note shown to the buyer after acceptance
    created_at_utc          timestamptz  NOT NULL DEFAULT now(),
    responded_at_utc        timestamptz,                      -- when the seller accepted / declined
    withdrawn_at_utc        timestamptz,

    CONSTRAINT ck_marketplace_interest_status CHECK (status BETWEEN 1 AND 6)
);

CREATE INDEX        ix_marketplace_interests_listing ON marketplace_buy_interests (listing_id, status);
CREATE INDEX        ix_marketplace_interests_buyer   ON marketplace_buy_interests (buyer_user_id, created_at_utc DESC);
CREATE UNIQUE INDEX ux_marketplace_interest_one_open ON marketplace_buy_interests (listing_id, buyer_user_id) WHERE status IN (1, 2);


-- One row each time someone opens a listing's detail page.
CREATE TABLE marketplace_listing_views (
    id             bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    listing_id     bigint      NOT NULL REFERENCES marketplace_listings (id) ON DELETE CASCADE,
    viewer_user_id bigint      REFERENCES users (id) ON DELETE SET NULL,
    viewed_at_utc  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_marketplace_views_listing ON marketplace_listing_views (listing_id);


-- Reasons a user can pick when reporting a listing. Adding one is an INSERT.
CREATE TABLE marketplace_report_reasons (
    id         smallint    PRIMARY KEY,
    name       varchar(60) NOT NULL,
    sort_order smallint    NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX ux_marketplace_report_reasons_name ON marketplace_report_reasons (name);

INSERT INTO marketplace_report_reasons (id, name, sort_order) VALUES
    (1, 'Prohibited item',        1),
    (2, 'Misleading description', 2),
    (3, 'Suspected scam',         3),
    (4, 'Wrong category',         4),
    (5, 'Other',                  5);


-- A user reports a listing; admins strike the listing down or dismiss it.
-- state: 1 Open, 2 Resolved (listing removed), 3 Dismissed.
CREATE TABLE marketplace_reports (
    id                   bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    listing_id           bigint       NOT NULL REFERENCES marketplace_listings (id) ON DELETE CASCADE,
    reported_by_user_id  bigint       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    reason_id            smallint     NOT NULL REFERENCES marketplace_report_reasons (id) ON DELETE RESTRICT,
    details              varchar(300),
    state                smallint     NOT NULL DEFAULT 1,
    raised_at_utc        timestamptz  NOT NULL DEFAULT now(),
    decided_at_utc       timestamptz,
    decided_by_admin_id  bigint       REFERENCES users (id) ON DELETE SET NULL,

    CONSTRAINT ck_marketplace_report_state CHECK (state BETWEEN 1 AND 3)
);

CREATE INDEX        ix_marketplace_reports_queue    ON marketplace_reports (state, raised_at_utc);
CREATE INDEX        ix_marketplace_reports_listing  ON marketplace_reports (listing_id);
CREATE UNIQUE INDEX ux_marketplace_report_one_open  ON marketplace_reports (listing_id, reported_by_user_id) WHERE state = 1;


-- ============================================================================
-- 7. Settlement  (Settlement.sql)
--
-- Expenses, meals, contributions and the monthly settlement book of a home.
-- expenses, contributions and meal_entries are append-only: a mistake is fixed
-- by inserting a correcting row, never by UPDATE or DELETE.
--
--   expenses.category:      1 = EqualSplit, 2 = MealPurchase
--   contributions.fund_type: 1 = MealFund,  2 = SharedBills
--   contributions.source:    1 = DerivedFromExpense, 2 = DirectCashIn
-- ============================================================================

CREATE TABLE expenses (
    id                  bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id            bigint        NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    category            smallint      NOT NULL,
    description         varchar(200)  NOT NULL,
    amount              numeric(18,2) NOT NULL,
    spent_by_user_id    bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    spent_on            date          NOT NULL,
    period_year         int           NOT NULL,
    period_month        int           NOT NULL,
    corrects_expense_id bigint        REFERENCES expenses (id) ON DELETE RESTRICT,
    created_by_user_id  bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    created_at_utc      timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_expense_category CHECK (category BETWEEN 1 AND 2),
    CONSTRAINT ck_expense_year     CHECK (period_year BETWEEN 2020 AND 2100),
    CONSTRAINT ck_expense_month    CHECK (period_month BETWEEN 1 AND 12)
);

CREATE INDEX ix_expense_house_period ON expenses (house_id, period_year, period_month, category);
CREATE INDEX ix_expense_corrects     ON expenses (corrects_expense_id) WHERE corrects_expense_id IS NOT NULL;


CREATE TABLE expense_shares (
    id           bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    expense_id   bigint        NOT NULL REFERENCES expenses (id) ON DELETE CASCADE,
    user_id      bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    share_amount numeric(18,2) NOT NULL
);

CREATE UNIQUE INDEX ux_expense_share ON expense_shares (expense_id, user_id);


CREATE TABLE meal_entries (
    id                       bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id                 bigint       NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    user_id                  bigint       NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    meal_date                date         NOT NULL,
    breakfast                numeric(4,1) NOT NULL DEFAULT 0,
    lunch                    numeric(4,1) NOT NULL DEFAULT 0,
    dinner                   numeric(4,1) NOT NULL DEFAULT 0,
    meal_count               numeric(4,1) NOT NULL DEFAULT 0,
    period_year              int          NOT NULL,
    period_month             int          NOT NULL,
    supersedes_meal_entry_id bigint       REFERENCES meal_entries (id) ON DELETE RESTRICT,
    recorded_by_user_id      bigint       NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    recorded_at_utc          timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_meal_slots CHECK (breakfast BETWEEN 0 AND 10 AND lunch BETWEEN 0 AND 10 AND dinner BETWEEN 0 AND 10),
    CONSTRAINT ck_meal_count CHECK (meal_count BETWEEN 0 AND 30),
    CONSTRAINT ck_meal_month CHECK (period_month BETWEEN 1 AND 12)
);

CREATE INDEX ix_meal_current      ON meal_entries (house_id, user_id, meal_date, recorded_at_utc DESC);
CREATE INDEX ix_meal_house_period ON meal_entries (house_id, period_year, period_month);


CREATE TABLE meal_entry_audits (
    id              bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    meal_entry_id   bigint       NOT NULL REFERENCES meal_entries (id) ON DELETE RESTRICT,
    house_id        bigint       NOT NULL,
    target_user_id  bigint       NOT NULL,
    actor_user_id   bigint       NOT NULL,
    old_meal_count  numeric(4,1),
    new_meal_count  numeric(4,1) NOT NULL,
    reason          varchar(200),
    occurred_at_utc timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX ix_meal_audit_house ON meal_entry_audits (house_id, occurred_at_utc DESC);


CREATE TABLE contributions (
    id                       bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id                 bigint        NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    user_id                  bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    amount                   numeric(18,2) NOT NULL,
    paid_on                  date          NOT NULL,
    period_year              int           NOT NULL,
    period_month             int           NOT NULL,
    source                   smallint      NOT NULL,
    fund_type                smallint      NOT NULL DEFAULT 1,
    note                     varchar(200)  NOT NULL DEFAULT '',
    source_expense_id        bigint        REFERENCES expenses (id) ON DELETE RESTRICT,
    corrects_contribution_id bigint        REFERENCES contributions (id) ON DELETE RESTRICT,
    recorded_by_user_id      bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    created_at_utc           timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_contribution_source    CHECK (source BETWEEN 1 AND 2),
    CONSTRAINT ck_contribution_fund_type CHECK (fund_type BETWEEN 1 AND 2),
    CONSTRAINT ck_contribution_month     CHECK (period_month BETWEEN 1 AND 12)
);

CREATE INDEX        ix_contribution_house_period      ON contributions (house_id, period_year, period_month);
CREATE INDEX        ix_contribution_house_period_fund ON contributions (house_id, period_year, period_month, fund_type);
CREATE UNIQUE INDEX ux_contribution_expense           ON contributions (source_expense_id) WHERE source_expense_id IS NOT NULL;


-- One row per home per month: the "book". Opened by a manager (status 1),
-- totals filled in and locked at finalize (status 2).
CREATE TABLE settlement_runs (
    id                         bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    house_id                   bigint        NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    period_year                int           NOT NULL,
    period_month               int           NOT NULL,
    total_meal_spending        numeric(18,2) NOT NULL DEFAULT 0,
    total_meals                numeric(10,1) NOT NULL DEFAULT 0,
    per_meal_rate              numeric(18,6) NOT NULL DEFAULT 0,
    total_equal_costs          numeric(18,2) NOT NULL DEFAULT 0,
    member_count_at_settlement int           NOT NULL DEFAULT 0,
    status                     smallint      NOT NULL DEFAULT 1,
    opened_by_user_id          bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    opened_at_utc              timestamptz   NOT NULL DEFAULT now(),
    computed_by_user_id        bigint        REFERENCES users (id) ON DELETE RESTRICT,
    computed_at_utc            timestamptz   DEFAULT now(),

    CONSTRAINT ck_settlement_status CHECK (status BETWEEN 1 AND 2),
    CONSTRAINT ck_settlement_month  CHECK (period_month BETWEEN 1 AND 12)
);

CREATE UNIQUE INDEX ux_settlement_book ON settlement_runs (house_id, period_year, period_month);
CREATE UNIQUE INDEX ux_settlement_open ON settlement_runs (house_id) WHERE status = 1;


-- Who is in the book: everyone in the home when it was opened, plus anyone a
-- manager adds later. Leaving the home does not remove someone from the book.
CREATE TABLE settlement_members (
    id                bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    settlement_run_id bigint      NOT NULL REFERENCES settlement_runs (id) ON DELETE CASCADE,
    user_id           bigint      NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    added_by_user_id  bigint      NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    added_at_utc      timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ux_settlement_member UNIQUE (settlement_run_id, user_id)
);

CREATE INDEX ix_settlement_member_user ON settlement_members (user_id);


CREATE TABLE settlement_lines (
    id                  bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    settlement_run_id   bigint        NOT NULL REFERENCES settlement_runs (id) ON DELETE CASCADE,
    user_id             bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    meal_count          numeric(10,1) NOT NULL,
    meal_cost           numeric(18,2) NOT NULL,
    equal_share         numeric(18,2) NOT NULL,
    contributions       numeric(18,2) NOT NULL,
    rounding_adjustment numeric(18,2) NOT NULL DEFAULT 0.00,
    net_amount          numeric(18,2) NOT NULL
);

CREATE UNIQUE INDEX ux_settlement_line      ON settlement_lines (settlement_run_id, user_id);
CREATE INDEX        ix_settlement_line_user ON settlement_lines (user_id);


CREATE TABLE settlement_transfers (
    id                bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    settlement_run_id bigint        NOT NULL REFERENCES settlement_runs (id) ON DELETE CASCADE,
    from_user_id      bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    to_user_id        bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    amount            numeric(18,2) NOT NULL,

    CONSTRAINT ck_transfer_amount   CHECK (amount > 0),
    CONSTRAINT ck_transfer_distinct CHECK (from_user_id <> to_user_id)
);

CREATE INDEX ix_settlement_transfer_run ON settlement_transfers (settlement_run_id);


-- ============================================================================
-- 8. Verification  (User_Verification.sql)
--
-- A user proves identity with an NID or birth certificate; a student also
-- attaches a student ID and a job holder an employee ID. A domestic helper
-- sends a photo of herself and her NID. Either pays the fee through the
-- (fake) bKash portal first. An admin then approves or rejects with a reason.
-- ============================================================================

-- subject_type: 1 User, 2 DomesticHelper.
-- status: 1 Pending, 2 Approved, 3 Rejected, 4 Cancelled (withdrawn by the applicant).
CREATE TABLE verification_requests (
    id                  bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id             bigint       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    subject_type        smallint     NOT NULL DEFAULT 1,
    status              smallint     NOT NULL DEFAULT 1,
    rejection_reason    varchar(500),
    submitted_at_utc    timestamptz  NOT NULL DEFAULT now(),
    decided_at_utc      timestamptz,
    decided_by_admin_id bigint       REFERENCES users (id) ON DELETE SET NULL,

    CONSTRAINT ck_verification_subject   CHECK (subject_type BETWEEN 1 AND 2),
    CONSTRAINT ck_verification_status    CHECK (status BETWEEN 1 AND 4),
    CONSTRAINT ck_verification_rejection CHECK (status <> 3 OR rejection_reason IS NOT NULL)
);

CREATE UNIQUE INDEX ux_verification_one_open ON verification_requests (user_id) WHERE status = 1;
CREATE INDEX        ix_verification_queue    ON verification_requests (status, submitted_at_utc);


-- One row per uploaded photo.
-- document_type: 1 NID, 2 Birth certificate, 3 Student ID, 4 Employee ID,
--                5 Passport, 6 Helper photo.
CREATE TABLE verification_documents (
    id                      bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    verification_request_id bigint       NOT NULL REFERENCES verification_requests (id) ON DELETE CASCADE,
    document_type           smallint     NOT NULL,
    document_url            text         NOT NULL,
    original_file_name      varchar(160) NOT NULL,
    uploaded_at_utc         timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_vdoc_type CHECK (document_type BETWEEN 1 AND 6)
);

CREATE UNIQUE INDEX ux_vdoc_request_type ON verification_documents (verification_request_id, document_type);


-- The fake bKash payment made right before an application is sent. The fee
-- code and amount are copied from fee_settings at payment time. The PIN is
-- never stored. verification_request_id is filled in once the application
-- that used this payment is created.
CREATE TABLE verification_payments (
    id                      bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id                 bigint        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    verification_request_id bigint        REFERENCES verification_requests (id) ON DELETE SET NULL,
    fee_code                varchar(40)   NOT NULL,
    amount_bdt              numeric(10,2) NOT NULL,
    bkash_number            varchar(20)   NOT NULL,
    transaction_id          varchar(20)   NOT NULL,
    paid_at_utc             timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_vpay_amount CHECK (amount_bdt >= 0)
);

CREATE UNIQUE INDEX ux_vpay_transaction ON verification_payments (transaction_id);
CREATE UNIQUE INDEX ux_vpay_request     ON verification_payments (verification_request_id) WHERE verification_request_id IS NOT NULL;
CREATE INDEX        ix_vpay_user        ON verification_payments (user_id, paid_at_utc DESC);


-- ============================================================================
-- 9. Admin  (Admin.sql)
--
-- Reports on housing posts, admin takedowns of housing and marketplace posts,
-- verification fees, posting plans, admin accounts and the audit log.
-- ============================================================================

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


-- A user reports a housing post; admins take the post down or dismiss it.
-- state: 1 Open, 2 Resolved (post taken down), 3 Dismissed.
CREATE TABLE housing_reports (
    id                   bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    post_id              bigint       NOT NULL REFERENCES housing_posts (id) ON DELETE CASCADE,
    reported_by_user_id  bigint       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    reason_id            smallint     NOT NULL REFERENCES housing_report_reasons (id) ON DELETE RESTRICT,
    details              varchar(300),
    state                smallint     NOT NULL DEFAULT 1,
    raised_at_utc        timestamptz  NOT NULL DEFAULT now(),
    decided_at_utc       timestamptz,
    decided_by_admin_id  bigint       REFERENCES users (id) ON DELETE SET NULL,

    CONSTRAINT ck_housing_report_state CHECK (state BETWEEN 1 AND 3)
);

CREATE INDEX        ix_housing_reports_queue   ON housing_reports (state, raised_at_utc);
CREATE INDEX        ix_housing_reports_post    ON housing_reports (post_id);
CREATE UNIQUE INDEX ux_housing_report_one_open ON housing_reports (post_id, reported_by_user_id) WHERE state = 1;


-- One row each time an admin strikes a post down, for housing (scope 1) and
-- marketplace (scope 2) alike. post_id points at housing_posts or
-- marketplace_listings depending on scope, so there is no foreign key.
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


-- A plan is a bundle of posts someone buys once. scope: 1 Housing, 2 Marketplace.
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


-- ============================================================================
-- 10. Domestic help  (Domestic_Help.sql)
--
-- The helpers (khala / bua) who register on Nestify, where they live, the
-- services they offer, their weekly open hours, the engagements bachelors book
-- with them and the reviews those engagements leave behind. Helper
-- verification reuses verification_requests with subject_type 2.
-- ============================================================================

-- One row per helper account (users.account_type = 2). Sign-up only fills the
-- users row; the API adds this row with rate 0 and paused the first time she
-- opens her workspace, and browse skips her until services, rate and address
-- are filled in. is_active is the "accepting bookings" switch.
CREATE TABLE domestic_helper_profiles (
    id               bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id          bigint        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    photo_url        text          NOT NULL DEFAULT 'https://res.cloudinary.com/dait0sacc/image/upload/v1774704629/k7ygnoel72ychr8ico6n.png',
    headline         varchar(120)  NOT NULL DEFAULT '',
    bio              varchar(1000),
    languages        varchar(120)  NOT NULL DEFAULT 'Bangla',
    years_experience int           NOT NULL DEFAULT 0,
    monthly_rate     numeric(18,2) NOT NULL,
    is_verified      boolean       NOT NULL DEFAULT false,
    is_active        boolean       NOT NULL DEFAULT true,
    average_rating   numeric(3,2),
    review_count     int           NOT NULL DEFAULT 0,
    created_at_utc   timestamptz   NOT NULL DEFAULT now(),
    updated_at_utc   timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_helper_rate       CHECK (monthly_rate >= 0),
    CONSTRAINT ck_helper_experience CHECK (years_experience >= 0),
    CONSTRAINT ck_helper_reviews    CHECK (review_count >= 0)
);

CREATE UNIQUE INDEX ux_helper_user   ON domestic_helper_profiles (user_id);
CREATE INDEX        ix_helper_rating ON domestic_helper_profiles (average_rating DESC) WHERE is_active;


-- Where the helper lives: the upazila she picks, a plain address line, and
-- the pin she drops on the map. Only the area is shown to bachelors.
CREATE TABLE helper_addresses (
    helper_profile_id bigint        PRIMARY KEY REFERENCES domestic_helper_profiles (id) ON DELETE CASCADE,
    upazila_id        int           NOT NULL REFERENCES upazilas (id) ON DELETE RESTRICT,
    address_line      varchar(300)  NOT NULL,
    latitude          numeric(9,6)  NOT NULL,
    longitude         numeric(9,6)  NOT NULL,
    updated_at_utc    timestamptz   NOT NULL DEFAULT now()
);

CREATE INDEX ix_helper_address_upazila ON helper_addresses (upazila_id);


-- service_type: 1 Cooking, 2 Cleaning, 3 Laundry, 4 Dishwashing,
-- 5 Grocery runs, 6 General help.
CREATE TABLE helper_services (
    helper_profile_id bigint   NOT NULL REFERENCES domestic_helper_profiles (id) ON DELETE CASCADE,
    service_type      smallint NOT NULL,

    PRIMARY KEY (helper_profile_id, service_type),
    CONSTRAINT ck_helper_service_type CHECK (service_type BETWEEN 1 AND 6)
);


-- The helper's weekly board, one row per open hour; an hour with no row is
-- off. day_of_week follows Postgres extract(dow): 0 Sunday .. 6 Saturday.
-- The board runs 6 AM to midnight, so hour is 6..23.
CREATE TABLE helper_weekly_availability (
    helper_profile_id bigint   NOT NULL REFERENCES domestic_helper_profiles (id) ON DELETE CASCADE,
    day_of_week       smallint NOT NULL,
    hour              smallint NOT NULL,

    PRIMARY KEY (helper_profile_id, day_of_week, hour),
    CONSTRAINT ck_helper_availability_day  CHECK (day_of_week BETWEEN 0 AND 6),
    CONSTRAINT ck_helper_availability_hour CHECK (hour BETWEEN 6 AND 23)
);


-- A home's manager or co-manager asks a helper for a monthly engagement on
-- behalf of the home; the helper accepts or declines. client_user_id is who
-- asked, home_id is the home she would work at.
-- status: 1 Requested, 2 Active (helper accepted), 3 Completed,
--         4 Declined, 5 Cancelled (withdrawn by the client).
-- monthly_rate is copied from the helper's profile at request time.
CREATE TABLE service_engagements (
    id                      bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    helper_profile_id       bigint        NOT NULL REFERENCES domestic_helper_profiles (id) ON DELETE RESTRICT,
    client_user_id          bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    home_id                 bigint        NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    status                  smallint      NOT NULL DEFAULT 1,
    monthly_rate            numeric(18,2) NOT NULL,
    message                 varchar(1000) NOT NULL DEFAULT '',
    decline_reason          varchar(500),
    requested_at_utc        timestamptz   NOT NULL DEFAULT now(),
    start_date              date,                       -- set when the helper accepts
    helper_confirmed_at_utc timestamptz,
    client_completed_at_utc timestamptz,
    helper_completed_at_utc timestamptz,
    completed_at_utc        timestamptz,
    cancelled_at_utc        timestamptz,

    CONSTRAINT ck_engagement_status CHECK (status BETWEEN 1 AND 5),
    CONSTRAINT ck_engagement_rate   CHECK (monthly_rate >= 0)
);

CREATE INDEX        ix_engagement_helper ON service_engagements (helper_profile_id, status);
CREATE INDEX        ix_engagement_client ON service_engagements (client_user_id, status);
CREATE INDEX        ix_engagement_home   ON service_engagements (home_id, status);
CREATE UNIQUE INDEX ux_engagement_open   ON service_engagements (helper_profile_id, home_id) WHERE status IN (1, 2);


-- What the client asked the helper to do. Same numbering as helper_services.
CREATE TABLE service_engagement_services (
    engagement_id bigint   NOT NULL REFERENCES service_engagements (id) ON DELETE CASCADE,
    service_type  smallint NOT NULL,

    PRIMARY KEY (engagement_id, service_type),
    CONSTRAINT ck_engagement_service_type CHECK (service_type BETWEEN 1 AND 6)
);


-- The weekly hours the client picked off the helper's board. While the
-- engagement is active these hours show as booked to everyone else.
CREATE TABLE service_engagement_slots (
    engagement_id bigint   NOT NULL REFERENCES service_engagements (id) ON DELETE CASCADE,
    day_of_week   smallint NOT NULL,
    hour          smallint NOT NULL,

    PRIMARY KEY (engagement_id, day_of_week, hour),
    CONSTRAINT ck_engagement_slot_day  CHECK (day_of_week BETWEEN 0 AND 6),
    CONSTRAINT ck_engagement_slot_hour CHECK (hour BETWEEN 6 AND 23)
);


-- One row per stretch a helper worked at a home: written the moment she
-- accepts the request (joined_on) and closed when the engagement ends
-- (left_on). Every member who lived in the home during that stretch took
-- her service, so every one of them may review her once.
CREATE TABLE helper_home_placements (
    id                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    engagement_id     bigint NOT NULL REFERENCES service_engagements (id) ON DELETE CASCADE,
    helper_profile_id bigint NOT NULL REFERENCES domestic_helper_profiles (id) ON DELETE CASCADE,
    home_id           bigint NOT NULL REFERENCES homes (id) ON DELETE CASCADE,
    joined_on         date   NOT NULL DEFAULT CURRENT_DATE,
    left_on           date,

    CONSTRAINT ck_placement_dates CHECK (left_on IS NULL OR left_on >= joined_on)
);

CREATE UNIQUE INDEX ux_placement_engagement ON helper_home_placements (engagement_id);
CREATE INDEX        ix_placement_home       ON helper_home_placements (home_id, joined_on DESC);
CREATE INDEX        ix_placement_helper     ON helper_home_placements (helper_profile_id, joined_on DESC);


-- A review hangs off a placement: only someone who lived in that home while
-- the helper worked there can write one, and each of them only once. The
-- helper may write one reply under the review.
CREATE TABLE helper_reviews (
    id                bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    placement_id      bigint        NOT NULL REFERENCES helper_home_placements (id) ON DELETE CASCADE,
    helper_profile_id bigint        NOT NULL REFERENCES domestic_helper_profiles (id) ON DELETE CASCADE,
    reviewer_user_id  bigint        NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    rating            smallint      NOT NULL,
    comment           varchar(1000) NOT NULL DEFAULT '',
    reply             varchar(500),
    replied_at_utc    timestamptz,
    created_at_utc    timestamptz   NOT NULL DEFAULT now(),
    is_hidden         boolean       NOT NULL DEFAULT false,

    CONSTRAINT ck_review_rating CHECK (rating BETWEEN 1 AND 5)
);

CREATE UNIQUE INDEX ux_review_placement_reviewer ON helper_reviews (placement_id, reviewer_user_id);
CREATE INDEX        ix_review_helper             ON helper_reviews (helper_profile_id, created_at_utc DESC) WHERE NOT is_hidden;
