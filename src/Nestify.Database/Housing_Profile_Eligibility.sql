-- Non-destructive migration for enforcing housing eligibility from a user's
-- own profile. Run once after User_Additional_profile_info.sql.
ALTER TABLE user_additional_profile_info
    ADD COLUMN IF NOT EXISTS gender smallint,
    ADD COLUMN IF NOT EXISTS date_of_birth date,
    ADD COLUMN IF NOT EXISTS is_smoker boolean,
    ADD COLUMN IF NOT EXISTS is_drinker boolean;

ALTER TABLE user_additional_profile_info
    DROP CONSTRAINT IF EXISTS ck_profile_gender;

ALTER TABLE user_additional_profile_info
    ADD CONSTRAINT ck_profile_gender CHECK (gender IS NULL OR gender BETWEEN 0 AND 1);
