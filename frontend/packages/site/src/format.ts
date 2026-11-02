export const CURRENCY = "GH₵";
export const money = (n: number, symbol = CURRENCY) => `${symbol} ${Number.isInteger(n) ? n.toLocaleString("en-GB") : n.toLocaleString("en-GB", { minimumFractionDigits: 2 })}`;
export const dayOf = (iso: string) => new Date(iso).toLocaleDateString("en-GB", { day: "2-digit" });
export const monthOf = (iso: string) => new Date(iso).toLocaleDateString("en-GB", { month: "short" });
export const dateOf = (iso?: string | null) => (iso ? new Date(iso).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" }) : "");
export const pct = (raised: number, goal: number) => (goal > 0 ? Math.min(100, Math.round((raised / goal) * 100)) : 0);
export const group = (n: number) => n.toLocaleString("en-GB");
