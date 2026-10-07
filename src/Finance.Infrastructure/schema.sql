-- Finance tracker schema. Applied by Finance.Data.SqliteDatabase.Bootstrap().
-- All statements are idempotent (CREATE ... IF NOT EXISTS / INSERT OR IGNORE) so
-- bootstrapping an existing database is a safe no-op. Plan step 2 / spec §4.2.
--
-- SQLite type notes:
--   * No native DATE type -> dates stored as TEXT in ISO 'YYYY-MM-DD'.
--   * No exact DECIMAL -> `amount` is a SIGNED INTEGER of minor units (agorot).
--     Sign convention (flagged decision 1 / spec §3):
--         amount < 0  => outflow  (expense)
--         amount > 0  => inflow   (income)
--     net balance = SUM(amount); expense = SUM(-amount) WHERE amount < 0;
--     income = SUM(amount) WHERE amount > 0. Kept integer so GROUP BY SUM over
--     the time series stays exact; the C# boundary converts to/from decimal.

-- ---------------------------------------------------------------------------
-- Reference tables (spec §3: sets that can grow are data, never hardcoded enums)
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS buckets (
  id    TEXT PRIMARY KEY,
  label TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS categories (
  id    TEXT PRIMARY KEY,
  label TEXT NOT NULL
  -- Starts empty. Rows are inserted as Layer 2 review discovers categories
  -- (spec §10); the count is never assumed anywhere.
);

CREATE TABLE IF NOT EXISTS sources (
  id     TEXT PRIMARY KEY,
  status TEXT NOT NULL CHECK (status IN ('active', 'dormant'))
  -- The source registry (spec §6). Adding a source = one row; retiring one =
  -- a status flip. History rows keep their source_id regardless.
);

-- ---------------------------------------------------------------------------
-- Categorization Layer 1 (spec §10): merchant dictionary.
-- Empty at start; every row is written back from a confirmed Layer 2 decision,
-- keyed on the exact scraped merchant string (Hebrew, as it appears in source
-- data) -- never on free-text notes.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS merchant_dictionary (
  merchant_raw TEXT PRIMARY KEY,
  category_id  TEXT NOT NULL REFERENCES categories(id),
  confirmed_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- ---------------------------------------------------------------------------
-- Transactions (spec §4.2). One flat table, one nullable bucket_id column --
-- reclassifying is one UPDATE, never a file move.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS transactions (
  id             TEXT PRIMARY KEY,               -- stable idempotency key (spec §7.1)
  source_id      TEXT NOT NULL REFERENCES sources(id),
  date           TEXT NOT NULL,                  -- charge date, ISO 'YYYY-MM-DD' (spec §8)
  amount         INTEGER NOT NULL,               -- signed agorot; see sign convention above
  merchant_raw   TEXT NOT NULL,                  -- as scraped/entered, original language
  category_id    TEXT REFERENCES categories(id), -- NULL until Layer 1/2 assigns one (spec §10)
  bucket_id      TEXT REFERENCES buckets(id),    -- NULL until Phase 3 assigns one -- deliberately
                                                 -- nullable: ingestion (Phase 1) runs before
                                                 -- bucket assignment (Phase 3). Every aggregate
                                                 -- query must decide explicitly how to treat
                                                 -- bucket_id IS NULL (spec §4.2 / plan step 6).
  status         TEXT NOT NULL DEFAULT 'needs_review',
  effective_date TEXT,                           -- optional override of `date` for reporting
  ingested_at    TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS ix_transactions_date        ON transactions(date);
CREATE INDEX IF NOT EXISTS ix_transactions_source      ON transactions(source_id);
CREATE INDEX IF NOT EXISTS ix_transactions_bucket      ON transactions(bucket_id);
CREATE INDEX IF NOT EXISTS ix_transactions_category    ON transactions(category_id);
CREATE INDEX IF NOT EXISTS ix_transactions_status      ON transactions(status);

-- ---------------------------------------------------------------------------
-- Config: small key/value store for values that can change but aren't per-row.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS config (
  key   TEXT PRIMARY KEY,
  value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS schema_version (
  version    INTEGER NOT NULL,
  applied_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- ---------------------------------------------------------------------------
-- Seed data: only the things known to exist today (spec §5 / §6). All INSERT
-- OR IGNORE so re-running never duplicates or overwrites edited rows.
-- ---------------------------------------------------------------------------

INSERT OR IGNORE INTO buckets (id, label) VALUES
  ('personal_liza',  'Personal — Liza'),
  ('personal_akumu', 'Personal — Akumu'),
  ('shared',         'Shared');

INSERT OR IGNORE INTO sources (id, status) VALUES
  ('leumi',       'active'),
  ('cal',         'active'),
  ('max',         'dormant'),   -- registered dormant; 2025 history still enters the baseline (spec §6)
  ('water',       'active'),
  ('electricity', 'active'),
  ('gas',         'active'),
  ('vaad',        'active'),
  ('partner',     'active');

-- Year boundary for yearly rollups and the cumulative-within-year reset.
-- Configurable (spec §3): default 1 = January. Change with a single UPDATE.
INSERT OR IGNORE INTO config (key, value) VALUES ('year_start_month', '1');
