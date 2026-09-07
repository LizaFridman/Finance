import type { CSSProperties } from "react";
import { useEffect, useState } from "react";
import {
  Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from "recharts";
import { api, type LabeledSeries, type SeriesQuery } from "../api";
import { monthLabel, shekels } from "../format";

/**
 * Category / bucket breakdowns rendered as time series (stacked bars per period),
 * not standalone pie totals — still time-first. The bucket view surfaces the
 * synthetic `needs_bucket_assignment` series for rows Phase 3 hasn't touched yet.
 */
export function Breakdown({ by, query }: { by: "bucket" | "category"; query: SeriesQuery }) {
  const [series, setSeries] = useState<LabeledSeries[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setError(null);
    const load = by === "bucket" ? api.byBucket(query) : api.byCategory(query);
    load.then(setSeries).catch((e) => setError(String(e)));
  }, [by, query]);

  const periods = series[0]?.points.map((p) => p.periodStart) ?? [];
  const rows = periods.map((period, i) => {
    const entry: Record<string, number | string> = { period: monthLabel(period) };
    for (const s of series) entry[s.key] = s.points[i]?.measures.expense ?? 0;
    return entry;
  });

  const palette = ["#2563eb", "#16a34a", "#dc2626", "#d97706", "#7c3aed", "#0891b2", "#64748b"];

  return (
    <section style={card}>
      <h2 style={{ margin: "0 0 8px", fontSize: 16 }}>
        Expense by {by} over time
      </h2>
      {error && <p style={{ color: "crimson" }}>{error}</p>}
      <ResponsiveContainer width="100%" height={260}>
        <BarChart data={rows} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="period" />
          <YAxis tickFormatter={(n) => shekels(Number(n))} width={90} />
          <Tooltip formatter={(n) => shekels(Number(n))} />
          <Legend />
          {series.map((s, i) => (
            <Bar key={s.key} dataKey={s.key} stackId="x" fill={palette[i % palette.length]} />
          ))}
        </BarChart>
      </ResponsiveContainer>
    </section>
  );
}

const card: CSSProperties = {
  background: "#fff", border: "1px solid #e5e7eb", borderRadius: 10, padding: 16,
};
