-- Extra profile fields that the registration form on /auth does not ask for.
-- One row per user, linked by users.id. The row is created when the account is
-- registered; if an older account has no row yet, the API creates one the first
-- time that user opens the profile page.

DROP TABLE IF EXISTS user_additional_profile_info CASCADE;

CREATE TABLE user_additional_profile_info (
    user_id             bigint       PRIMARY KEY REFERENCES users (id) ON DELETE CASCADE,
    profile_picture_url text         NOT NULL DEFAULT 'https://res.cloudinary.com/dait0sacc/image/upload/v1774704629/k7ygnoel72ychr8ico6n.png',
    occupation          varchar(120),
    organization_name   varchar(160),                    -- workplace or school / university name
    gender              smallint,                        -- 0 Male, 1 Female
    date_of_birth       date,
    is_smoker           boolean      NOT NULL DEFAULT false,
    is_drinker          boolean      NOT NULL DEFAULT false,
    is_verified         boolean      NOT NULL DEFAULT false,
    address             varchar(250),
    whatsapp_number     varchar(20),
    facebook_url        varchar(250),
    x_url               varchar(250),
    instagram_url       varchar(250),
    created_at_utc      timestamptz  NOT NULL DEFAULT now(),
    updated_at_utc      timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_profile_gender CHECK (gender IS NULL OR gender BETWEEN 0 AND 1)
);
