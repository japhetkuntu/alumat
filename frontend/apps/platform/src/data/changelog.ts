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
    date: "2026-09-30",
    title: "Refreshed the AlumUnion logo across every portal",
    body: "A bolder, more visible mark and wordmark, with the header/footer logo sized up — replacing the old thin-lined version everywhere: favicons, home-screen icons, and both portal headers.",
    scopes: ["Platform", "Institution", "Member", "Marketing"],
    type: "Improvement",
  },
  {
    date: "2026-09-30",
    title: "Reworked the marketing site's visual design",
    body: "Learned from other institution marketing sites to make the public site feel less templated: varied section layouts, a dedicated stylesheet for the marketing pages, and refreshed footer and FAQ content.",
    scopes: ["Marketing"],
    type: "Improvement",
  },
  {
    date: "2026-09-30",
    title: "Fixed inconsistent font sizes on the marketing site",
    body: "Text sizing across the footer and marketing sections now follows one consistent scale instead of varying section to section.",
    scopes: ["Marketing"],
    type: "Fix",
  },
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
  {
    date: "2026-09-28",
    title: "Redesigned the member portal's public landing page",
    body: "A ground-up rewrite: a live community pulse strip, real news/events/spotlight sections instead of stock content, zig-zag use-case stories, and dynamic image fitting — replacing the old generic template feel.",
    scopes: ["Marketing"],
    type: "Improvement",
  },
  {
    date: "2026-09-29",
    title: "Installed app now shows the institution's own name and icon",
    body: "Installing the member or institution portal to a phone's home screen now shows that institution's own name and icon instead of a generic \"Member Portal\"/\"Institution Portal\" label.",
    scopes: ["Institution", "Member"],
    type: "Feature",
  },
  {
    date: "2026-09-27",
    title: "Platform admins can set activation targets and assign tasks",
    body: "SuperAdmins can set time-boxed activation targets (live institutions, members, payment volume) and assign tasks to platform staff, with alerts for overdue/stalled work and CSV import for bulk onboarding leads.",
    scopes: ["Platform"],
    type: "Feature",
  },
  {
    date: "2026-09-27",
    title: "Added proper home-screen icons for installing as an app",
    body: "Favicons, Apple touch icon, and Android/PWA icons (including maskable variants) for both the member and institution portals — previously missing, so an installed icon looked broken or generic.",
    scopes: ["Institution", "Member"],
    type: "Fix",
  },
  {
    date: "2026-09-27",
    title: "Fixed a font-loading failure affecting the marketing site's build",
    body: "Google Fonts was being blocked at build time on the hosting provider; brand fonts are now self-hosted instead, so the build no longer depends on an external font service.",
    scopes: ["Platform", "Institution", "Member"],
    type: "Fix",
  },
  {
    date: "2026-09-25",
    title: "Updated Terms of Service and Privacy Policy wording",
    body: "Added Board of Governors disclaimer language, a 72-hour data breach notification commitment, and pledge-related terms — reviewed alongside the new pledge feature.",
    scopes: ["Member"],
    type: "Improvement",
  },
  {
    date: "2026-09-24",
    title: "Shared links now show a proper preview image",
    body: "Sharing an event, campaign, job, or spotlight link now generates a real Open Graph preview image, instead of a generic one, when pasted into WhatsApp, iMessage, or social media.",
    scopes: ["Marketing", "Member"],
    type: "Feature",
  },
  {
    date: "2026-09-24",
    title: "Fixed Google Sign-In in production",
    body: "Google Sign-In wasn't correctly configured for the production domain — fixed.",
    scopes: ["Member"],
    type: "Fix",
  },
  {
    date: "2026-09-23",
    title: "Added share buttons and offline-ready install support",
    body: "Members and institution admins can now share content (events, campaigns, contributions) with a native share sheet, and both portals gained a service worker so they install and behave more like a real app.",
    scopes: ["Institution", "Member"],
    type: "Feature",
  },
  {
    date: "2026-09-22",
    title: "Cleaned up app manifests and landing page SEO metadata",
    body: "Consistency pass across all three portals' PWA manifests and the member portal's SEO/social metadata.",
    scopes: ["Platform", "Institution", "Member", "Marketing"],
    type: "Improvement",
  },
  {
    date: "2026-09-21",
    title: "Institution admins can change their portal's URL slug",
    body: "Previously fixed at creation time; now editable from settings.",
    scopes: ["Institution"],
    type: "Feature",
  },
  {
    date: "2026-09-18",
    title: "Platform staff can see members across every institution",
    body: "A cross-institution member directory for platform staff — who every institution's members are, and which of them are actually active — instead of having to check each institution individually.",
    scopes: ["Platform"],
    type: "Feature",
  },
  {
    date: "2026-09-16",
    title: "Migrated payment and notification processing to a durable workflow engine",
    body: "Contribution/store/service payment callbacks and outbound notifications now run on Temporal, a durable workflow engine — improving reliability when a step fails partway through (e.g. a payment confirms but the follow-up notification fails to send).",
    scopes: ["Platform"],
    type: "Improvement",
  },
  {
    date: "2026-09-16",
    title: "Fixed several payment and notification reliability bugs",
    body: "A duplicate payment reference could show the wrong status, recurring gifts sometimes didn't appear after being set up, and a background worker registration bug meant some scheduled jobs silently didn't run. All fixed alongside the move to the new workflow engine.",
    scopes: ["Institution", "Member", "Platform"],
    type: "Fix",
  },
  {
    date: "2026-09-19",
    title: "Fixed email delivery issues affecting sign-in and registration",
    body: "Verification and Google Sign-In emails were intermittently failing to send via Mailtrap — fixed.",
    scopes: ["Member", "Platform"],
    type: "Fix",
  },
  {
    date: "2026-09-17",
    title: "Fixed a profile picture upload issue",
    body: "Profile pictures weren't reliably saving/displaying after upload — fixed.",
    scopes: ["Member"],
    type: "Fix",
  },
];
