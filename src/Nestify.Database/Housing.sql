-- Housing: a manager or co-manager posts that their home has room for more
-- bachelors, seekers browse those posts and ask to book a seat.
--
-- A post always belongs to a home. Its location (area, division) is read from
-- the home, and "seats available" is never stored: it is
--   home_capacity.max_occupants - count of active home_members
-- worked out whenever the post is read, so it stays right as people join or leave.
--
-- Needs users (Autthintication.sql), homes / home_members / home_capacity
-- (User_Home.sql) and divisions / districts / upazilas
-- (Bangladesh_Administrative_Structure.sql) to exist first.

DROP TABLE IF EXISTS housing_bookings          CASCADE;
DROP TABLE IF EXISTS housing_post_images       CASCADE;
DROP TABLE IF EXISTS housing_post_requirements CASCADE;
DROP TABLE IF EXISTS housing_posts             CASCADE;
DROP TABLE IF EXISTS housing_listing_types     CASCADE;


-- ========================= Listing types =========================
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


-- ========================= Posts =========================
-- One row per listing. posted_by_user_id is who wrote it, but any manager or
-- co-manager of the home can edit, close, reopen or delete it.
-- status: 1 Active, 2 Closed, 3 Filled. A closed post drops out of browse but
-- stays on "my posts" so it can be reopened. Filled is set by the API the moment
-- the home's active members reach max_occupants; it cannot be reopened while
-- the home stays full. Delete is a real delete and takes the photos,
-- requirements and bookings with it.

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


-- ========================= Requirements =========================
-- Who can apply. One row per post; NULL / false means no requirement on that
-- trait. gender: 0 Male, 1 Female. occupation: 0 Student, 1 Working.
-- Age is optional on either end: min only, max only, both or neither.

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


-- ========================= Post images =========================
-- Photos of the place, one row per photo. sort_order 0 is the cover image.

CREATE TABLE housing_post_images (
    id              bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    post_id         bigint      NOT NULL REFERENCES housing_posts (id) ON DELETE CASCADE,
    image_url       text        NOT NULL,
    sort_order      smallint    NOT NULL DEFAULT 0,
    uploaded_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_housing_post_images_order ON housing_post_images (post_id, sort_order);


-- ========================= Bookings =========================
-- A seeker who is in no home asks to book a seat on a post with a short
-- message. A manager or co-manager of the home accepts or rejects (plain
-- members cannot); the seeker can withdraw while it is still pending.
-- Once accepted, the managers and co-managers see the seeker's contact and the
-- seeker sees the contact of the one person who accepted (decided_by_user_id).
-- Getting into the home then happens outside the site: the seeker joins with
-- the join code or is added by email, and at that moment the API sets this row
-- to Joined. When the home fills up, every other open booking on its posts is
-- set to HouseFull.
-- status: 1 Pending, 2 Accepted, 3 Rejected, 4 Withdrawn, 5 Joined, 6 HouseFull.
-- A seeker can have only one open (pending or accepted) booking per post.

CREATE TABLE housing_bookings (
    id                 bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    post_id            bigint       NOT NULL REFERENCES housing_posts (id) ON DELETE CASCADE,
    requester_user_id  bigint       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    message            varchar(500),                     -- seeker's note to the manager
    reply_message      varchar(500),                     -- manager's note back, mostly on reject
    status             smallint     NOT NULL DEFAULT 1,  -- 1 Pending, 2 Accepted, 3 Rejected, 4 Withdrawn, 5 Joined, 6 HouseFull
    requested_at_utc   timestamptz  NOT NULL DEFAULT now(),
    decided_at_utc     timestamptz,
    decided_by_user_id bigint       REFERENCES users (id) ON DELETE SET NULL,

    CONSTRAINT ck_housing_booking_status CHECK (status BETWEEN 1 AND 6)
);

CREATE INDEX        ix_housing_bookings_post      ON housing_bookings (post_id, status);
CREATE INDEX        ix_housing_bookings_requester ON housing_bookings (requester_user_id, requested_at_utc DESC);
CREATE UNIQUE INDEX ux_housing_booking_one_open   ON housing_bookings (post_id, requester_user_id) WHERE status IN (1, 2);
