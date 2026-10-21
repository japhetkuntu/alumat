import Link from "next/link";

/**
 * What happens to a member's details, in plain words. Every line describes how the product already behaves (the member
 * directory is for approved members, email and phone start hidden, the map is opt-in), so this page never promises more
 * than the product does. If one of those behaviours changes, change the line with it.
 */
export const DATA_POINTS = [
  "Only approved members can see the member directory. Visitors to this page cannot.",
  "Your email and phone number stay hidden from other members unless you choose to show them.",
  "You are not on the members' map unless you switch it on.",
  "This page counts members but never lists them. A name appears only where the institution chose to show it, such as its leaders.",
  "You can leave and delete your account whenever you like.",
];

export function DataPromise({ compact = false }: { compact?: boolean }) {
  return (
    <div>
      <ul className={compact ? "space-y-1.5" : "space-y-3"}>
        {DATA_POINTS.slice(0, compact ? 3 : DATA_POINTS.length).map((point) => (
          <li key={point} className={compact ? "flex gap-2 text-[12.5px] leading-snug" : "flex gap-3 text-[16px] leading-relaxed"} style={{ color: "var(--muted-foreground)" }}>
            <span aria-hidden className="mt-[0.55em] h-1.5 w-1.5 shrink-0" style={{ background: "var(--primary)" }} />
            <span>{point}</span>
          </li>
        ))}
      </ul>
      <p className={compact ? "mt-2 text-[12.5px]" : "mt-5 text-[15px]"} style={{ color: "var(--muted-foreground)" }}>
        Read the full <Link href="/privacy" className="font-semibold underline underline-offset-2" style={{ color: "var(--primary)" }}>Privacy Policy</Link> and <Link href="/terms" className="font-semibold underline underline-offset-2" style={{ color: "var(--primary)" }}>Terms</Link>.
      </p>
    </div>
  );
}
