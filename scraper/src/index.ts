/**
 * Local scraper wrapper. Pulls transactions for one source and writes a
 * normalized JSON array into `Raw Data Feed/<source>/<timestamp>.json`, which the
 * C# `ScraperJsonParser` reads verbatim.
 *
 * Runs on the host machine only. Credentials come from `process.env` (a local
 * `.env`, or injected by the C# orchestrator) and are never written anywhere but
 * the private feed folder (spec §7.2 / §7.3).
 *
 *   npm run scrape -- --source leumi --from 2025-01-01 [--to 2025-12-31]
 *
 * `combineInstallments: true` collapses an installment series to its first
 * installment carrying the full amount (spec §8).
 */
import { mkdirSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { createScraper, CompanyTypes } from "israeli-bank-scrapers";

type NormalizedTransaction = {
  date: string; // YYYY-MM-DD (charge date)
  amount: number; // signed shekels: negative = outflow
  merchantRaw: string; // exact scraped description, original language
  nativeId?: string;
  installments?: { number: number; total: number };
};

const SOURCES: Record<string, { company: CompanyTypes; credentials: () => Record<string, string> }> = {
  leumi: {
    company: CompanyTypes.leumi,
    credentials: () => ({
      username: required("LEUMI_USERNAME"),
      password: required("LEUMI_PASSWORD"),
    }),
  },
  cal: {
    company: CompanyTypes.visaCal,
    credentials: () => ({
      username: required("CAL_USERNAME"),
      password: required("CAL_PASSWORD"),
    }),
  },
  max: {
    company: CompanyTypes.max,
    credentials: () => ({
      username: required("MAX_USERNAME"),
      password: required("MAX_PASSWORD"),
      id: required("MAX_ID"),
    }),
  },
};

function required(name: string): string {
  const value = process.env[name];
  if (!value) throw new Error(`Missing required env var ${name}`);
  return value;
}

function arg(flag: string): string | undefined {
  const i = process.argv.indexOf(flag);
  return i >= 0 ? process.argv[i + 1] : undefined;
}

async function main(): Promise<void> {
  const sourceId = arg("--source");
  if (!sourceId || !(sourceId in SOURCES)) {
    throw new Error(`--source must be one of: ${Object.keys(SOURCES).join(", ")}`);
  }
  const from = arg("--from");
  if (!from) throw new Error("--from YYYY-MM-DD is required");
  const startDate = new Date(from);
  const source = SOURCES[sourceId];

  const scraper = createScraper({
    companyId: source.company,
    startDate,
    combineInstallments: true, // spec §8 — keep only the first installment of a series
    additionalTransactionInformation: true,
    showBrowser: false,
  });

  const result = await scraper.scrape(source.credentials());
  if (!result.success) {
    throw new Error(`scrape failed: ${result.errorType ?? ""} ${result.errorMessage ?? ""}`.trim());
  }

  const toArg = arg("--to");
  const cutoff = toArg ? new Date(toArg) : undefined;

  const normalized: NormalizedTransaction[] = [];
  for (const account of result.accounts ?? []) {
    for (const txn of account.txns) {
      const date = new Date(txn.date);
      if (date < startDate) continue;
      if (cutoff && date > cutoff) continue;
      normalized.push({
        date: date.toISOString().slice(0, 10),
        amount: txn.chargedAmount, // signed, account currency
        merchantRaw: (txn.description ?? "").trim(),
        nativeId: txn.identifier != null ? String(txn.identifier) : undefined,
        installments: txn.installments
          ? { number: txn.installments.number, total: txn.installments.total }
          : undefined,
      });
    }
  }

  const feedRoot = resolve(process.env.RAW_DATA_FEED ?? join("..", "Raw Data Feed"));
  const outDir = join(feedRoot, sourceId);
  mkdirSync(outDir, { recursive: true });
  const outFile = join(outDir, `${new Date().toISOString().replace(/[:.]/g, "-")}.json`);
  writeFileSync(outFile, JSON.stringify(normalized, null, 2), "utf8");

  console.log(`wrote ${normalized.length} transactions -> ${outFile}`);
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
