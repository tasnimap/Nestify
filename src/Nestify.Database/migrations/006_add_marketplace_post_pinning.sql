-- Marketplace post pinning feature with bKash payment support
BEGIN;

ALTER TABLE public.marketplace_listings
    ADD COLUMN IF NOT EXISTS is_pinned boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS pinned_at_utc timestamptz,
    ADD COLUMN IF NOT EXISTS pinned_until_utc timestamptz;

CREATE INDEX IF NOT EXISTS ix_marketplace_listings_pinned
    ON public.marketplace_listings (is_pinned, pinned_until_utc)
    WHERE status = 1;

CREATE TABLE IF NOT EXISTS public.marketplace_pin_payments (
    id              bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    listing_id      bigint         NOT NULL REFERENCES marketplace_listings (id) ON DELETE CASCADE,
    user_id         bigint         NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    amount_bdt      numeric(10,2)  NOT NULL,
    bkash_number    varchar(20)    NOT NULL,
    transaction_id  varchar(40)    NOT NULL,
    pinned_days     int            NOT NULL DEFAULT 7,
    paid_at_utc     timestamptz    NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_marketplace_pin_payments_listing
    ON public.marketplace_pin_payments (listing_id, paid_at_utc DESC);

COMMIT;
