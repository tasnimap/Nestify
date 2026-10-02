-- Housing posting credits belong to a home and are shared by its current
-- members, while user_id records which member made the purchase.
BEGIN;

ALTER TABLE public.plan_purchases
    ADD COLUMN IF NOT EXISTS home_id bigint REFERENCES public.homes (id) ON DELETE CASCADE;

CREATE INDEX IF NOT EXISTS ix_plan_purchases_home_scope_expiry
    ON public.plan_purchases (home_id, scope, expires_at_utc DESC)
    WHERE home_id IS NOT NULL;

COMMIT;
