/**
 * Ready-to-send wording for sharing an item into a chat (WhatsApp and similar). Each builder returns the text
 * that goes ABOVE the link; ShareLinkButton appends the link itself. Wording is deliberately generic
 * ("our community", "members"): the same text has to suit a school, a church or an association.
 */

function when(iso?: string | Date | null, withTime = false) {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const day = d.toLocaleDateString("en-GB", { weekday: "long", day: "numeric", month: "long" });
  return withTime ? `${day}, ${d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" })}` : day;
}

const money = (n: number) => `GHS ${Math.round(n).toLocaleString("en-GH")}`;

/** Joins lines, dropping missing ones; "" is a deliberate blank line (never doubled, never first or last). */
const lines = (...parts: (string | false | null | undefined)[]) =>
  parts
    .filter((p): p is string => typeof p === "string")
    .filter((p, i, all) => p !== "" || (i > 0 && all[i - 1] !== "" && i < all.length - 1))
    .join("\n");

function clip(text: string | null | undefined, max = 140) {
  if (!text) return "";
  const t = text.replace(/\s+/g, " ").trim();
  return t.length > max ? `${t.slice(0, max - 1).trimEnd()}…` : t;
}

export const shareMessages = {
  event: (e: { title: string; startDate?: string | null; venue?: string | null; description?: string | null }) =>
    lines(e.title, when(e.startDate, true), e.venue, "", clip(e.description), "", "View the event and RSVP:"),

  job: (j: { title: string; company?: string | null; location?: string | null; type?: string | null }) =>
    lines("New opportunity in our community", "", j.title, j.company, [j.location, j.type].filter(Boolean).join(" · "), "", "View the opportunity:"),

  news: (n: { title: string; excerpt?: string | null }) =>
    lines(n.title, "", clip(n.excerpt), "", "Read more:"),

  album: (a: { title: string; photoCount?: number | null }) =>
    lines(a.title, a.photoCount ? `${a.photoCount} photo${a.photoCount === 1 ? "" : "s"}` : null, "", "See the album:"),

  fundraiser: (c: { title: string; target?: number | null; raised?: number | null; contributors?: number | null; description?: string | null }) =>
    lines(
      c.title,
      c.target ? `Goal: ${money(c.target)}` : null,
      c.raised ? `Raised: ${money(c.raised)}` : null,
      c.contributors ? `${c.contributors} member${c.contributors === 1 ? " has" : "s have"} contributed.` : null,
      !c.target && !c.raised ? clip(c.description) : null,
      "",
      "Support the fundraiser:",
    ),

  dues: (d: { title: string; amount?: number | null; deadline?: string | null }) =>
    lines(d.title, d.amount ? `Amount: ${money(d.amount)}` : null, d.deadline ? `Pay by ${when(d.deadline)}` : null, "", "Pay your membership dues:"),

  service: (s: { name: string; description?: string | null }) => lines(s.name, clip(s.description), "", "Request this service:"),

  business: (b: { name: string; description?: string | null }) =>
    lines("From a member of our community", "", b.name, clip(b.description), "", "See the business:"),

  community: (c: { name: string; description?: string | null }) => lines(`Join ${c.name}`, clip(c.description), "", "Join the group:"),

  resource: (r: { title: string }) => lines(r.title, "", "Open the resource:"),
};
