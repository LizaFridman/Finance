import type { CSSProperties } from "react";
import { useEffect, useMemo, useState } from "react";
import {
  CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from "recharts";
import { api, type Grain, type SeriesPoint, type SeriesQuery, type YearOverYear } from "../api";
import { CALENDAR_MONTHS, monthLabel, shekels } from "../format";

type View = "trend" | "cumulative" | "rolling12" | "yoy";

const VIEWS: { id: View; label: string }[] = [
  { id: "trend", label: "Monthly trend" },
  { id: "cumulative", label: "Cumulative within year" },
  { id: "yoy", label: "Year over year" },
  { id: "rolling12", label: "Rolling 12 months" },
];

/**
 * The primary view: income, expense and net balance on one time axis, with the
 * four transforms from the time-series query layer (plan step 6/11). Category and
 * bucket are breakdowns elsewhere — this is time-first.
 */
export function BalanceOverTime({ query }: { query: SeriesQuery }) {
  const [view, setView] = useState<View>("trend");
  const [grain, setGrain] = useState<Grain>("Month");
  const [points, setPoints] = useState<SeriesPoint[]>([]);
  const [yoy, setYoy] = useState<YearOverYear | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setError(null);
    const q = { ...query, grain };
    const load =
      view === "trend" ? api.trend(q)
      : view === "cumulative" ? api.cumulative(q)
      : view === "rolling12" ? api.rolling12(q)
      : null;

    if (load) {
      load.then(setPoints).catch((e) => setError(String(e)));
    } else {
      const thisYear = new Date().getFullYear();
      api.yoy([thisYear - 1, thisYear], query).then(setYoy).catch((e) => setError(String(e)));
    }
  }, [view, grain, query]);

  const lineData = useMemo(
    () => points.map((p) => ({
      period: monthLabel(p.periodStart),
      income: p.measures.income,
      expense: p.measures.expense,
      net: p.measures.netBalance,
    })),
    [points],
  );

  const yoyData = useMemo(() => {
    if (!yoy) return [];
    return yoy.rows.map((row) => {
      const entry: Record<string, number | string> = { month: CALENDAR_MONTHS[row.calendarMonth - 1] };
      for (const year of yoy.years) entry[String(year)] = row.byYear[year]?.netBalance ?? 0;
      return entry;
    });
  }, [yoy]);

  return (
    <section style={card}>
      <header style={{ display: "flex", gap: 12, flexWrap: "wrap", alignItems: "center" }}>
        <h2 style={{ margin: 0, fontSize: 16 }}>Income vs expense vs balance</h2>
        <div style={{ display: "flex", gap: 4 }}>
          {VIEWS.map((v) => (
            <button key={v.id} onClick={() => setView(v.id)}
              style={v.id === view ? tabActive : tab}>{v.label}</button>
          ))}
        </div>
        {view !== "yoy" && (
          <select value={grain} onChange={(e) => setGrain(e.target.value as Grain)}>
            <option value="Day">Day</option>
            <option value="Month">Month</option>
            <option value="Year">Year</option>
          </select>
        )}
      </header>

      {error && <p style={{ color: "crimson" }}>{error}</p>}

      <ResponsiveContainer width="100%" height={320}>
        {view === "yoy" ? (
          <LineChart data={yoyData} margin={{ top: 16, right: 16, bottom: 0, left: 0 }}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="month" />
            <YAxis tickFormatter={(n) => shekels(Number(n))} width={90} />
            <Tooltip formatter={(n) => shekels(Number(n))} />
            <Legend />
            {(yoy?.years ?? []).map((year, i) => (
              <Line key={year} type="monotone" dataKey={String(year)}
                stroke={["#2563eb", "#16a34a", "#dc2626"][i % 3]} strokeWidth={2} dot={false} />
            ))}
          </LineChart>
        ) : (
          <LineChart data={lineData} margin={{ top: 16, right: 16, bottom: 0, left: 0 }}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="period" />
            <YAxis tickFormatter={(n) => shekels(Number(n))} width={90} />
            <Tooltip formatter={(n) => shekels(Number(n))} />
            <Legend />
            <Line type="monotone" dataKey="income" stroke="#16a34a" strokeWidth={2} dot={false} />
            <Line type="monotone" dataKey="expense" stroke="#dc2626" strokeWidth={2} dot={false} />
            <Line type="monotone" dataKey="net" stroke="#2563eb" strokeWidth={2} dot={false} />
          </LineChart>
        )}
      </ResponsiveContainer>
    </section>
  );
}

const card: CSSProperties = {
  background: "#fff", border: "1px solid #e5e7eb", borderRadius: 10, padding: 16,
};
const tab: CSSProperties = {
  border: "1px solid #d1d5db", background: "#fff", borderRadius: 6, padding: "4px 8px", cursor: "pointer",
};
const tabActive: CSSProperties = { ...tab, background: "#111827", color: "#fff", borderColor: "#111827" };
