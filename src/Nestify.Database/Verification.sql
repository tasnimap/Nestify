-- Apply after Autthintication.sql and User_Additional_profile_info.sql.
ALTER TABLE user_additional_profile_info
    ADD COLUMN IF NOT EXISTS organization_name varchar(160),
    ADD COLUMN IF NOT EXISTS is_verified boolean NOT NULL DEFAULT false;

CREATE TABLE IF NOT EXISTS verification_requests (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id bigint NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    document_type varchar(40) NOT NULL,
    document_url text NOT NULL,
    original_file_name varchar(160) NOT NULL,
    status smallint NOT NULL DEFAULT 1,
    submitted_at_utc timestamptz NOT NULL DEFAULT now(),
    decided_at_utc timestamptz,
    decided_by_admin_id bigint REFERENCES users(id) ON DELETE SET NULL,
    CONSTRAINT ck_verification_request_status CHECK (status BETWEEN 1 AND 4)
);

-- Existing installations created before cancellation support need the wider check.
ALTER TABLE verification_requests DROP CONSTRAINT IF EXISTS ck_verification_request_status;
ALTER TABLE verification_requests ADD CONSTRAINT ck_verification_request_status CHECK (status BETWEEN 1 AND 4);

CREATE UNIQUE INDEX IF NOT EXISTS ux_verification_one_open
    ON verification_requests(user_id) WHERE status = 1;
CREATE INDEX IF NOT EXISTS ix_verification_queue
    ON verification_requests(status, submitted_at_utc);
