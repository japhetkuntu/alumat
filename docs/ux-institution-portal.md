# Institution Portal — Experience Spec

## Context

This is one of three portals in a multi-tenant alumni-engagement SaaS product:

1. **Platform Portal** — used internally by the SaaS company to onboard and manage the institutions (tenants) that subscribe to the product. (Separate document.)
2. **Institution Portal** *(this document)* — used by each subscribing institution's own staff (a university/association's alumni office) to manage their members and engage their alumni community.
3. **Member Portal** — used by an institution's alumni, branded per-institution. (Separate document.)

**Who uses the Institution Portal**: a single institution's own staff. Everything here is scoped to that one institution's data — a user of this portal never sees another tenant's members, campaigns, or content. This is the tool an alumni office runs day-to-day.

This document covers pages, navigation, components, forms (fields only), and flows — not visual design (no colors, spacing, typography, or styling direction).

---

## Navigation

Sidebar, in order: Dashboard, Members, [Institution Admins — SuperAdmin-of-institution only], Campaigns, Membership Renewal, Contributions, — Community — Job Board, Events, News, Forum, Mentorship, Resources, Spotlights, — Reports — Reports & Exports, Notifications, — Institution — Branding & Settings, Plan & Billing.

Header: notification bell, account menu (Settings, Log out).

Access tiers within an institution: **SuperAdmin** (full control of this institution, incl. its own admin users, forum moderation, year-group-wide targeting) and **Admin** (day-to-day operations, scoped to their own assigned member cohort/year-group where applicable).

## Auth

- **Login**: email + password, show/hide toggle, "Forgot password?" link.
- **Forgot password**: email → reset link → confirmation screen.
- Session guard: unauthenticated visitors are redirected to Login; a logged-in Member-role user cannot access this portal.

## Dashboard

Read-only overview, no data entry.
- Stat tiles: Total Members (+ pending-approval count), Active Campaigns, Total Collected, Upcoming Events (+ open job postings count).
- Contribution trend chart (recent months).
- Pending Approvals shortlist (first few, with a link to the full queue).
- Active Campaigns progress list.
- Recent Contributions table (latest payments across all campaigns).

## Members

**Purpose**: the membership pipeline — approve/reject new registrants, moderate accounts, bulk-manage the roster.

- Search + status filter (All/Active/Pending/Suspended/Banned/Blocked).
- Table: member summary (avatar, name, email), member number, class year, status, email-verified indicator, joined date, row actions.
- Row actions, contextual to status: Approve / Reject (Pending), Ban (Active/Suspended), Unban (Banned), View full profile (always).
- Confirm dialogs for each moderation action, each with an optional reason field where relevant, and clear language about consequences (e.g. "after 3 rejections this account is permanently blocked").
- **Bulk import**: paste/upload a structured list of members (name, email, class year, optional pre-paid membership years for migrating an existing roster) → import runs, reports how many succeeded/were skipped and why.
- **Export** the current roster to a spreadsheet file.
- Row density toggle (comfortable/compact) for scanning a large roster.
- Pagination.

## Member Detail

- Profile header: avatar, name, status, member number, verified indicator, contextual moderation actions (same rules as the list).
- Personal info, professional info, bio sections.
- Membership section: active/inactive status, years paid, and a manual "Activate Membership" action for backfilling years without an online payment (e.g. someone who paid in person/by transfer).
- Notes section: rejection history, ban reason, if any.
- "Copy shareable link" to this profile (for internal handoff between staff).

## Institution Admins *(SuperAdmin-of-institution only)*

- List of this institution's own staff accounts: name, email, role, assigned year-group scope (if role is limited to one cohort), active/disabled status, created date.
- Create form: first/last name, email, password, role, optional year-group scope.
- Edit form: same fields plus enable/disable toggle.

## Campaigns

**Purpose**: fundraising drives outside of standard membership dues.

- Status filter pills (Draft/Active/Closed/Completed/Archived).
- Create/Edit form: Title, Description, Target amount, Suggested/minimum contribution, Deadline, Target audience (all members or specific class years), Banner image, Optional video link, Online payments toggle, Manual/offline payments toggle (reveals bank details and/or mobile-money details to display to contributors).
- Card grid of campaigns: progress bar, key stats, and lifecycle actions — View details, Edit (while Active), Close (stop accepting new payments), Archive/Restore, Re-open a completed campaign.
- Pagination.

## Campaign Detail

- Summary: description, progress, stats, status.
- Payment-collection overview for online payments: how much has come in, how much has been forwarded to the institution's real bank account so far, and an action to mark the outstanding balance as disbursed once transferred.
- Media (banner/video) with a full-view option.
- Contribution list scoped to this one campaign.
- Inline edit while the campaign is Active.

## Membership Renewal

**Purpose**: the recurring annual/periodic dues cycle, distinct from one-off fundraising campaigns because it determines whether a member counts as "active."

- Summary stats: eligible members, paid this period, unpaid this period, total renewal cycles run historically.
- Sections grouped by time: Current period, Future (early-renewal) periods, Past periods (compact history view).
- Create form (SuperAdmin only): Period/year label, Description, Standard amount, Discounted/pensioner amount (optional), Deadline, Banner, Online/manual payment toggles + associated payment-instruction details.
- Each period's card shows paid/unpaid/total, a progress bar, and a "Manage" link into the Campaign Detail experience above (renewal periods are a special kind of campaign under the hood, but presented distinctly here).

## Contributions

**Purpose**: the payment ledger across every campaign and renewal period.

- **Record a manual payment** form: which campaign/period, member identifier, member name/email (for lookup or new entries), amount, payment method (cash/bank transfer/mobile money/other manual), reference note, optional free-text notes, and whether to mark it confirmed immediately or leave it pending review.
- Search + status filter (Pending/Confirmed/Rejected).
- Table: member, campaign, amount, reference, method, status, date.
- Export to spreadsheet.
- Row density toggle, pagination.

## Job Board

- Create/Edit form: Title, Company, Location, Employment type, Target audience (all or specific class years), Application deadline, Apply link, Banner image, Description.
- Filters: search, location, posted-date range, status pills, type pills.
- Card grid: key info, status, edit/preview/close/delete actions.
- Detail page: full posting view with edit and lifecycle actions (close, delete) and an "Applications closed" state once the deadline passes.

## Events

- Create/Edit form: Title, Description, Start/end date & time, Venue, Map link, Capacity, Target audience, Status, Banner image, Photo gallery, Video links.
- Card grid: status, RSVP count against capacity, and actions — View RSVPs, Edit, Cancel, Delete.
- **RSVP management page**: attendee list for one event, filterable by RSVP status, with the ability to see who registered and when they cancelled (if applicable).

## News

- Create/Edit form: Title, Category, Publish status (Draft/Published/Archived), Pin-to-top toggle, Target audience, Rich-text body content, Image gallery, Video links.
- List: search, status filter, card grid showing a content preview, publish/archive lifecycle actions.
- Detail page: full rendered article with an image gallery and embedded videos, plus publish/archive controls.

## Forum *(SuperAdmin only)*

- Two views: **Categories** (create/list discussion categories) and **Threads** (moderate existing discussions — search, filter by category, filter by recent/popular/pinned).
- Thread moderation actions: pin/unpin, close/reopen discussion, delete.

## Mentorship

- Two views: **Mentors** (review and approve/decline mentor applications, see each mentor's current mentee load against their stated capacity) and **Requests** (see the pairing requests flowing between mentees and mentors).

## Resources

- Create/Edit form: Title, Category, Description, Type (an uploaded file or an external link), the file or URL itself, Target audience, Banner image.
- List: search, category/type/date filters, card grid with view/edit/delete.
- Detail page: type-aware preview (video embed, document preview, image, or a link-out card), download/open action, related-resources suggestions.

## Spotlights

**Purpose**: curated alumni success-story features.

- Status filter (Pending/Approved/Rejected) over member-submitted stories, with approve/reject actions (reject supports an optional reason).
- **Feature a member directly**: search for a member, then write a title/story to publish on their behalf without waiting for a self-submission — used when staff want to proactively highlight someone.

## Reports & Exports

- Summary stat tiles: members, contributions, total collected, campaigns, events.
- Campaign performance view (progress per campaign) with export.
- Status breakdown (active vs. closed campaigns).
- One-click exports: members, contributions, events, jobs, campaigns — each downloads a spreadsheet file.

## Notifications (institution-facing)

- Full inbox view of system notifications relevant to staff (new registrations awaiting approval, new contributions, etc.), filterable, with mark-as-read/mark-all-read and "load more" pagination.

## Branding & Settings *(new for SaaS — institution-level self-service)*

**Purpose**: let each institution customize their own portals without needing the Platform Portal.

- Institution profile: display name, contact details, logo upload, primary color, portal tagline/description.
- Custom domain setup (optional): institution can point their own domain at their portals, with a status indicator (pending verification/active).
- Notification preferences for the institution as a whole (which system emails/alerts go out to staff).
- Account security: change password.

## Plan & Billing *(new for SaaS — institution-level self-service)*

**Purpose**: let the institution see and manage their own subscription without contacting the platform operator.

- Current plan summary: tier, price, renewal date, usage against limits (members/storage/etc.) shown as progress indicators.
- Upgrade/downgrade plan action.
- Payment method on file (update card/payment details).
- Invoice history with downloadable receipts.

## Component Inventory (functional)

Paginated/searchable/filterable data table with row actions, card grid with lifecycle-action buttons, confirm-action dialog (destructive vs. neutral), multi-field create/edit form (inline-expanding or full page), status/type badge, progress bar, image/gallery uploader (single and multi), rich-text composer, YouTube/video embed & preview, tag-style multi-select for audience targeting (e.g. class years), tabbed or sectioned detail page, notification bell + panel, toast messages, CSV/spreadsheet export action, row-density toggle, search-with-live-preview box, date-range filter, lightbox/full-view for images.

## Experience Principles

- **Everything is scoped to one institution.** No page in this portal should ever be able to surface another tenant's data — this is the core trust boundary of the product.
- **Two access tiers, consistently applied.** SuperAdmin-only areas (institution admin management, forum moderation, audience targeting across all class years, membership-cycle creation) should be visibly and consistently gated, not just hidden from navigation while still reachable directly.
- **Review-then-publish patterns repeat by design.** Member-submitted spotlights, mentor applications, and manual-payment confirmations all follow the same "submitted → staff reviews → approved/rejected" shape — treat this as one reusable pattern, not three different ones.
- **Self-service reduces support load.** Branding and Plan & Billing exist so an institution can manage its own identity and subscription without contacting the platform operator — these should feel like natural extensions of the portal, not an awkward bolt-on.
