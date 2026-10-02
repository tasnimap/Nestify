-- Preserve the amount paid for each posting plan so reporting remains accurate
-- after an administrator changes a plan's current price.
BEGIN;

ALTER TABLE public.plan_purchases
    ADD COLUMN IF NOT EXISTS amount_bdt numeric(10,2);

UPDATE public.plan_purchases pp
SET amount_bdt = p.price_bdt
FROM public.post_plans p
WHERE p.id = pp.plan_id
  AND pp.amount_bdt IS NULL;

ALTER TABLE public.plan_purchases
    ALTER COLUMN amount_bdt SET NOT NULL;

COMMIT;
