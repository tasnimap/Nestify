CREATE TABLE IF NOT EXISTS home_capacity (
    home_id        bigint      PRIMARY KEY REFERENCES homes (id) ON DELETE CASCADE,
    max_occupants  smallint    NOT NULL DEFAULT 4,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_home_capacity_min CHECK (max_occupants >= 1)
);

INSERT INTO home_capacity (home_id)
SELECT id
FROM homes
ON CONFLICT (home_id) DO NOTHING;

SELECT h.id, h.name, c.max_occupants
FROM homes h
LEFT JOIN home_capacity c ON c.home_id = h.id;
