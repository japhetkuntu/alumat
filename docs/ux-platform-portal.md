# Platform Portal — Experience Spec

## Context

This is one of three portals in a multi-tenant alumni-engagement SaaS product:

1. **Platform Portal** *(this document)* — used internally by the SaaS company to onboard and manage the institutions (tenants) that subscribe to the product.
2. **Institution Portal** — used by each subscribing institution's own staff to manage their members and engage their alumni community. (Separate document.)
3. **Member Portal** — used by an institution's alumni, branded per-institution. (Separate document.)

**Who uses the Platform Portal**: the SaaS company's own staff only — platform operations, support, billing, sales. Never an institution's staff, never an alumnus. From here, staff can see and act across every institution on the platform.

**Core job of this portal**: onboard institutions, keep them healthy and paying, support them when something breaks, and control what features each institution can access.

This document covers pages, navigation, components, forms (fields only), and flows — not visual design (no colors, spacing, typography, or styling direction).

---

## Navigation

Sidebar: Dashboard, Institutions, Subscriptions & Billing, Plans & Features, Platform Staff, Support, Announcements, Audit Log, Settings.
Header: platform-wide search (jump to any institution by name/domain), notification bell (billing failures, new signups, support escalations), account menu.

## Auth

- **Login**: email + password. Separate credential space from every institution's Institution/Member portals — a platform staff login must never work on a tenant portal and vice versa.
- **Forgot password**: email → reset link.
- Recommended: two-factor prompt after password, since this account can see/impersonate across every tenant.

## Dashboard

**Purpose**: at-a-glance health of the whole platform.

- Stat tiles: Total Institutions (active/trial/suspended breakdown), Total Members across all institutions, Monthly Recurring Revenue, New Institutions this month.
- Growth chart: institutions and total members over time.
- Revenue chart: MRR trend, plan-mix breakdown (how many institutions on each plan tier).
- "Needs attention" list: institutions with failed payments, expiring trials, or support tickets open.
- Recent signups list: newest institutions onboarded, with quick link to their detail page.

## Institutions (list)

**Purpose**: the tenant registry — every institution using the platform.

- Search (name/domain/contact email) + filters: plan tier, status (Trial / Active / Past Due / Suspended / Cancelled), date onboarded.
- Table: Institution name, Domain/slug, Plan, Member count, Status badge, MRR, Onboarded date, Actions.
- Row actions: View detail, Suspend (temporarily blocks that institution's staff and members from logging in — reversible), Impersonate (see below).
- "Add Institution" button → onboarding flow (below).
- Export list to CSV.

### Institution onboarding flow (create new tenant)
Multi-step form, launched either by platform staff (manual onboarding) or completed by the institution itself via a public "Start your alumni platform" signup page that feeds into the same record:

1. **Institution details**: Name, Short code/subdomain slug (e.g. `greenfield` → `greenfield.yourplatform.com`), Country, Primary contact name/email/phone.
2. **Branding**: Logo upload, Primary color, Portal display name (what members see, e.g. "Greenfield Alumni").
3. **Plan selection**: choose a plan tier or start on a free trial.
4. **First institution admin account**: name, email — this person becomes the institution's first Institution Portal SuperAdmin and receives an invite email to set their password.
5. **Review & create** → institution record is created, invite email sent, institution appears in the list as "Trial" or "Active" depending on plan chosen.

## Institution Detail

**Purpose**: everything about one tenant, and the tools to support/manage them.

Tabs or sections:
- **Overview**: contact info, plan, status, member count, storage/usage against plan limits, dates (onboarded, trial ends, last activity).
- **Branding**: logo, colors, custom domain status (if using their own domain, e.g. `alumni.institution.edu` instead of the shared subdomain) — view/edit.
- **Institution Admins**: list of that institution's own Institution-Portal admin users (mirrors what the institution sees on their own "Admins" page) — platform staff can view this list and, if needed, reset an admin's access or deactivate an account for support purposes.
- **Usage & Limits**: current usage vs. plan caps (members, storage, emails sent, campaigns) with progress indicators; upgrade/downgrade plan action.
- **Billing**: subscription status, next billing date, payment method on file (masked), invoice history (downloadable), manual "record payment" or "apply credit" action for edge cases, "Suspend for non-payment" / "Reinstate" actions.
- **Feature toggles**: per-institution overrides of which modules are enabled (Forum, Mentorship, Job Board, Leaderboard, Spotlights, Referrals, Class Notes, Resources) — independent of plan defaults, for pilots/custom deals.
- **Activity log**: recent actions taken within this institution's portals (approvals, campaigns created, large exports) — support/audit visibility.
- **Support notes**: freeform internal notes thread visible only to platform staff (ticket history, special arrangements).
- **Danger zone**: Suspend institution (blocks all logins for that tenant), Delete institution (double-confirmed, exports data first).

### Impersonation ("Log in as")
From an institution's detail page, platform staff can start an impersonation session that drops them into that institution's Institution Portal (or a specific member's Member Portal) as if logged in, for support/troubleshooting. The impersonated session is visibly flagged (a persistent banner: "Viewing as {Institution} — Exit impersonation") and every action taken while impersonating is attributed in the audit log to the platform staff member, not silently as the institution's own admin.

## Subscriptions & Billing

**Purpose**: platform-wide revenue operations.

- List of all subscriptions across institutions: institution, plan, status, MRR contribution, next renewal date, payment method status.
- Filters: status (active/past due/cancelled/trialing), plan tier.
- Failed-payment queue: institutions whose last charge failed, with retry/contact actions.
- Invoice search across all tenants.

## Plans & Features

**Purpose**: define what institutions can buy and what each tier includes.

- List of plan tiers (e.g. Starter / Growth / Enterprise) with: price, billing interval, member-count cap, storage cap, included modules (checklist of the same module list as feature toggles), support level.
- Create/edit plan form: Name, Price, Billing interval (monthly/annual), Member limit, Storage limit, Module checklist, Trial length (days).
- Archiving a plan (existing subscribers stay on it, no longer offered to new institutions) vs. deleting.

## Platform Staff

**Purpose**: manage the SaaS company's own internal users (mirrors the institution-level "Admins" page but one level up).

- List: name, email, role (SuperAdmin / Support / Billing / Sales — read-only vs. full-access roles), status, last login.
- Create/edit staff form: name, email, role, active/disabled toggle.

## Support

**Purpose**: a light internal ticket/issue queue so platform staff can track institution-reported problems without leaving the portal.

- Ticket list: institution, subject, status (Open/In progress/Resolved), priority, last updated.
- Ticket detail: message thread, internal notes, linked institution (jumps to Institution Detail), status change control.

## Announcements

**Purpose**: broadcast a message to some or all institutions (e.g. planned maintenance, new feature release).

- Compose form: Title, Body, Audience (All institutions / specific plan tier / specific institutions), Send now or schedule.
- History list of past announcements with delivery stats (how many institutions/admins saw it).
- Announcements surface inside the Institution Portal as a dismissible banner or a notification-panel entry.

## Audit Log

**Purpose**: platform-wide accountability trail — every sensitive action taken by any platform staff member (institution created/suspended/deleted, plan changed, impersonation started/ended, staff account created).

- Filterable table: actor, action type, target institution, timestamp.
- Row expand → detail of what changed (before/after where applicable).

## Settings

**Purpose**: global platform configuration.

- Default branding fallback (used before an institution sets their own).
- Default email sender identity for system emails.
- Global feature flag defaults for new institutions.
- API/webhook configuration for platform-level integrations (payment processor platform account, transactional email provider).

## Component Inventory (functional)

Paginated data table (sortable columns, row actions, bulk select), search-with-live-preview box, status badge, filter pill group, stat tile, trend chart, progress-against-limit bar, confirm-action dialog (destructive vs. neutral variants), multi-step wizard form (onboarding), tabbed detail page, side-panel/drawer for quick actions, toast notifications, file/image upload control, in-page banner (impersonation flag, announcement banner), rich text composer (announcements), CSV export action, audit-trail timeline.

## Experience Principles

- **Separate credential space.** A platform-staff login must never work on any institution's portal, and vice versa — except through the explicit, clearly-flagged impersonation tool.
- **Tenant boundary is invisible to everyone but platform staff.** Only this portal shows cross-institution data; nothing here should leak into what an institution or its members can see.
- **Impersonation is always visible and always attributed.** Anyone using "log in as" sees a persistent banner while impersonating, and every action taken is logged against the real platform-staff actor, not the impersonated identity.
- **Consistent patterns with the other two portals** for anything that repeats everywhere: paginated tables, confirm dialogs before destructive actions, toast feedback, empty/loading states, search-with-live-preview — reuse one shared pattern library rather than inventing new ones here.
