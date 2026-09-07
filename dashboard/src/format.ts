export const shekels = (n: number): string =>
  new Intl.NumberFormat("he-IL", { style: "currency", currency: "ILS", maximumFractionDigits: 0 }).format(n);

export const monthLabel = (iso: string): string =>
  new Date(iso).toLocaleDateString("en-GB", { month: "short", year: "2-digit" });

export const CALENDAR_MONTHS = [
  "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec",
];
