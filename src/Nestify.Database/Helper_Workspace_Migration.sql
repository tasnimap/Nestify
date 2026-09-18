-- Manual pgAdmin migration. Do not run automatically.
-- Persists the weekly board and the engagement details/hours rendered by the
-- Domestic Help workspace. Existing service_engagements rows remain valid.

ALTER TABLE service_engagements
    DROP CONSTRAINT IF EXISTS ck_engagement_status;
ALTER TABLE service_engagements
    ADD CONSTRAINT ck_engagement_status CHECK (status BETWEEN 1 AND 6);

CREATE TABLE IF NOT EXISTS helper_weekly_availability (
    helper_profile_id bigint NOT NULL REFERENCES domestic_helper_profiles(id) ON DELETE CASCADE,
    day_of_week smallint NOT NULL,
    hour smallint NOT NULL,
    is_open boolean NOT NULL DEFAULT true,
    PRIMARY KEY (helper_profile_id, day_of_week, hour),
    CONSTRAINT ck_helper_availability_day CHECK (day_of_week BETWEEN 0 AND 6),
    CONSTRAINT ck_helper_availability_hour CHECK (hour BETWEEN 6 AND 23)
);

CREATE TABLE IF NOT EXISTS service_engagement_details (
    engagement_id bigint PRIMARY KEY REFERENCES service_engagements(id) ON DELETE CASCADE,
    area varchar(160) NOT NULL DEFAULT '',
    address varchar(300) NOT NULL DEFAULT '',
    home_type varchar(100) NOT NULL DEFAULT '',
    client_phone varchar(40) NOT NULL DEFAULT '',
    monthly_rate numeric(18,2) NOT NULL DEFAULT 0,
    services text[] NOT NULL DEFAULT '{}',
    message varchar(1000) NOT NULL DEFAULT '',
    decline_reason varchar(500)
);

CREATE TABLE IF NOT EXISTS service_engagement_slots (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    engagement_id bigint NOT NULL REFERENCES service_engagements(id) ON DELETE CASCADE,
    visit_date date NOT NULL,
    start_hour smallint NOT NULL,
    CONSTRAINT ck_engagement_slot_hour CHECK (start_hour BETWEEN 6 AND 23),
    CONSTRAINT ux_engagement_slot UNIQUE (engagement_id, visit_date, start_hour)
);
CREATE INDEX IF NOT EXISTS ix_engagement_slot_date ON service_engagement_slots(visit_date, engagement_id);
