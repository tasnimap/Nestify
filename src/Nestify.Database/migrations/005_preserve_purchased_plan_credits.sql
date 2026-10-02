-- Keep a purchaser's marketplace credits usable even when an admin later edits
-- or deletes the catalog plan that originally created them.
BEGIN;

ALTER TABLE public.post_plans
    ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT false;

ALTER TABLE public.plan_purchases
    ADD COLUMN IF NOT EXISTS scope smallint;

UPDATE public.plan_purchases pp
SET scope = p.scope
FROM public.post_plans p
WHERE p.id = pp.plan_id
  AND pp.scope IS NULL;

ALTER TABLE public.plan_purchases
    ALTER COLUMN scope SET NOT NULL;

CREATE INDEX IF NOT EXISTS ix_plan_purchases_user_scope_expiry
    ON public.plan_purchases (user_id, scope, expires_at_utc DESC);

COMMIT;
