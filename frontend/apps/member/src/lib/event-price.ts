import { formatCurrency } from "@alumni/ui";
import type { AlumniEvent } from "@/types";

/** What to show for an event's price: "Free", "GH₵150", or "From GH₵100" when members can choose between several prices. */
export function eventPriceLabel(e: Pick<AlumniEvent, "isTicketed" | "ticketPrice" | "priceOptions">): string {
  if (!e.isTicketed || !e.ticketPrice) return "Free";
  const several = (e.priceOptions ?? []).length > 1;
  return `${several ? "From " : ""}${formatCurrency(e.ticketPrice)}`;
}
