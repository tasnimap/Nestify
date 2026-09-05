-- ============================================================================
-- Bangladesh administrative structure
--
--   division -> district -> upazila / thana
--
-- Upazilas and metropolitan thanas share one table. A rural upazila and a
-- metropolitan thana sit at the same level of the hierarchy (both are the third
-- administrative tier under a district), so splitting them into two tables would
-- force every lookup to union them back together. The is_metropolitan_thana flag
-- tells them apart for the places where that matters, such as showing "Thana"
-- instead of "Upazila" in a label.
--
-- Run this file first, then seed/bangladesh_administrative_seed.sql.
--
-- If an older copy of these three tables already exists in the database, drop
-- them before running this file:
--
--   DROP TABLE IF EXISTS upazilas, districts, divisions CASCADE;
--
-- Note that CASCADE also drops the foreign keys other tables point at them with,
-- so re-create those afterwards.
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
-- numbered from 90001 up so the two ranges never collide when the upstream
-- dataset adds an upazila.
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
