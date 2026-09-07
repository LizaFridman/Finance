import { useEffect, useMemo, useState } from "react";
import { api, type SeriesQuery } from "./api";
import { BalanceOverTime } from "./components/BalanceOverTime";
import { Breakdown } from "./components/Breakdown";
import { ScenarioSimulator } from "./components/ScenarioSimulator";

const today = new Date();
const isoDate = (d: Date) => d.toISOString().slice(0, 10);

export function App() {
  const [from, setFrom] = useState(isoDate(new Date(today.getFullYear() - 1, today.getMonth(), 1)));
  const [to, setTo] = useState(isoDate(today));
  const [hidePersonal, setHidePersonal] = useState(false);
  const [health, setHealth] = useState<string>("checking…");

  useEffect(() => {
    api.health()
      .then((h) => setHealth(`connected · ${h.database}`))
      .catch((e) => setHealth(`offline · ${e}`));
  }, []);

  const query = useMemo<SeriesQuery>(
    () => ({ from, to, hidePersonal, nullBucket: "include" }),
    [from, to, hidePersonal],
  );

  return (
    <div style={{ maxWidth: 1100, margin: "0 auto", padding: 24, display: "grid", gap: 16 }}>
      <header style={{ display: "flex", gap: 16, alignItems: "baseline", flexWrap: "wrap" }}>
        <h1 style={{ margin: 0, fontSize: 20 }}>Finance</h1>
        <span style={{ color: "#6b7280", fontSize: 13 }}>{health}</span>
        <div style={{ marginLeft: "auto", display: "flex", gap: 8, alignItems: "center" }}>
          <label style={{ fontSize: 13 }}>
            From <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </label>
          <label style={{ fontSize: 13 }}>
            To <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </label>
          <label style={{ fontSize: 13, display: "flex", gap: 4, alignItems: "center" }}>
            <input type="checkbox" checked={hidePersonal}
              onChange={(e) => setHidePersonal(e.target.checked)} />
            Hide personal (Akumu)
          </label>
        </div>
      </header>

      <BalanceOverTime query={query} />
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16 }}>
        <Breakdown by="bucket" query={query} />
        <Breakdown by="category" query={query} />
      </div>
      <ScenarioSimulator query={query} />
    </div>
  );
}
