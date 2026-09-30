-- Allow an unset smoking/drinking preference for existing and new profiles.
-- Safe to run more than once on a live database.
BEGIN;

ALTER TABLE public.user_additional_profile_info
    ALTER COLUMN is_smoker DROP NOT NULL;

ALTER TABLE public.user_additional_profile_info
    ALTER COLUMN is_drinker DROP NOT NULL;

COMMIT;
