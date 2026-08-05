-- Slice 1 schema.
--
-- The two partial unique indexes below are named in design/ as the mechanism behind two critical
-- requirements. They are the enforcement, and the application-level checks beside them are a
-- courtesy that makes the common path cheap and the error message good.

CREATE TABLE IF NOT EXISTS quotes (
    id                UUID PRIMARY KEY,
    pickup            TEXT        NOT NULL,
    dropoff           TEXT        NOT NULL,
    total_minor       BIGINT      NOT NULL,
    currency          TEXT        NOT NULL,
    issued_at         TIMESTAMPTZ NOT NULL,
    expires_at        TIMESTAMPTZ NOT NULL,
    consumed_by_trip  UUID        NULL
);

-- pricing/quote#quote-consumed-once, and trip/request#quote-consumed-once: a quote is spent by at
-- most one admitted request. Recorded on the quote rather than the trip; see the residue in
-- design/trip/request.md.
CREATE UNIQUE INDEX IF NOT EXISTS ux_quote_consumer
    ON quotes (consumed_by_trip)
    WHERE consumed_by_trip IS NOT NULL;

CREATE TABLE IF NOT EXISTS trips (
    id                  UUID PRIMARY KEY,
    rider_id            TEXT        NOT NULL,
    assigned_driver_id  TEXT        NULL,
    state               TEXT        NOT NULL,
    fare_minor          BIGINT      NOT NULL,
    currency            TEXT        NOT NULL,
    quote_id            UUID        NOT NULL REFERENCES quotes (id),
    created_at          TIMESTAMPTZ NOT NULL
);

-- trip/request#one-active-trip-per-rider. Two requests arriving together both read "no active
-- trip"; this is what actually holds the line.
--
-- Note the coupling flagged in design/trip/request.md: the predicate depends on the set of terminal
-- states, which trip/lifecycle owns. Adding a state and forgetting to classify it here silently
-- widens or narrows the rule, and nothing currently catches that.
CREATE UNIQUE INDEX IF NOT EXISTS ux_trip_rider_active
    ON trips (rider_id)
    WHERE state NOT IN ('completed', 'cancelled');

CREATE TABLE IF NOT EXISTS trip_transitions (
    id          BIGSERIAL PRIMARY KEY,
    trip_id     UUID        NOT NULL REFERENCES trips (id),
    from_state  TEXT        NOT NULL,
    to_state    TEXT        NOT NULL,
    actor       TEXT        NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_trip_transitions_trip ON trip_transitions (trip_id, id);

CREATE TABLE IF NOT EXISTS offers (
    trip_id     UUID        NOT NULL REFERENCES trips (id),
    driver_id   TEXT        NOT NULL,
    state       TEXT        NOT NULL,
    offered_at  TIMESTAMPTZ NOT NULL,
    expires_at  TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (trip_id, driver_id)
);

CREATE TABLE IF NOT EXISTS drivers (
    id         TEXT PRIMARY KEY,
    available  BOOLEAN NOT NULL,
    near       TEXT    NOT NULL,
    display    TEXT    NOT NULL,
    vehicle    TEXT    NOT NULL,
    position   TEXT    NULL
);
