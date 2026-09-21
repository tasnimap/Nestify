-- Account verification for users and domestic helpers.
-- Apply after Autthintication.sql and User_Additional_profile_info.sql.
--
-- A user proves identity with an NID or birth certificate. A student also
-- attaches a student ID and a job holder an employee ID; anyone else sends
-- only the identity document. The user pays the verification fee through the
-- (fake) bKash portal before the application is sent. An admin then opens the
-- request, sees the documents next to the profile, and approves or rejects it
-- with a reason.

-- Older installations may still be missing these profile columns.
ALTER TABLE user_additional_profile_info
    ADD COLUMN IF NOT EXISTS organization_name varchar(160),
    ADD COLUMN IF NOT EXISTS gender            smallint,
    ADD COLUMN IF NOT EXISTS date_of_birth     date,
    ADD COLUMN IF NOT EXISTS is_smoker         boolean,
    ADD COLUMN IF NOT EXISTS is_drinker        boolean,
    ADD COLUMN IF NOT EXISTS is_verified       boolean NOT NULL DEFAULT false;

-- Smoking and drinking are a plain yes/no now; "prefer not to say" is gone.
UPDATE user_additional_profile_info SET is_smoker  = false WHERE is_smoker  IS NULL;
UPDATE user_additional_profile_info SET is_drinker = false WHERE is_drinker IS NULL;
ALTER TABLE user_additional_profile_info
    ALTER COLUMN is_smoker  SET NOT NULL,
    ALTER COLUMN is_smoker  SET DEFAULT false,
    ALTER COLUMN is_drinker SET NOT NULL,
    ALTER COLUMN is_drinker SET DEFAULT false;


DROP TABLE IF EXISTS verification_payments  CASCADE;
DROP TABLE IF EXISTS verification_documents CASCADE;
DROP TABLE IF EXISTS verification_requests  CASCADE;


-- ========================= Verification requests =========================
-- subject_type: 1 User, 2 DomesticHelper.
-- status: 1 Pending, 2 Approved, 3 Rejected, 4 Cancelled (withdrawn by the applicant).
-- rejection_reason is required when an admin rejects; it is only stored for now.

CREATE TABLE verification_requests (
    id                  bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id             bigint       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    subject_type        smallint     NOT NULL DEFAULT 1,
    status              smallint     NOT NULL DEFAULT 1,
    rejection_reason    varchar(500),
    submitted_at_utc    timestamptz  NOT NULL DEFAULT now(),
    decided_at_utc      timestamptz,
    decided_by_admin_id bigint       REFERENCES users (id) ON DELETE SET NULL,

    CONSTRAINT ck_verification_subject   CHECK (subject_type BETWEEN 1 AND 2),
    CONSTRAINT ck_verification_status    CHECK (status BETWEEN 1 AND 4),
    CONSTRAINT ck_verification_rejection CHECK (status <> 3 OR rejection_reason IS NOT NULL)
);

CREATE UNIQUE INDEX ux_verification_one_open ON verification_requests (user_id) WHERE status = 1;
CREATE INDEX        ix_verification_queue    ON verification_requests (status, submitted_at_utc);


-- ========================= Verification documents =========================
-- One row per uploaded photo. Every request has one identity document
-- (NID or birth certificate); students and job holders add a second one.
-- document_type: 1 NID, 2 Birth certificate, 3 Student ID, 4 Employee ID, 5 Passport.

CREATE TABLE verification_documents (
    id                      bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    verification_request_id bigint       NOT NULL REFERENCES verification_requests (id) ON DELETE CASCADE,
    document_type           smallint     NOT NULL,
    document_url            text         NOT NULL,
    original_file_name      varchar(160) NOT NULL,
    uploaded_at_utc         timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_vdoc_type CHECK (document_type BETWEEN 1 AND 5)
);

CREATE UNIQUE INDEX ux_vdoc_request_type ON verification_documents (verification_request_id, document_type);


-- ========================= Verification payments =========================
-- The fake bKash payment made right before an application is sent. The fee
-- code and amount are copied from fee_settings (Admin.sql) at payment time so
-- the row still makes sense if the admin changes the fee later. The PIN is
-- never stored. verification_request_id is filled in once the application
-- that used this payment is created.

CREATE TABLE verification_payments (
    id                      bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id                 bigint        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    verification_request_id bigint        REFERENCES verification_requests (id) ON DELETE SET NULL,
    fee_code                varchar(40)   NOT NULL,
    amount_bdt              numeric(10,2) NOT NULL,
    bkash_number            varchar(20)   NOT NULL,
    transaction_id          varchar(20)   NOT NULL,
    paid_at_utc             timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_vpay_amount CHECK (amount_bdt >= 0)
);

CREATE UNIQUE INDEX ux_vpay_transaction ON verification_payments (transaction_id);
CREATE UNIQUE INDEX ux_vpay_request     ON verification_payments (verification_request_id) WHERE verification_request_id IS NOT NULL;
CREATE INDEX        ix_vpay_user        ON verification_payments (user_id, paid_at_utc DESC);
