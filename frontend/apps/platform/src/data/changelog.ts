/**
 * A hand-maintained record of what shipped and where — platform staff only, never surfaced to
 * institution admins or members (this file lives only in the platform app's own bundle). No
 * database, no admin form: entries are added directly here, in the same commit as the change
 * they describe, by whoever (or whichever Claude session) did the work — the same way a
 * CHANGELOG.md is maintained in an open-source repo, just rendered as a page instead of read as
 * a text file. Add new entries at the top; nothing here is ever edited through the UI.
 */

export type ChangelogScope = "Platform" | "Institution" | "Member" | "Marketing";
export type ChangelogType = "Feature" | "Improvement" | "Fix";

export interface ChangelogEntry {
  date: string; // YYYY-MM-DD
  title: string;
  body: string;
  scopes: ChangelogScope[];
  type: ChangelogType;
}

export const CHANGELOG_ENTRIES: ChangelogEntry[] = [
  {
    date: "2026-09-29",
    title: "Platform staff can message an institution's own members",
    body: "A new Notify tab on the institution detail page lets Support/SuperAdmin send a targeted, image-capable notification (in-app, SMS, or email) to a filtered group of one institution's members — the same engagement segments (dormant, never contributed, hasn't given to the open fundraiser) as the institution admins' own Broadcast tool.",
    scopes: ["Platform", "Institution", "Member"],
    type: "Feature",
  },
  {
    date: "2026-09-29",
    title: "Broadcasts can target inactive members and include an image",
    body: "Institution admins can now narrow a broadcast to members who've gone quiet (no login in 60+ days), never made a contribution, or haven't given to a currently open fundraiser — and attach an image, shown in the recipient's in-app notification panel and as a banner in the email.",
    scopes: ["Institution", "Member"],
    type: "Feature",
  },
  {
    date: "2026-09-29",
    title: "Fixed referrals not being tracked from a shared link",
    body: "Referral links never actually worked end to end: the signup page silently ignored the ?ref= code in the URL, so sharing your own link (rather than typing in a specific email to invite) earned no credit at all. Fixed, and added points (10 for a registration, +15 once they become a paying member) with a leaderboard of top referrers.",
    scopes: ["Member"],
    type: "Fix",
  },
  {
    date: "2026-09-29",
    title: "Referral links now show who invited you before signing up",
    body: "Opening a shared referral link shows a real, specific preview first — who sent it, how many of their own graduation year are already active members, and an open fundraiser's live progress — instead of a cold signup form.",
    scopes: ["Member", "Marketing"],
    type: "Feature",
  },
  {
    date: "2026-09-29",
    title: "\"Invite members\" card on the institution dashboard",
    body: "A ready-made, pre-filled WhatsApp message with real numbers for that institution (member count, active fundraisers, upcoming events), one tap from being shared — instead of an admin having to write their own pitch.",
    scopes: ["Institution"],
    type: "Feature",
  },
  {
    date: "2026-09-29",
    title: "Fixed misaligned sections on the member portal landing page",
    body: "Several sections (the live stats band, the closing call-to-action, and others) used a slightly different content width than the rest of the page, so left edges didn't line up when scrolling past them. Standardized on one width across the whole page.",
    scopes: ["Marketing"],
    type: "Fix",
  },
  {
    date: "2026-09-29",
    title: "A proper maintenance page during deploys",
    body: "Nginx now shows a clean, on-brand \"we'll be right back\" page automatically during the brief window a deploy restarts a service, instead of a generic server error screen.",
    scopes: ["Platform"],
    type: "Improvement",
  },
  {
    date: "2026-09-28",
    title: "Fixed a production database connection exhaustion issue",
    body: "Under load, all four backend services could exhaust the database's connection limit at the same time, causing intermittent login and API failures. Fixed by correctly routing startup/migration traffic and everyday app traffic through separate, properly-pooled connections.",
    scopes: ["Platform"],
    type: "Fix",
  },
];
