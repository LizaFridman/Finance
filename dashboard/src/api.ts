// Typed client for the Finance host API. Every call is a live query against the
// host's SQLite database — there is no static export step (spec §0/§14 P4).

const BASE = import.meta.env.VITE_API_BASE ?? "http://localhost:5179";

export type Measures = { income: number; expense: number; netBalance: number };
export type SeriesPoint = { periodStart: string; measures: Measures };
export type LabeledSeries = { key: string; points: SeriesPoint[] };
export type Bucket = { id: string; label: string };
export type Category = { id: string; label: string };
export type Source = { id: string; status: string };

export type YearOverYear = {
  yearStartMonth: number;
  years: number[];
  rows: { ordinalMonth: number; calendarMonth: number; byYear: Record<number, Measures> }[];
  yearTotals: Record<number, Measures>;
};

export type Grain = "Day" | "Month" | "Year";

export type SeriesQuery = {
  from: string;
  to: string;
  grain?: Grain;
  hidePersonal?: boolean;
  nullBucket?: "include" | "exclude";
  bucketId?: string;
  categoryId?: string;
  sourceId?: string;
};

function qs(query: SeriesQuery): string {
  const params = new URLSearchParams();
  params.set("from", query.from);
  params.set("to", query.to);
  if (query.grain) params.set("grain", query.grain);
  if (query.hidePersonal) params.set("hidePersonal", "true");
  if (query.nullBucket) params.set("nullBucket", query.nullBucket);
  if (query.bucketId) params.set("bucketId", query.bucketId);
  if (query.categoryId) params.set("categoryId", query.categoryId);
  if (query.sourceId) params.set("sourceId", query.sourceId);
  return params.toString();
}

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`);
  if (!res.ok) throw new Error(`${res.status} ${res.statusText} for ${path}`);
  return (await res.json()) as T;
}

export const api = {
  health: () => get<{ status: string; database: string; utc: string }>("/api/health"),
  buckets: () => get<Bucket[]>("/api/reference/buckets"),
  categories: () => get<Category[]>("/api/reference/categories"),
  sources: () => get<Source[]>("/api/reference/sources"),

  trend: (q: SeriesQuery) => get<SeriesPoint[]>(`/api/series/trend?${qs(q)}`),
  cumulative: (q: SeriesQuery) => get<SeriesPoint[]>(`/api/series/cumulative?${qs(q)}`),
  rolling12: (q: SeriesQuery) => get<SeriesPoint[]>(`/api/series/rolling12?${qs(q)}`),
  yoy: (years: number[], q: Omit<SeriesQuery, "from" | "to">) =>
    get<YearOverYear>(
      `/api/series/yoy?years=${years.join(",")}&${qs({ ...q, from: "2000-01-01", to: "2100-01-01" })}`,
    ),
  byBucket: (q: SeriesQuery) => get<LabeledSeries[]>(`/api/series/by-bucket?${qs(q)}`),
  byCategory: (q: SeriesQuery) => get<LabeledSeries[]>(`/api/series/by-category?${qs(q)}`),

  assignBucket: (id: string, bucketId: string | null) =>
    fetch(`${BASE}/api/transactions/${id}/bucket`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ bucketId }),
    }),
  confirmCategory: (id: string, categoryId: string, categoryLabel?: string) =>
    fetch(`${BASE}/api/transactions/${id}/category`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ categoryId, categoryLabel }),
    }),
};
