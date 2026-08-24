# Alumni SaaS — Frontend Experience Specification

**Scope of this document:** pages, navigation, components, forms (fields only), and user flows — the *experience*, not the visual design (no colors, spacing, typography, or styling decisions here). This is the UX blueprint for rebuilding the platform as a multi-tenant SaaS with **three portals**:

1. **Platform Portal** *(new)* — used by the SaaS company itself to onboard and manage institutions (tenants), billing/plans, and platform-wide operations.
2. **Institution Portal** *(the current "Admin Portal", reframed as tenant-scoped)* — used by a university/association's own staff to manage their members and engage their alumni community.
3. **Member Portal** *(the current member-facing app, now tenant-branded)* — used by an institution's alumni.

A single alumnus only ever sees one institution's Member Portal (their own). A single institution's staff only ever see their own Institution Portal. Only the SaaS operator's own staff see the Platform Portal — and from it, they can see across all institutions.

---

# PART 1 — PLATFORM PORTAL (new)

**Who uses this**: the SaaS company's own staff (platform ops, support, billing, sales) — not any institution's staff, not any alumnus.

**Core job**: onboard institutions onto the platform, keep them healthy and paying, support them when something breaks, and control what features each institution can access.

## 1.0 Navigation

Sidebar: Dashboard, Institutions, Subscriptions & Billing, Plans & Features, Platform Staff, Support, Announcements, Audit Log, Settings.
Header: platform-wide search (jump to any institution by name/domain), notification bell (billing failures, new signups, support escalations), account menu.

## 1.1 Auth

- **Login**: email + password. Separate credential space from every institution's Institution/Member portals — a platform staff login must never work on a tenant portal and vice versa.
- **Forgot password**: email → reset link.
- Optional (recommended for a SaaS control plane): two-factor prompt after password, since this account can see/impersonate across every tenant.

## 1.2 Dashboard

**Purpose**: at-a-glance health of the whole platform.

- Stat tiles: Total Institutions (active/trial/suspended breakdown), Total Members across all institutions, Monthly Recurring Revenue, New Institutions this month.
- Growth chart: institutions and total members over time.
- Revenue chart: MRR trend, plan-mix breakdown (how many institutions on each plan tier).
- "Needs attention" list: institutions with failed payments, expiring trials, or support tickets open.
- Recent signups list: newest institutions onboarded, with quick link to their detail page.

## 1.3 Institutions (list)

**Purpose**: the tenant registry — every institution using the platform.

- Search (name/domain/contact email) + filters: plan tier, status (Trial / Active / Past Due / Suspended / Cancelled), date onboarded.
- Table: Institution name, Domain/slug, Plan, Member count, Status badge, MRR, Onboarded date, Actions.
- Row actions: View detail, Suspend (temporarily blocks that institution's staff and members from logging in — reversible), Impersonate (see 1.4).
- "Add Institution" button → onboarding flow (below).
- Export list to CSV.

### Institution onboarding flow (create new tenant)
Multi-step form, launched either by platform staff (manual onboarding) or completed by the institution itself via a public "Start your alumni platform" signup page that feeds into the same record:

1. **Institution details**: Name, Short code/subdomain slug (e.g. `greenfield` → `greenfield.yourplatform.com`), Country, Primary contact name/email/phone.
2. **Branding**: Logo upload, Primary color, Portal display name (what members see, e.g. "Greenfield Alumni").
3. **Plan selection**: choose a plan tier (see 1.5) or start on a free trial.
4. **First institution admin account**: name, email — this person becomes the institution's first Institution Portal SuperAdmin and receives an invite email to set their password.
5. **Review & create** → institution record is created, invite email sent, institution appears in the list as "Trial" or "Active" depending on plan chosen.

## 1.4 Institution Detail

**Purpose**: everything about one tenant, and the tools to support/manage them.

Tabs or sections:
- **Overview**: contact info, plan, status, member count, storage/usage against plan limits, dates (onboarded, trial ends, last activity).
- **Branding**: logo, colors, custom domain status (if using their own domain, e.g. `alumni.greenfield.edu` instead of the shared subdomain) — view/edit.
- **Institution Admins**: list of that institution's own Institution-Portal admin users (mirrors what the institution sees on their own "Admins" page) — platform staff can view this list and, if needed, reset an admin's access or deactivate an account for support purposes.
- **Usage & Limits**: current usage vs. plan caps (members, storage, emails sent, campaigns) with progress indicators; upgrade/downgrade plan action.
- **Billing**: subscription status, next billing date, payment method on file (masked), invoice history (downloadable), manual "record payment" or "apply credit" action for edge cases, "Suspend for non-payment" / "Reinstate" actions.
- **Feature toggles**: per-institution overrides of which modules are enabled (Forum, Mentorship, Job Board, Leaderboard, Spotlights, Referrals, Class Notes, Resources) — independent of plan defaults, for pilots/custom deals.
- **Activity log**: recent actions taken within this institution's portals (approvals, campaigns created, large exports) — support/audit visibility.
- **Support notes**: freeform internal notes thread visible only to platform staff (ticket history, special arrangements).
- **Danger zone**: Suspend institution (blocks all logins for that tenant), Delete institution (double-confirmed, exports data first).

### Impersonation ("Log in as")
From an institution's detail page, platform staff can start an impersonation session that drops them into that institution's Institution Portal (or a specific member's Member Portal) as if logged in, for support/troubleshooting. The impersonated session is visibly flagged (a persistent banner: "Viewing as {Institution} — Exit impersonation") and every action taken while impersonating is attributed in the audit log to the platform staff member, not silently as the institution's own admin.

## 1.5 Subscriptions & Billing

**Purpose**: platform-wide revenue operations.

- List of all subscriptions across institutions: institution, plan, status, MRR contribution, next renewal date, payment method status.
- Filters: status (active/past due/cancelled/trialing), plan tier.
- Failed-payment queue: institutions whose last charge failed, with retry/contact actions.
- Invoice search across all tenants.

## 1.6 Plans & Features

**Purpose**: define what institutions can buy and what each tier includes.

- List of plan tiers (e.g. Starter / Growth / Enterprise) with: price, billing interval, member-count cap, storage cap, included modules (checklist of the same module list as feature toggles), support level.
- Create/edit plan form: Name, Price, Billing interval (monthly/annual), Member limit, Storage limit, Module checklist, Trial length (days).
- Archiving a plan (existing subscribers stay on it, no longer offered to new institutions) vs. deleting.

## 1.7 Platform Staff

**Purpose**: manage the SaaS company's own internal users (mirrors the institution-level "Admins" page but one level up).

- List: name, email, role (SuperAdmin / Support / Billing / Sales — read-only vs. full-access roles), status, last login.
- Create/edit staff form: name, email, role, active/disabled toggle.

## 1.8 Support

**Purpose**: a light internal ticket/issue view so platform staff can track institution-reported problems without leaving the portal (could also be a link out to a dedicated helpdesk tool — either way, the experience needed here is a queue).

- Ticket list: institution, subject, status (Open/In progress/Resolved), priority, last updated.
- Ticket detail: message thread, internal notes, linked institution (jumps to Institution Detail), status change control.

## 1.9 Announcements

**Purpose**: broadcast a message to some or all institutions (e.g. planned maintenance, new feature release).

- Compose form: Title, Body, Audience (All institutions / specific plan tier / specific institutions), Send now or schedule.
- History list of past announcements with delivery stats (how many institutions/admins saw it).
- Announcements surface inside the Institution Portal as a dismissible banner or a notification-panel entry.

## 1.10 Audit Log

**Purpose**: platform-wide accountability trail — every sensitive action taken by any platform staff member (institution created/suspended/deleted, plan changed, impersonation started/ended, staff account created).

- Filterable table: actor, action type, target institution, timestamp.
- Row expand → detail of what changed (before/after where applicable).

## 1.11 Settings

**Purpose**: global platform configuration.

- Default branding fallback (used before an institution sets their own).
- Default email sender identity for system emails.
- Global feature flag defaults for new institutions.
- API/webhook configuration for platform-level integrations (payment processor platform account, transactional email provider).

## 1.12 Platform Portal — Component Inventory (functional)

Paginated data table (sortable columns, row actions, bulk select), search-with-live-preview box, status badge, filter pill group, stat tile, trend chart, progress-against-limit bar, confirm-action dialog (destructive vs. neutral variants), multi-step wizard form (onboarding), tabbed detail page, side-panel/drawer for quick actions, toast notifications, file/image upload control, in-page banner (impersonation flag, announcement banner), rich text composer (announcements), CSV export action, audit-trail timeline.

---

# PART 2 — INSTITUTION PORTAL (formerly "Admin Portal")

**Who uses this**: a single institution's own staff (the university/association's alumni-office employees). Everything here is scoped to that one institution's data — an Institution Portal user never sees another tenant's members, campaigns, or content.

This is functionally the same tool documented for the current Admin Portal, reframed as tenant-scoped, plus a few new pages needed to operate as one tenant among many (branding/settings, plan/usage awareness, billing).

## 2.0 Navigation

Sidebar, in order: Dashboard, Members, [Institution Admins — SuperAdmin-of-institution only], Campaigns, Membership Renewal, Contributions, — Community — Job Board, Events, News, Forum, Mentorship, Resources, Spotlights, — Reports — Reports & Exports, Notifications, — Institution — Branding & Settings, Plan & Billing.

Header: notification bell, account menu (Settings, Log out).

Access tiers *within* an institution, unchanged from the current system: **SuperAdmin** (full control of this institution, incl. its own admin users, forum moderation, year-group-wide targeting) and **Admin** (day-to-day operations, scoped to their own assigned member cohort/year-group where applicable).

## 2.1 Auth

- **Login**: email + password, show/hide toggle, "Forgot password?" link.
- **Forgot password**: email → reset link → confirmation screen.
- Session guard: unauthenticated visitors are redirected to Login; a logged-in Member-role user cannot access this portal.

## 2.2 Dashboard

Read-only overview, no data entry.
- Stat tiles: Total Members (+ pending-approval count), Active Campaigns, Total Collected, Upcoming Events (+ open job postings count).
- Contribution trend chart (recent months).
- Pending Approvals shortlist (first few, with a link to the full queue).
- Active Campaigns progress list.
- Recent Contributions table (latest payments across all campaigns).

## 2.3 Members

**Purpose**: the membership pipeline — approve/reject new registrants, moderate accounts, bulk-manage the roster.

- Search + status filter (All/Active/Pending/Suspended/Banned/Blocked).
- Table: member summary (avatar, name, email), member number, class year, status, email-verified indicator, joined date, row actions.
- Row actions, contextual to status: Approve / Reject (Pending), Ban (Active/Suspended), Unban (Banned), View full profile (always).
- Confirm dialogs for each moderation action, each with an optional reason field where relevant, and clear language about consequences (e.g. "after 3 rejections this account is permanently blocked").
- **Bulk import**: paste/upload a structured list of members (name, email, class year, optional pre-paid membership years for migrating an existing roster) → import runs, reports how many succeeded/were skipped and why.
- **Export** the current roster to a spreadsheet file.
- Row density toggle (comfortable/compact) for scanning a large roster.
- Pagination.

## 2.4 Member Detail

- Profile header: avatar, name, status, member number, verified indicator, contextual moderation actions (same rules as the list).
- Personal info, professional info, bio sections.
- Membership section: active/inactive status, years paid, and a manual "Activate Membership" action for backfilling years without an online payment (e.g. someone who paid in person/by transfer).
- Notes section: rejection history, ban reason, if any.
- "Copy shareable link" to this profile (for internal handoff between staff).

## 2.5 Institution Admins *(SuperAdmin-of-institution only)*

- List of this institution's own staff accounts: name, email, role, assigned year-group scope (if role is limited to one cohort), active/disabled status, created date.
- Create form: first/last name, email, password, role, optional year-group scope.
- Edit form: same fields plus enable/disable toggle.

## 2.6 Campaigns

**Purpose**: fundraising drives outside of standard membership dues.

- Status filter pills (Draft/Active/Closed/Completed/Archived).
- Create/Edit form: Title, Description, Target amount, Suggested/minimum contribution, Deadline, Target audience (all members or specific class years), Banner image, Optional video link, Online payments toggle, Manual/offline payments toggle (reveals bank details and/or mobile-money details to display to contributors).
- Card grid of campaigns: progress bar, key stats, and lifecycle actions — View details, Edit (while Active), Close (stop accepting new payments), Archive/Restore, Re-open a completed campaign.
- Pagination.

## 2.7 Campaign Detail

- Summary: description, progress, stats, status.
- Payment-collection overview for online payments: how much has come in, how much has been forwarded to the institution's real bank account so far, and an action to mark the outstanding balance as disbursed once transferred.
- Media (banner/video) with a full-view option.
- Contribution list scoped to this one campaign.
- Inline edit while the campaign is Active.

## 2.8 Membership Renewal

**Purpose**: the recurring annual/periodic dues cycle, distinct from one-off fundraising campaigns because it determines whether a member counts as "active."

- Summary stats: eligible members, paid this period, unpaid this period, total renewal cycles run historically.
- Sections grouped by time: Current period, Future (early-renewal) periods, Past periods (compact history view).
- Create form (SuperAdmin only): Period/year label, Description, Standard amount, Discounted/pensioner amount (optional), Deadline, Banner, Online/manual payment toggles + associated payment-instruction details.
- Each period's card shows paid/unpaid/total, a progress bar, and a "Manage" link into the Campaign Detail experience above (renewal periods are a special kind of campaign under the hood, but presented distinctly here).

## 2.9 Contributions

**Purpose**: the payment ledger across every campaign and renewal period.

- **Record a manual payment** form: which campaign/period, member identifier, member name/email (for lookup or new entries), amount, payment method (cash/bank transfer/mobile money/other manual), reference note, optional free-text notes, and whether to mark it confirmed immediately or leave it pending review.
- Search + status filter (Pending/Confirmed/Rejected).
- Table: member, campaign, amount, reference, method, status, date.
- Export to spreadsheet.
- Row density toggle, pagination.

## 2.10 Job Board

- Create/Edit form: Title, Company, Location, Employment type, Target audience (all or specific class years), Application deadline, Apply link, Banner image, Description.
- Filters: search, location, posted-date range, status pills, type pills.
- Card grid: key info, status, edit/preview/close/delete actions.
- Detail page: full posting view with edit and lifecycle actions (close, delete) and an "Applications closed" state once the deadline passes.

## 2.11 Events

- Create/Edit form: Title, Description, Start/end date & time, Venue, Map link, Capacity, Target audience, Status, Banner image, Photo gallery, Video links.
- Card grid: status, RSVP count against capacity, and actions — View RSVPs, Edit, Cancel, Delete.
- **RSVP management page**: attendee list for one event, filterable by RSVP status, with the ability to see who registered and when they cancelled (if applicable).

## 2.12 News

- Create/Edit form: Title, Category, Publish status (Draft/Published/Archived), Pin-to-top toggle, Target audience, Rich-text body content, Image gallery, Video links.
- List: search, status filter, card grid showing a content preview, publish/archive lifecycle actions.
- Detail page: full rendered article with an image gallery and embedded videos, plus publish/archive controls.

## 2.13 Forum *(SuperAdmin only)*

- Two views: **Categories** (create/list discussion categories) and **Threads** (moderate existing discussions — search, filter by category, filter by recent/popular/pinned).
- Thread moderation actions: pin/unpin, close/reopen discussion, delete.

## 2.14 Mentorship

- Two views: **Mentors** (review and approve/decline mentor applications, see each mentor's current mentee load against their stated capacity) and **Requests** (see the pairing requests flowing between mentees and mentors).

## 2.15 Resources

- Create/Edit form: Title, Category, Description, Type (an uploaded file or an external link), the file or URL itself, Target audience, Banner image.
- List: search, category/type/date filters, card grid with view/edit/delete.
- Detail page: type-aware preview (video embed, document preview, image, or a link-out card), download/open action, related-resources suggestions.

## 2.16 Spotlights

**Purpose**: curated alumni success-story features.

- Status filter (Pending/Approved/Rejected) over member-submitted stories, with approve/reject actions (reject supports an optional reason).
- **Feature a member directly**: search for a member, then write a title/story to publish on their behalf without waiting for a self-submission — used when staff want to proactively highlight someone.

## 2.17 Reports & Exports

- Summary stat tiles: members, contributions, total collected, campaigns, events.
- Campaign performance view (progress per campaign) with export.
- Status breakdown (active vs. closed campaigns).
- One-click exports: members, contributions, events, jobs, campaigns — each downloads a spreadsheet file.

## 2.18 Notifications (institution-facing)

- Full inbox view of system notifications relevant to staff (new registrations awaiting approval, new contributions, etc.), filterable, with mark-as-read/mark-all-read and "load more" pagination.

## 2.19 Branding & Settings *(new for SaaS — institution-level self-service)*

**Purpose**: let each institution customize their own portals without needing the Platform Portal.

- Institution profile: display name, contact details, logo upload, primary color, portal tagline/description.
- Custom domain setup (optional): institution can point their own domain at their portals, with a status indicator (pending verification/active).
- Notification preferences for the institution as a whole (which system emails/alerts go out to staff).
- Account security: change password.

## 2.20 Plan & Billing *(new for SaaS — institution-level self-service)*

**Purpose**: let the institution see and manage their own subscription without contacting the platform operator.

- Current plan summary: tier, price, renewal date, usage against limits (members/storage/etc.) shown as progress indicators.
- Upgrade/downgrade plan action.
- Payment method on file (update card/payment details).
- Invoice history with downloadable receipts.

## 2.21 Institution Portal — Component Inventory (functional)

Paginated/searchable/filterable data table with row actions, card grid with lifecycle-action buttons, confirm-action dialog (destructive vs. neutral), multi-field create/edit form (inline-expanding or full page), status/type badge, progress bar, image/gallery uploader (single and multi), rich-text composer, YouTube/video embed & preview, tag-style multi-select for audience targeting (e.g. class years), tabbed or sectioned detail page, notification bell + panel, toast messages, CSV/spreadsheet export action, row-density toggle, search-with-live-preview box, date-range filter, lightbox/full-view for images.

---

# PART 3 — MEMBER PORTAL (tenant-branded)

**Who uses this**: an institution's own alumni, once approved. Same experience shape as the current member app, now presented under each institution's own branding (their logo/colors/name), and — where the platform's URL structure requires it — reached via that institution's own subdomain or custom domain.

## 3.0 Navigation

Sidebar: Dashboard, My Contributions, Job Board, Events, Alumni Directory, News, Forum, Mentorship, Resources, Leaderboard, Spotlights, Refer a Friend, Class Notes, Notifications, My Profile. Footer: account summary, Settings, Log out.
Mobile: bottom nav (Home, Pay, Jobs, Events, Profile) + slide-in drawer for the rest.
Header: notification bell.

## 3.1 Auth

- **Login**: email + password.
- **Register**: guided multi-step signup — personal details → alumni details (class year, department, optional referral code, optional student ID) → set a password → email verification via a one-time code → "pending approval" waiting screen (with an optional "pay your dues now to speed things up" prompt).
- **Forgot password**: email → reset link → confirmation.

## 3.2 Dashboard

- Membership status summary (active/inactive, expiring-soon warning, "you're all set" state).
- Outstanding-dues warning if applicable, with a clear path to pay.
- Quick stats: campaigns supported, total contributed, contributed this period, upcoming events.
- "Dues to pay" list and an "early renewal" list for getting ahead of next period.
- Open campaigns to support.
- Upcoming events preview.
- Recent payments preview.

## 3.3 My Contributions

- Grid of open campaigns/dues you can pay, each showing progress, amount, deadline, and a Pay action (or a "paid" confirmation with a certificate link for membership dues).
- Full payment history table.
- A live status check while a payment is processing (for online payments), so the member isn't left wondering if it went through.
- **Campaign detail page**: full description, media, a flexible or fixed amount selector, the Pay action, a public share link, and — if the institution allows manual payments — the bank/mobile-money details to pay by transfer instead.

## 3.4 Events

- Browse upcoming/past events, RSVP or cancel with one action.
- Event detail: full description, media gallery, venue/map link, RSVP state and capacity awareness ("X spots left" / "full").

## 3.5 Job Board

- Browse postings with search/location/type filters.
- Detail page with an external "Apply" action and a clear deadline/closed state.

## 3.6 Alumni Directory

- Search classmates by name/company/location/class year.
- Click a person to open a detail panel with their public profile info and a LinkedIn link if provided.

## 3.7 News

- Browse announcements/news by category.
- Article detail with rich content and media.

## 3.8 Forum

- Browse/search discussion threads by category, start a new thread, reply within a thread; closed threads are clearly marked read-only.

## 3.9 Mentorship

- **Find a mentor**: browse available mentors, request mentorship with a short note.
- **My requests**: track the status of requests you've sent.
- **Incoming** (only if you're an approved mentor): accept/decline requests from mentees.
- **Become a mentor**: apply with your area of expertise and capacity; track your application's review status.

## 3.10 Resources

- Browse a library of files/links with search, category, and type filters.
- Detail page with an inline preview where possible, a download/open action, and related-resource suggestions.
- Personal "save for later" list.

## 3.11 Leaderboard

- Class-year rankings by membership rate, total contributed, and event attendance, with your own class year highlighted.

## 3.12 Spotlights

- Browse featured alumni success stories.
- Submit your own story for review, and track your own submissions' status.

## 3.13 Refer a Friend

- Your personal referral link/code with a copy action.
- Send an invitation by email directly from the page.
- Track your referral funnel (invited → registered → became an active member) and any referral-based recognition earned.

## 3.14 Class Notes

- A wall-style feed scoped to your own class year: post an update, like others' posts, delete your own posts.

## 3.15 Notifications

- Full notification inbox with category filters (jobs, events, campaigns, payments, class notes), mark-as-read actions, and "load more" pagination.

## 3.16 My Profile

- Update your photo, contact/professional details, bio, and LinkedIn link.
- Employment status toggle (standard vs. a discounted-dues category), clearly flagged as a one-way choice where that's the business rule.
- Any badges/recognition you've earned.
- Change your password.

## 3.17 Settings

- Account summary with a link to the full profile editor.
- Notification preferences by category (toggle each type of alert on/off).
- Security (password change).

## 3.18 Membership Certificate

- View and download a certificate for each period you've paid dues, with a year selector if you have more than one.
- Certificate includes your details, the payment it's based on, and a verification reference.

## 3.19 Public / Pre-Login Pages

- **Landing page**: institution-branded marketing/informational page introducing the alumni platform, with sign-in and join calls to action.
- **Guest membership activation**: a direct-link page (e.g. from an invitation) letting someone pay dues before their account is fully approved, followed by a status page confirming activation and inviting them to sign in.
- **Public campaign page**: a shareable, no-login-required donation page for a specific campaign, with a simple contribute action and social-share links — useful for reaching non-members too (e.g. friends/family of alumni, or the wider public for a fundraising push).

## 3.20 Member Portal — Component Inventory (functional)

Paginated/searchable/filterable listing grids, detail drawers/panels, confirm-action dialogs, forms with inline validation feedback, image upload (avatar), rich content viewer, media gallery with full-view/lightbox, progress bars, status/category badges, notification bell + panel, toast messages, tabbed sections, expandable "read more" text blocks, live-updating payment-status indicator, copy-to-clipboard actions, social-share actions, certificate viewer with a downloadable-file action, empty states for every list, loading placeholders for every list/card/table.

---

# 4. Cross-Portal Experience Notes

- **One identity per portal type.** A platform-staff login, an institution-staff login, and a member login are three separate credential spaces. Nobody moves between portals just by re-navigating — each requires its own sign-in, except platform staff using the explicit, clearly-flagged "impersonate" tool.
- **Tenant boundary is invisible to the people inside it.** An institution's staff and members should never see any hint that other institutions exist on the platform — no shared directories, no cross-tenant search, no leaking of another institution's branding or data.
- **Consistent patterns across all three portals** for the things that repeat everywhere: paginated tables, card grids, confirm dialogs before destructive actions, toast feedback after every action, empty states, loading states, and search-with-live-preview. Reusing one shared pattern library across all three frontends (rather than three separate implementations) keeps the experience coherent and speeds up building the new Platform Portal.
- **Notifications are additive, not a redesign.** Institution and Member portals keep their existing notification-bell pattern; the Platform Portal gets the same pattern for its own audience (billing issues, new signups, support escalations).
- **Everything that was self-submit-then-review in the current system stays that way** (spotlights, mentor applications, class notes moderation-by-absence) — the SaaS reframing changes *who owns the data*, not the underlying review workflows members and institution staff already understand.
