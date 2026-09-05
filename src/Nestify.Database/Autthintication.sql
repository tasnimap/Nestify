DROP TABLE IF EXISTS refresh_tokens CASCADE;
DROP TABLE IF EXISTS user_roles CASCADE;
DROP TABLE IF EXISTS users CASCADE;
DROP TABLE IF EXISTS roles CASCADE;




CREATE TABLE roles (
    id          smallint     PRIMARY KEY,
    code        varchar(40)  NOT NULL,
    name        varchar(60)  NOT NULL,
    description varchar(200)
);

CREATE UNIQUE INDEX ux_roles_code ON roles (code);

INSERT INTO roles (id, code, name, description) VALUES
    (1, 'User',           'User',            'Normal account, given at registration'),
    (2, 'DomesticHelper', 'Domestic Helper', 'Helper account, added on top of User'),
    (3, 'Admin',          'Administrator',   'Manages verification, reports and bans');


-- ========================= Users =========================

CREATE TABLE users (
    id             bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    full_name      varchar(120) NOT NULL,
    email          varchar(256) NOT NULL,          -- always stored lower-case by the API
    password_hash  text         NOT NULL,
    phone_number   varchar(20)  NOT NULL,
    account_type   smallint     NOT NULL DEFAULT 1,   -- 1 = User, 2 = DomesticHelp
    created_at_utc timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_users_account_type CHECK (account_type BETWEEN 1 AND 3)
);



CREATE UNIQUE INDEX ux_users_email ON users (email);


CREATE TABLE user_roles (
    user_id        bigint      NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    role_id        smallint    NOT NULL REFERENCES roles (id) ON DELETE RESTRICT,
    granted_at_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, role_id)
);

CREATE INDEX ix_user_roles_role ON user_roles (role_id);


-- ========================= Refresh tokens (login sessions) =========================

CREATE TABLE refresh_tokens (
    id                   bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id              bigint      NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    token_hash           bytea       NOT NULL,       -- SHA-256 of the token, 32 bytes
    family_id            bigint,                     -- NULL on the first token of a login; children point at that root id
    expires_at_utc       timestamptz NOT NULL,
    created_at_utc       timestamptz NOT NULL DEFAULT now(),
    revoked_at_utc       timestamptz,
    replaced_by_token_id bigint      REFERENCES refresh_tokens (id),
    created_by_ip        inet,

    CONSTRAINT ck_refresh_token_hash_len CHECK (octet_length(token_hash) = 32)
);

CREATE UNIQUE INDEX ux_refresh_token_hash  ON refresh_tokens (token_hash);
CREATE INDEX        ix_refresh_user_active ON refresh_tokens (user_id) WHERE revoked_at_utc IS NULL;
CREATE INDEX        ix_refresh_family      ON refresh_tokens (family_id);


select * from users