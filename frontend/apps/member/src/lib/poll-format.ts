/** "12 Nov, 9:00 am", in the member's own time zone. */
export function fmtWhen(iso: string): string {
  const d = new Date(iso);
  return isNaN(d.getTime()) ? "" : d.toLocaleString("en-GB", { day: "numeric", month: "short", hour: "numeric", minute: "2-digit", hour12: true });
}

/** How long is left, in the plainest words: "Closes in 2 days", "Closes in 3 hours", "Closes in 12 minutes". */
export function timeLeft(closesIso: string, now = Date.now()): string {
  const ms = new Date(closesIso).getTime() - now;
  if (isNaN(ms)) return "";
  if (ms <= 0) return "Closing now";
  const mins = Math.round(ms / 60_000);
  if (mins < 60) return `Closes in ${Math.max(1, mins)} ${mins === 1 ? "minute" : "minutes"}`;
  const hours = Math.round(mins / 60);
  if (hours < 48) return `Closes in ${hours} ${hours === 1 ? "hour" : "hours"}`;
  const days = Math.round(hours / 24);
  return `Closes in ${days} days, ${fmtWhen(closesIso)}`;
}
