# Phase 0 — Setup runbook

Ordered. Later steps depend on earlier ones (plan §14 Phase 0). None of this is
code the scaffold can run for you — it's machine, network and account setup.

## 1. Decide the host machine  ·  **gates step 2**

One machine runs the ASP.NET Core host + the SQLite file; every other device
reaches it over Tailscale (spec §0). Two options (spec §15):

| Option | Trade-off |
|---|---|
| **This Windows PC** | Nothing to buy. Dashboard is only reachable when the PC is on. |
| **A dedicated always-on box** (e.g. Raspberry Pi, ~$50 once) | App is always up; one more device to maintain, likely Linux. |

The choice decides which credential store applies in step 2 (Windows Credential
Manager vs. a local `.env`). Nothing else in the code changes —
`CredentialStoreFactory` picks automatically.

## 2. Local credential storage

Store Leumi / Cal / Max credentials on the host, in the OS store — **never**
committed, never uploaded to any third-party service (spec §7.2).

- **Windows host:** they go in Windows Credential Manager under `Finance:<source>`.
  Use `WindowsCredentialStore` (a tiny console seeder or `dotnet run` snippet), or
  add them by hand.
- **Linux/Pi host:** copy `scraper/.env.example` to `scraper/.env` and fill it in.
  `.env` is gitignored.

Max needs a third field (`MAX_ID`) as well as username/password (spec §5).

## 3. Install Tailscale

On the host **and** every device that needs access — both of you, any PC or phone.
Free tier: up to 6 users, unlimited devices. No port forwarding, nothing exposed
to the public internet (spec §0). After joining, note the host's tailnet IP
(`100.x.y.z`).

## 4. Create the SQLite database

Nothing to do by hand — the host runs `SqliteDatabase.Bootstrap()` on start-up and
creates `finance.db` (schema + WAL + seed rows) next to the app. To pre-create or
relocate it, set `Finance:DatabasePath` in `appsettings.json` (absolute path ok).

## 5. Run the host

```
dotnet run --project src/Finance.Host
```

It binds `http://0.0.0.0:5179` (see `appsettings.json` — **not** localhost, or the
tailnet can't see it). Verify from another device:

```
curl http://<host-tailnet-ip>:5179/api/health
```

Set the port with `ASPNETCORE_URLS=http://0.0.0.0:<port>` if 5179 is taken.

## 6. Confirm with Akumu

Once his device is on the tailnet he has direct live access via his own browser —
`http://<host-tailnet-ip>:5179` (API) and the dashboard dev/preview server.
Nothing is second-hand.

---

### Not part of Phase 0, but needed later

- Real current income / rent / savings figures for the scenario simulator (Phase 4).
- Whether `כאל 2025.xlsm` is structured, reusable data (Phase 1 check).
- Line-item contents of the `Water` and `גז` PDFs (Phase 1).
- The full missing-data report — see `missing-data-report.md`.
