-- payments/capture.
--
-- The outbox and the unique index are both named in design/payments/capture.md as the mechanism
-- behind critical requirements. They are the enforcement; the application-level idempotency key is
-- a courtesy that makes the common path cheap and the error message good.

CREATE TABLE IF NOT EXISTS capture_intents (
    trip_id       UUID PRIMARY KEY,
    amount_minor  BIGINT      NOT NULL,
    currency      TEXT        NOT NULL,
    written_at    TIMESTAMPTZ NOT NULL,
    dispatched_at TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS captures (
    id                UUID PRIMARY KEY,
    trip_id           UUID        NOT NULL,
    amount_minor      BIGINT      NOT NULL,
    currency          TEXT        NOT NULL,
    adjustment_reason TEXT        NULL,
    voided            BOOLEAN     NOT NULL DEFAULT FALSE,
    captured_at       TIMESTAMPTZ NOT NULL
);

-- payments/capture#captured-once. Two workers processing the same completion both read "no
-- capture"; this is what actually holds the line, against paths that did not exist when it was
-- written. Partial, because a voided capture stays as a row so that a disputed trip's history is
-- legible — see the residue in design/payments/capture.md.
CREATE UNIQUE INDEX IF NOT EXISTS ux_capture_trip
    ON captures (trip_id)
    WHERE NOT voided;

CREATE TABLE IF NOT EXISTS capture_failures (
    id          BIGSERIAL PRIMARY KEY,
    trip_id     UUID        NOT NULL,
    reason      TEXT        NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL
);
