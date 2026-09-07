import type { CSSProperties } from "react";
import { useEffect, useMemo, useState } from "react";
import { api, type SeriesQuery } from "../api";
import { shekels } from "../format";

/**
 * Projects the balance forward from the live trend (spec §14 P4). Every input is
 * entered by the user — nothing is seeded from the old תקציב sheet.
 */
export function ScenarioSimulator({ query }: { query: SeriesQuery }) {
  const [monthlyIncome, setMonthlyIncome] = useState(0);
  const [monthlyRent, setMonthlyRent] = useState(0);
  const [monthlySavings, setMonthlySavings] = useState(0);
  const [oneOff, setOneOff] = useState(0);
  const [months, setMonths] = useState(12);
  const [observedNet, setObservedNet] = useState<number | null>(null);

  useEffect(() => {
    api.trend({ ...query, grain: "Month" })
      .then((pts) => {
        if (pts.length === 0) return setObservedNet(null);
        const recent = pts.slice(-3);
        setObservedNet(recent.reduce((s, p) => s + p.measures.netBalance, 0) / recent.length);
      })
      .catch(() => setObservedNet(null));
  }, [query]);

  const projection = useMemo(() => {
    const baseline = observedNet ?? 0;
    const monthlyDelta = monthlyIncome - monthlyRent - monthlySavings;
    const out: { month: number; balance: number }[] = [];
    let balance = -oneOff;
    for (let m = 1; m <= months; m++) {
      balance += baseline + monthlyDelta;
      out.push({ month: m, balance });
    }
    return out;
  }, [observedNet, monthlyIncome, monthlyRent, monthlySavings, oneOff, months]);

  return (
    <section style={card}>
      <h2 style={{ margin: "0 0 8px", fontSize: 16 }}>Scenario simulator</h2>
      <p style={{ margin: "0 0 8px", color: "#6b7280", fontSize: 13 }}>
        Recent observed net / month: {observedNet == null ? "—" : shekels(observedNet)}
      </p>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
        <Field label="Monthly income" value={monthlyIncome} onChange={setMonthlyIncome} />
        <Field label="Monthly rent" value={monthlyRent} onChange={setMonthlyRent} />
        <Field label="Monthly savings" value={monthlySavings} onChange={setMonthlySavings} />
        <Field label="One-off cost now" value={oneOff} onChange={setOneOff} />
        <Field label="Months to project" value={months} onChange={setMonths} />
      </div>
      <p style={{ marginTop: 12, fontWeight: 600 }}>
        Projected balance after {months} months:{" "}
        {shekels(projection.at(-1)?.balance ?? 0)}
      </p>
    </section>
  );
}

function Field({ label, value, onChange }: {
  label: string; value: number; onChange: (n: number) => void;
}) {
  return (
    <label style={{ display: "flex", flexDirection: "column", fontSize: 13, gap: 2 }}>
      {label}
      <input type="number" value={value}
        onChange={(e) => onChange(Number(e.target.value) || 0)} />
    </label>
  );
}

const card: CSSProperties = {
  background: "#fff", border: "1px solid #e5e7eb", borderRadius: 10, padding: 16,
};
