# Member Portal — Experience Spec

## Context

This is one of three portals in a multi-tenant alumni-engagement SaaS product:

1. **Platform Portal** — used internally by the SaaS company to onboard and manage the institutions (tenants) that subscribe to the product. (Separate document.)
2. **Institution Portal** — used by each subscribing institution's own staff to manage their members and engage their alumni community. (Separate document.)
3. **Member Portal** *(this document)* — used by an institution's alumni, once approved.

**Who uses the Member Portal**: an individual alumnus/alumna of one institution. This portal is tenant-branded — presented under each institution's own logo, colors, and name — and reached via that institution's own subdomain or custom domain. A member only ever sees their own institution's community; they never see any hint that other institutions exist on the platform.

This document covers pages, navigation, components, forms (fields only), and flows — not visual design (no colors, spacing, typography, or styling direction).

---

## Navigation

Sidebar: Dashboard, My Contributions, Job Board, Events, Alumni Directory, News, Forum, Mentorship, Resources, Leaderboard, Spotlights, Refer a Friend, Class Notes, Notifications, My Profile. Footer: account summary, Settings, Log out.
Mobile: bottom nav (Home, Pay, Jobs, Events, Profile) + slide-in drawer for the rest.
Header: notification bell.

## Auth

- **Login**: email + password.
- **Register**: guided multi-step signup — personal details → alumni details (class year, department, optional referral code, optional student ID) → set a password → email verification via a one-time code → "pending approval" waiting screen (with an optional "pay your dues now to speed things up" prompt).
- **Forgot password**: email → reset link → confirmation.

## Dashboard

- Membership status summary (active/inactive, expiring-soon warning, "you're all set" state).
- Outstanding-dues warning if applicable, with a clear path to pay.
- Quick stats: campaigns supported, total contributed, contributed this period, upcoming events.
- "Dues to pay" list and an "early renewal" list for getting ahead of next period.
- Open campaigns to support.
- Upcoming events preview.
- Recent payments preview.

## My Contributions

- Grid of open campaigns/dues you can pay, each showing progress, amount, deadline, and a Pay action (or a "paid" confirmation with a certificate link for membership dues).
- Full payment history table.
- A live status check while a payment is processing (for online payments), so the member isn't left wondering if it went through.
- **Campaign detail page**: full description, media, a flexible or fixed amount selector, the Pay action, a public share link, and — if the institution allows manual payments — the bank/mobile-money details to pay by transfer instead.

## Events

- Browse upcoming/past events, RSVP or cancel with one action.
- Event detail: full description, media gallery, venue/map link, RSVP state and capacity awareness ("X spots left" / "full").

## Job Board

- Browse postings with search/location/type filters.
- Detail page with an external "Apply" action and a clear deadline/closed state.

## Alumni Directory

- Search classmates by name/company/location/class year.
- Click a person to open a detail panel with their public profile info and a LinkedIn link if provided.

## News

- Browse announcements/news by category.
- Article detail with rich content and media.

## Forum

- Browse/search discussion threads by category, start a new thread, reply within a thread; closed threads are clearly marked read-only.

## Mentorship

- **Find a mentor**: browse available mentors, request mentorship with a short note.
- **My requests**: track the status of requests you've sent.
- **Incoming** (only if you're an approved mentor): accept/decline requests from mentees.
- **Become a mentor**: apply with your area of expertise and capacity; track your application's review status.

## Resources

- Browse a library of files/links with search, category, and type filters.
- Detail page with an inline preview where possible, a download/open action, and related-resource suggestions.
- Personal "save for later" list.

## Leaderboard

- Class-year rankings by membership rate, total contributed, and event attendance, with your own class year highlighted.

## Spotlights

- Browse featured alumni success stories.
- Submit your own story for review, and track your own submissions' status.

## Refer a Friend

- Your personal referral link/code with a copy action.
- Send an invitation by email directly from the page.
- Track your referral funnel (invited → registered → became an active member) and any referral-based recognition earned.

## Class Notes

- A wall-style feed scoped to your own class year: post an update, like others' posts, delete your own posts.

## Notifications

- Full notification inbox with category filters (jobs, events, campaigns, payments, class notes), mark-as-read actions, and "load more" pagination.

## My Profile

- Update your photo, contact/professional details, bio, and LinkedIn link.
- Employment status toggle (standard vs. a discounted-dues category), clearly flagged as a one-way choice where that's the business rule.
- Any badges/recognition you've earned.
- Change your password.

## Settings

- Account summary with a link to the full profile editor.
- Notification preferences by category (toggle each type of alert on/off).
- Security (password change).

## Membership Certificate

- View and download a certificate for each period you've paid dues, with a year selector if you have more than one.
- Certificate includes your details, the payment it's based on, and a verification reference.

## Public / Pre-Login Pages

- **Landing page**: institution-branded marketing/informational page introducing the alumni platform, with sign-in and join calls to action.
- **Guest membership activation**: a direct-link page (e.g. from an invitation) letting someone pay dues before their account is fully approved, followed by a status page confirming activation and inviting them to sign in.
- **Public campaign page**: a shareable, no-login-required donation page for a specific campaign, with a simple contribute action and social-share links — useful for reaching non-members too (e.g. friends/family of alumni, or the wider public for a fundraising push).

## Component Inventory (functional)

Paginated/searchable/filterable listing grids, detail drawers/panels, confirm-action dialogs, forms with inline validation feedback, image upload (avatar), rich content viewer, media gallery with full-view/lightbox, progress bars, status/category badges, notification bell + panel, toast messages, tabbed sections, expandable "read more" text blocks, live-updating payment-status indicator, copy-to-clipboard actions, social-share actions, certificate viewer with a downloadable-file action, empty states for every list, loading placeholders for every list/card/table.

## Experience Principles

- **Tenant branding is the first impression.** This portal should feel like it belongs to the member's own institution — logo, name, and color identity should be present from the very first (pre-login) screen, not just after signing in.
- **The approval wait shouldn't feel like a dead end.** A newly-registered member sits in a pending state until approved (or until their membership payment auto-approves them) — the pending screen should give them something useful to do (pay dues, understand what's next) rather than just "please wait."
- **Payment status should never feel uncertain.** Because online payments involve a redirect to an external payment page and back, the return experience needs a clear, live-updating confirmation rather than leaving the member guessing whether it worked.
- **Community features assume an approved, active member** — directory, forum, mentorship, class notes, and leaderboard are all about connecting with real classmates, so their empty/first-use states should acknowledge a fresh institution may not have much activity yet.
