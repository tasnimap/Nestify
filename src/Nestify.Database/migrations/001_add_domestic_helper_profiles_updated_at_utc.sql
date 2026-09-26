-- Add the profile update timestamp required by the availability save/update
-- operation, which updates domestic_helper_profiles.updated_at_utc.
ALTER TABLE public.domestic_helper_profiles
    ADD COLUMN IF NOT EXISTS updated_at_utc timestamptz NOT NULL DEFAULT now();
