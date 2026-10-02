-- Add paid pinning for Housing posts, matching Marketplace pin durations and fees.
BEGIN;

ALTER TABLE public.housing_posts
    ADD COLUMN IF NOT EXISTS is_pinned boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS pinned_at_utc timestamptz,
    ADD COLUMN IF NOT EXISTS pinned_until_utc timestamptz;

CREATE INDEX IF NOT EXISTS ix_housing_posts_pinned
    ON public.housing_posts (is_pinned, pinned_until_utc)
    WHERE status = 1;

CREATE TABLE IF NOT EXISTS public.housing_pin_payments (
    id              bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    post_id          bigint         NOT NULL REFERENCES public.housing_posts (id) ON DELETE CASCADE,
    user_id          bigint         NOT NULL REFERENCES public.users (id) ON DELETE CASCADE,
    amount_bdt       numeric(10,2)  NOT NULL,
    bkash_number     varchar(20)    NOT NULL,
    transaction_id   varchar(40)    NOT NULL,
    pinned_days      int            NOT NULL DEFAULT 7,
    paid_at_utc      timestamptz    NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_housing_pin_payments_post
    ON public.housing_pin_payments (post_id, paid_at_utc DESC);

COMMIT;
