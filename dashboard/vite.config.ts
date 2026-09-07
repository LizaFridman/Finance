import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The dashboard fetches live from the ASP.NET Core host's API (spec §13) — it
// never reads a static export file. Point VITE_API_BASE at the host's tailnet
// address in production.
export default defineConfig({
  plugins: [react()],
  server: { port: 5173 },
});
