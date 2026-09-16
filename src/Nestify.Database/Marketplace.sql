-- Second-hand marketplace: listings, their photos, buy interests, view counts
-- and reports. Every listing, interest and report points at a row in users.
--
-- Needs users (Autthintication.sql) and divisions / districts / upazilas
-- (Bangladesh_Administrative_Structure.sql) to exist first.

DROP TABLE IF EXISTS marketplace_reports        CASCADE;
DROP TABLE IF EXISTS marketplace_report_reasons CASCADE;
DROP TABLE IF EXISTS marketplace_listing_views  CASCADE;
DROP TABLE IF EXISTS marketplace_buy_interests  CASCADE;
DROP TABLE IF EXISTS marketplace_listing_images CASCADE;
DROP TABLE IF EXISTS marketplace_listings       CASCADE;
DROP TABLE IF EXISTS marketplace_conditions     CASCADE;
DROP TABLE IF EXISTS marketplace_categories     CASCADE;


-- ========================= Categories =========================
-- Lookup of the fixed item categories the browse filter and sell form use.
-- Kept in a table so a new category is an INSERT, not a schema change.
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


-- ========================= Conditions =========================
-- Lookup of the condition grades (New, Like new, Good, Fair) shown as chips.
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


-- ========================= Listings =========================
-- One row per item a user puts up for sale. seller_user_id is the users.id of
-- the poster. Location is stored as ids into the administrative tables so the
-- browse page can filter by division / district / upazila; area_name is the
-- free-text handover spot the seller types ("Mirpur 10, near the bus stand").
-- status: 1 Active, 2 Sold, 3 Removed. When the seller marks it sold they pick
-- the buyer from the requests, kept in sold_to_user_id. A removed row stays so
-- old interests and reports still point at something.

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


-- ========================= Listing images =========================
-- Photos of a listing, one row per photo. image_url is the already-uploaded
-- picture URL. sort_order 0 is the cover image shown on the card.

CREATE TABLE marketplace_listing_images (
    id              bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    listing_id      bigint      NOT NULL REFERENCES marketplace_listings (id) ON DELETE CASCADE,
    image_url       text        NOT NULL,
    sort_order      smallint    NOT NULL DEFAULT 0,
    uploaded_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_marketplace_listing_images_order ON marketplace_listing_images (listing_id, sort_order);


-- ========================= Buy interests =========================
-- A buyer says "I want this" on a listing with a short message. The seller
-- accepts or declines; the buyer can withdraw while the listing is still active.
-- Phone numbers are only shown to both sides once status is Accepted (read from
-- users at that point). When the listing is marked sold, the chosen buyer's
-- request becomes Fulfilled and every other open request becomes Closed; nobody
-- can withdraw after that.
-- status: 1 Pending, 2 Accepted, 3 Declined, 4 Withdrawn, 5 Fulfilled, 6 Closed.
-- A buyer can have only one open (pending or accepted) interest per listing.

CREATE TABLE marketplace_buy_interests (
    id                      bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    listing_id              bigint       NOT NULL REFERENCES marketplace_listings (id) ON DELETE CASCADE,
    buyer_user_id           bigint       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    message                 varchar(500) NOT NULL,
    status                  smallint     NOT NULL DEFAULT 1,   -- 1 Pending, 2 Accepted, 3 Declined, 4 Withdrawn, 5 Fulfilled, 6 Closed
    preferred_handover_area varchar(120),                     -- seller's note shown to the buyer after acceptance
    created_at_utc          timestamptz  NOT NULL DEFAULT now(),
    responded_at_utc        timestamptz,                      -- when the seller accepted / declined
    withdrawn_at_utc        timestamptz,

    CONSTRAINT ck_marketplace_interest_status CHECK (status BETWEEN 1 AND 6)
);

CREATE INDEX        ix_marketplace_interests_listing ON marketplace_buy_interests (listing_id, status);
CREATE INDEX        ix_marketplace_interests_buyer   ON marketplace_buy_interests (buyer_user_id, created_at_utc DESC);
CREATE UNIQUE INDEX ux_marketplace_interest_one_open ON marketplace_buy_interests (listing_id, buyer_user_id) WHERE status IN (1, 2);


-- ========================= Listing views =========================
-- One row each time someone opens a listing's detail page. viewer_user_id is
-- NULL for a signed-out visitor. "My listings" counts rows here per listing.

CREATE TABLE marketplace_listing_views (
    id             bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    listing_id     bigint      NOT NULL REFERENCES marketplace_listings (id) ON DELETE CASCADE,
    viewer_user_id bigint      REFERENCES users (id) ON DELETE SET NULL,
    viewed_at_utc  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_marketplace_views_listing ON marketplace_listing_views (listing_id);


-- ========================= Report reasons =========================
-- Lookup of the reasons a user can pick when reporting a listing. Same idea as
-- categories: adding a reason is an INSERT.

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


-- ========================= Reports =========================
-- A user reports a listing; admins see the queue on the moderation page and
-- either strike the listing down (status 3 on the listing) or dismiss the report.
-- state: 1 Open, 2 Resolved (listing removed), 3 Dismissed.

CREATE TABLE marketplace_reports (
    id                   bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    listing_id           bigint       NOT NULL REFERENCES marketplace_listings (id) ON DELETE CASCADE,
    reported_by_user_id  bigint       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    reason_id            smallint     NOT NULL REFERENCES marketplace_report_reasons (id) ON DELETE RESTRICT,
    details              varchar(300),            -- optional free text from the reporter
    state                smallint     NOT NULL DEFAULT 1,   -- 1 Open, 2 Resolved, 3 Dismissed
    raised_at_utc        timestamptz  NOT NULL DEFAULT now(),
    decided_at_utc       timestamptz,
    decided_by_admin_id  bigint       REFERENCES users (id) ON DELETE SET NULL,

    CONSTRAINT ck_marketplace_report_state CHECK (state BETWEEN 1 AND 3)
);

CREATE INDEX        ix_marketplace_reports_queue    ON marketplace_reports (state, raised_at_utc);
CREATE INDEX        ix_marketplace_reports_listing  ON marketplace_reports (listing_id);
CREATE UNIQUE INDEX ux_marketplace_report_one_open  ON marketplace_reports (listing_id, reported_by_user_id) WHERE state = 1;
