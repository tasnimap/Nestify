-- Manual pgAdmin migration. Do not run automatically.
-- The live database still has the original M5 status constraint (1..3) in
-- addition to the later cancellation constraint (1..4), which prevents a
-- cancellation from being stored. Verification documents are hosted using the
-- established Cloudinary upload flow, so retain their returned URL explicitly.

ALTER TABLE verification_requests
    DROP CONSTRAINT IF EXISTS ck_verification_status;

ALTER TABLE verification_documents
    ADD COLUMN IF NOT EXISTS storage_url text;
