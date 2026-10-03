-- Keep helper review data in the required nestify schema. Moving the existing
-- table preserves its rows, identity, indexes, constraints and foreign keys.
BEGIN;

CREATE SCHEMA IF NOT EXISTS nestify;

DO $migration$
DECLARE
    public_table_exists boolean;
    nestify_table_exists boolean;
BEGIN
    SELECT to_regclass('public.helper_reviews') IS NOT NULL INTO public_table_exists;
    SELECT to_regclass('nestify.helper_reviews') IS NOT NULL INTO nestify_table_exists;

    IF public_table_exists AND NOT nestify_table_exists THEN
        ALTER TABLE public.helper_reviews SET SCHEMA nestify;
    ELSIF public_table_exists AND nestify_table_exists THEN
        RAISE EXCEPTION
            'Both public.helper_reviews and nestify.helper_reviews exist. Compare them and resolve the duplicate explicitly; no rows were moved.';
    ELSIF NOT public_table_exists AND NOT nestify_table_exists THEN
        RAISE EXCEPTION
            'Neither public.helper_reviews nor nestify.helper_reviews exists. Apply the canonical domestic-helper schema first.';
    END IF;
END;
$migration$;

-- The existing composite unique index enforces one review per placement and
-- reviewer. The API limits eligible reviewers to the engagement requester,
-- which makes this one review per engagement without discarding housemate data.
CREATE UNIQUE INDEX IF NOT EXISTS nestify.ux_review_placement_reviewer
    ON nestify.helper_reviews (placement_id, reviewer_user_id);
CREATE INDEX IF NOT EXISTS nestify.ix_review_helper
    ON nestify.helper_reviews (helper_profile_id, created_at_utc DESC)
    WHERE NOT is_hidden;

COMMIT;
