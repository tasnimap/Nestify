-- Add the normalized domestic-helper relations required by the current backend.
-- This migration is intentionally additive: legacy relationship columns and
-- their values remain available until a verified data crosswalk is supplied.

BEGIN;

-- Canonical helper address table. It was absent from the inspected local schema.
CREATE TABLE IF NOT EXISTS public.helper_addresses (
    helper_profile_id bigint       PRIMARY KEY REFERENCES public.domestic_helper_profiles (id) ON DELETE CASCADE,
    upazila_id        int          NOT NULL REFERENCES public.upazilas (id) ON DELETE RESTRICT,
    address_line      varchar(300) NOT NULL,
    latitude          numeric(9,6) NOT NULL,
    longitude         numeric(9,6) NOT NULL,
    updated_at_utc    timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_helper_address_upazila
    ON public.helper_addresses (upazila_id);

-- Canonical service selections were absent from the inspected local schema.
CREATE TABLE IF NOT EXISTS public.service_engagement_services (
    engagement_id bigint   NOT NULL REFERENCES public.service_engagements (id) ON DELETE CASCADE,
    service_type  smallint NOT NULL,
    PRIMARY KEY (engagement_id, service_type),
    CONSTRAINT ck_engagement_service_type CHECK (service_type BETWEEN 1 AND 6)
);

-- Canonical household relationship. Existing house_id values are not mapped:
-- houses is empty and no explicit houses.id -> homes.id crosswalk is available.
-- Keep home_id nullable for the unmapped legacy engagement; new backend writes
-- provide home_id. Keep house_id and relax only its NOT NULL requirement so new
-- canonical inserts do not need to invent a legacy house relationship.
ALTER TABLE public.service_engagements
    ADD COLUMN IF NOT EXISTS home_id bigint;

DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'service_engagements'
          AND column_name = 'house_id'
          AND is_nullable = 'NO'
    ) THEN
        ALTER TABLE public.service_engagements
            ALTER COLUMN house_id DROP NOT NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'public.service_engagements'::regclass
          AND conname = 'fk_service_engagements_home_id'
    ) THEN
        ALTER TABLE public.service_engagements
            ADD CONSTRAINT fk_service_engagements_home_id
            FOREIGN KEY (home_id) REFERENCES public.homes (id) ON DELETE CASCADE
            NOT VALID;
    END IF;
END;
$migration$;

ALTER TABLE public.service_engagements
    VALIDATE CONSTRAINT fk_service_engagements_home_id;

COMMENT ON COLUMN public.service_engagements.home_id IS
    'Canonical home relationship. Nullable only for preserved legacy rows awaiting a verified house-to-home mapping.';
COMMENT ON COLUMN public.service_engagements.house_id IS
    'Legacy relationship retained without rewriting existing values; new canonical engagements use home_id.';

CREATE INDEX IF NOT EXISTS ix_engagement_home_normalized
    ON public.service_engagements (home_id, status);

CREATE UNIQUE INDEX IF NOT EXISTS ux_engagement_open_home
    ON public.service_engagements (helper_profile_id, home_id)
    WHERE status IN (1, 2) AND home_id IS NOT NULL;

-- One row per helper stint. No placement is backfilled here because the
-- existing engagement has no verified home_id and its lifecycle dates have
-- not been inspected. Existing engagement/review rows are left untouched.
CREATE TABLE IF NOT EXISTS public.helper_home_placements (
    id                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    engagement_id     bigint NOT NULL REFERENCES public.service_engagements (id) ON DELETE CASCADE,
    helper_profile_id bigint NOT NULL REFERENCES public.domestic_helper_profiles (id) ON DELETE CASCADE,
    home_id           bigint NOT NULL REFERENCES public.homes (id) ON DELETE CASCADE,
    joined_on         date   NOT NULL DEFAULT CURRENT_DATE,
    left_on           date,
    CONSTRAINT ck_placement_dates CHECK (left_on IS NULL OR left_on >= joined_on)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_placement_engagement
    ON public.helper_home_placements (engagement_id);
CREATE INDEX IF NOT EXISTS ix_placement_home
    ON public.helper_home_placements (home_id, joined_on DESC);
CREATE INDEX IF NOT EXISTS ix_placement_helper
    ON public.helper_home_placements (helper_profile_id, joined_on DESC);

-- Keep the old service_engagement_id and its values. Add the canonical
-- placement_id as nullable because the existing review cannot yet be linked to
-- a placement without a verified engagement home and joining date.
-- Allow new canonical reviews to omit the legacy relationship column while
-- preserving all values already stored in it.
DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'helper_reviews'
          AND column_name = 'service_engagement_id'
          AND is_nullable = 'NO'
    ) THEN
        ALTER TABLE public.helper_reviews
            ALTER COLUMN service_engagement_id DROP NOT NULL;
    END IF;
END;
$migration$;

ALTER TABLE public.helper_reviews
    ADD COLUMN IF NOT EXISTS placement_id bigint;

DO $migration$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'public.helper_reviews'::regclass
          AND conname = 'fk_helper_reviews_placement_id'
    ) THEN
        ALTER TABLE public.helper_reviews
            ADD CONSTRAINT fk_helper_reviews_placement_id
            FOREIGN KEY (placement_id)
            REFERENCES public.helper_home_placements (id)
            ON DELETE CASCADE
            NOT VALID;
    END IF;
END;
$migration$;

ALTER TABLE public.helper_reviews
    VALIDATE CONSTRAINT fk_helper_reviews_placement_id;

COMMENT ON COLUMN public.helper_reviews.placement_id IS
    'Canonical placement relationship. Nullable only for preserved legacy reviews awaiting a verified placement mapping.';
COMMENT ON COLUMN public.helper_reviews.service_engagement_id IS
    'Legacy review relationship retained without rewriting existing values; new reviews use placement_id.';

CREATE INDEX IF NOT EXISTS ix_helper_reviews_placement
    ON public.helper_reviews (placement_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_review_placement_reviewer
    ON public.helper_reviews (placement_id, reviewer_user_id);
CREATE INDEX IF NOT EXISTS ix_review_helper
    ON public.helper_reviews (helper_profile_id, created_at_utc DESC)
    WHERE NOT is_hidden;

COMMIT;
