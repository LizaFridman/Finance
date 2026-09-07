# dashboard/

React + Recharts view over the Finance host API. It **fetches live** from the
ASP.NET Core host on every render — there is no static export file, because one
would go stale the moment a second device is looking at it (spec §0 / §14 P4).

## Status in this scaffold

Source is written; **not built or run** — Node.js is not yet installed. Once it is:

```bash
cd dashboard
npm install
npm run typecheck          # tsc --noEmit, should be clean
VITE_API_BASE=http://<host-tailnet-ip>:5179 npm run dev
```

## Layout

- `BalanceOverTime` — the primary view: income / expense / net balance on one time
  axis, with the four transforms (monthly trend, cumulative within year, year over
  year, rolling 12 months) and a Day/Month/Year grain switch.
- `Breakdown` — expense **per bucket / per category over time** (stacked bars). The
  bucket view shows the `needs_bucket_assignment` series for rows Phase 3 hasn't
  reached yet.
- `ScenarioSimulator` — projects the balance forward from the recent trend; all
  inputs are typed in by hand, nothing seeded from תקציב.
- `HidePersonalToggle` is the checkbox in `App.tsx` — it just flips the
  `hidePersonal` query param. No auth; Tailscale is the boundary (spec §0).
