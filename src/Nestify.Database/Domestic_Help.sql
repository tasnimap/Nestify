-- Domestic help: the helpers (khala / bua) who register on Nestify, where
-- they live, the services they offer, their weekly open hours, the
-- engagements bachelors book with them and the reviews those engagements
-- leave behind.
--
-- Needs users (Autthintication.sql), upazilas
-- (Bangladesh_Administrative_Structure.sql), homes / home_members
-- (User_Home.sql), user_additional_profile_info
-- (User_Additional_profile_info.sql) and the verification tables
-- (User_Verification.sql) to exist first.
--
-- Helper verification reuses verification_requests with subject_type 2: the
-- helper pays the 'helper_verification' fee from fee_settings (Admin.sql)
-- through the fake bKash portal, then sends a photo of herself and her NID.
-- Everything about a helper that is not on the users row lives in the tables
-- below, one table per concern.

DROP TABLE IF EXISTS helper_reviews              CASCADE;
DROP TABLE IF EXISTS helper_home_placements      CASCADE;
DROP TABLE IF EXISTS service_engagement_slots    CASCADE;
DROP TABLE IF EXISTS service_engagement_services CASCADE;
DROP TABLE IF EXISTS service_engagement_details  CASCADE;   -- old workspace migration
DROP TABLE IF EXISTS service_engagements         CASCADE;
DROP TABLE IF EXISTS helper_weekly_availability  CASCADE;
DROP TABLE IF EXISTS helper_services             CASCADE;
DROP TABLE IF EXISTS helper_addresses            CASCADE;
DROP TABLE IF EXISTS domestic_helper_profiles    CASCADE;


-- A helper's verification carries a photo of herself next to the NID, so the
-- document list from User_Verification.sql gets one more type:
-- 6 Helper photo.
ALTER TABLE verification_documents DROP CONSTRAINT IF EXISTS ck_vdoc_type;
ALTER TABLE verification_documents ADD  CONSTRAINT ck_vdoc_type CHECK (document_type BETWEEN 1 AND 6);


-- ========================= Helper profiles =========================
-- One row per helper account (users.account_type = 2). is_active is the
-- "accepting bookings" switch on the dashboard; a paused helper is hidden
-- from browse. average_rating / review_count are kept up to date by the API
-- every time a review is added.

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


-- ========================= Helper addresses =========================
-- Where the helper lives: the upazila she picks from the cascade, a plain
-- address line, and the pin she drops on the map. The coordinates are only
-- stored for now; distance search comes later.

CREATE TABLE helper_addresses (
    helper_profile_id bigint        PRIMARY KEY REFERENCES domestic_helper_profiles (id) ON DELETE CASCADE,
    upazila_id        int           NOT NULL REFERENCES upazilas (id) ON DELETE RESTRICT,
    address_line      varchar(300)  NOT NULL,
    latitude          numeric(9,6)  NOT NULL,
    longitude         numeric(9,6)  NOT NULL,
    updated_at_utc    timestamptz   NOT NULL DEFAULT now()
);

CREATE INDEX ix_helper_address_upazila ON helper_addresses (upazila_id);


-- ========================= Helper services =========================
-- service_type: 1 Cooking, 2 Cleaning, 3 Laundry, 4 Dishwashing,
-- 5 Grocery runs, 6 General help.

CREATE TABLE helper_services (
    helper_profile_id bigint   NOT NULL REFERENCES domestic_helper_profiles (id) ON DELETE CASCADE,
    service_type      smallint NOT NULL,

    PRIMARY KEY (helper_profile_id, service_type),
    CONSTRAINT ck_helper_service_type CHECK (service_type BETWEEN 1 AND 6)
);


-- ========================= Weekly availability =========================
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


-- ========================= Service engagements =========================
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


-- ========================= Engagement services =========================
-- What the client asked the helper to do. Same numbering as helper_services.

CREATE TABLE service_engagement_services (
    engagement_id bigint   NOT NULL REFERENCES service_engagements (id) ON DELETE CASCADE,
    service_type  smallint NOT NULL,

    PRIMARY KEY (engagement_id, service_type),
    CONSTRAINT ck_engagement_service_type CHECK (service_type BETWEEN 1 AND 6)
);


-- ========================= Engagement slots =========================
-- The weekly hours the client picked off the helper's board. Same day and
-- hour numbering as helper_weekly_availability. While the engagement is
-- active these hours show as booked to everyone else.

CREATE TABLE service_engagement_slots (
    engagement_id bigint   NOT NULL REFERENCES service_engagements (id) ON DELETE CASCADE,
    day_of_week   smallint NOT NULL,
    hour          smallint NOT NULL,

    PRIMARY KEY (engagement_id, day_of_week, hour),
    CONSTRAINT ck_engagement_slot_day  CHECK (day_of_week BETWEEN 0 AND 6),
    CONSTRAINT ck_engagement_slot_hour CHECK (hour BETWEEN 6 AND 23)
);


-- ========================= Home placements =========================
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


-- ========================= Helper reviews =========================
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
