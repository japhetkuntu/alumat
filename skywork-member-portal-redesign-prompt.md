# Redesign Brief: Alumni Member Portal (for Skywork.ai)

## Context

Redesign the **Member Portal** of a multi-tenant university alumni platform (white-labeled per institution — each institution has its own logo, primary brand color, and custom auth headline/photo). This is a web app (desktop + mobile responsive), not a marketing site. The audience is university alumni: paying dues, networking, finding jobs, attending events, mentoring, and staying connected to their graduating class.

**Do not invent new data fields.** Every field, page, and action listed below is exhaustive and matches the real backend data model. If you want to propose a genuinely new feature, list it separately as a "suggested addition" rather than blending it into the core redesign.

**Preserve white-label theming.** Colors must NOT be hardcoded. The entire palette (backgrounds, borders, accents, status colors) is generated at runtime from one institution `primaryColorHex` value. Deliver the design as a token system (CSS variables or equivalent: `--primary`, `--background`, `--foreground`, `--border`, `--muted`, `--success`, `--warning`, `--destructive`, etc.) so any institution's brand color can be swapped in without touching layout.

**Typography**: two-font system — a sans-serif for body/UI text and a serif/display font for headings and editorial accents (currently Inter + Lora). Keep this pairing model even if you choose different specific fonts.

**Border radius scale**: xs 4px / sm 8px / md 12px / lg 18px / xl 24px. Cards typically use the largest radius (24px, soft/rounded feel, not sharp corners).

---

## Global Navigation

**Desktop**: persistent left sidebar (~240px), collapsible to a drawer on mobile. Items in this exact order:

1. Dashboard
2. My Contributions
3. Job Board
4. Events
5. Alumni Directory
6. News
7. Forum
8. Communities
9. Mentorship
10. Resources
11. Leaderboard
12. Spotlights
13. Refer a Friend
14. Class Notes
15. Notifications
16. My Profile

Each item has an icon. Any item except Dashboard/Notifications/Profile can be hidden per-institution (feature flags), so the redesign must handle a variable-length nav gracefully (not assume all 16 items always show).

Sidebar footer: user avatar (photo or initials) + name + email, a **Settings** button, and a **Log out** button. (Settings is currently only reachable from here — consider whether it deserves a more prominent spot in the redesigned nav.)

**Mobile**: fixed bottom nav bar with 5 items only — Home, Pay, Jobs, Events, Profile — plus a hamburger menu (top-right) that opens the full sidebar as a drawer, and a notification bell (top area) next to the institution logo.

**Top bar**: minimal — just a notification bell icon with unread-count badge, opening a dropdown of the 20 most recent notifications ("Mark all read" action, auto-refreshing). No global search bar and no breadcrumbs currently exist — you may propose adding a global search if it improves the design, but flag it as a proposed addition.

---

## Design System / Components Needed

Deliver a component library covering (all must support light theme; dark mode is a stretch goal, not required):

- **Buttons**: primary, secondary, destructive, ghost/text, icon-only — with loading state (spinner + disabled)
- **Inputs**: text, email, password (with show/hide toggle), number, date, url, tel, textarea, single-select dropdown, multi-step OTP input (6 separate digit boxes with auto-advance)
- **Cards**: generic content card, stat/metric card, profile/member card, campaign card, event card, job card, resource card
- **Badges/pills**: status badges (success/warning/error/neutral/info variants), category pills, filter pills (toggleable)
- **Avatar**: photo with graceful fallback to initials-on-color-circle
- **Modals/dialogs**: standard dialog, a destructive confirmation modal, a bottom-sheet/drawer (mobile-friendly)
- **Tables**: sortable-looking data table with a mobile card-list fallback
- **Pagination**: page-number style, reusable across every list page
- **Empty states**: icon + message + optional CTA button
- **Skeleton loaders**: for cards and stat tiles
- **Toast notifications**: success/error
- **Progress bar**: for campaign funding % and multi-step wizards
- **Tabs**: pill-style tab switcher used within several pages
- **Media gallery**: mixed image + embedded YouTube video grid
- **Rich text renderer**: for news/announcement bodies (headings, bold/italic, links, images embedded in copy)

---

## Page-by-Page Specification

### AUTH PAGES (no sidebar — split-screen layout: left = photo panel with institution logo + editorial quote, hidden on mobile; right = centered form, max-width ~480px)

#### 1. Login (`/login`)
Fields:
- **Email address** — email input, placeholder "name@example.com", required, must be valid email format
- **Password** — password input with show/hide eye-icon toggle, placeholder masked dots, required, min 8 characters. Inline "Forgot password?" link next to the label.

Actions: **Sign in** primary button (full width, shows "Signing you in..." loading state). Footer link: "New here? Create an account."

#### 2. Register (`/register`) — multi-step wizard
Three top-level steps shown via a step indicator (numbered circles connected by a progress bar): **Your details → Verify email → Activate**.

**Step 1 has 3 sub-steps** (shown via smaller progress dots):

*Sub-step 1a — Personal details:*
- First name — text, required, min 2 chars, placeholder "Kwame"
- Last name — text, required, min 2 chars, placeholder "Mensah"
- Email address — email, required, valid format, placeholder "you@example.com"
- Phone — tel, optional, placeholder "+233 XX XXX XXXX"
- Button: "Continue"

*Sub-step 1b — Alumni information:*
- Student ID — text, placeholder "ENG/20/0001" (required or optional depending on institution config — label should show "(optional)" suffix dynamically when not required)
- Graduation year — dropdown select, populated from a list of class years (1952–current year), required
- Department — dropdown select, optional, only shown if the institution has departments configured
- Referral code — text, optional, placeholder "e.g. KWAMEN-A1B2C3" (pre-filled and locked/read-only when the user arrived via a referral link)
- Buttons: "Back", "Continue"

*Sub-step 1c — Secure your account:*
- Password — password input with show/hide toggle, placeholder "Min. 8 characters", min 8 chars required
- Confirm password — password input with show/hide toggle, placeholder "Repeat your password", must match Password field (inline error "Passwords do not match")
- Buttons: "Back", "Create account" (loading state "Creating account…")

**Step 2 — Verify email (OTP):**
- 6 separate single-digit numeric input boxes, auto-advance focus on entry, backspace moves to previous box, supports pasting a 6-digit code across all boxes
- "Verify & continue" button, disabled until all 6 digits entered
- "Resend" link — allows up to 3 resends, then disables with "No resend attempts left" text
- "Back to registration" link

**Step 3 — Pending/Activation:**
- Status checklist: "Email verified" (checked) and "Awaiting admin review" (usually within 24 hours)
- **Membership activation card**: if the institution has an active membership campaign, show banner image, "Membership {year}" label, campaign title, amount per member, description, closing date + days-remaining countdown, and an "Activate — {amount}" CTA button that leads to the guest payment page. If no active campaign, show a neutral "No active campaign yet" message.
- Footer buttons: "Register another account" / "Go to sign in"

#### 3. Forgot password (`/forgot-password`)
Fields:
- **Email address** — email input, required, valid format, placeholder "you@example.com"

Actions: "Send reset link" button (loading state "Sending"). On success, the form is replaced with a confirmation state (checkmark icon, "Check your email" heading, generic "if an account exists…" message that deliberately does not confirm/deny account existence, "Back to sign in" link).

*Note: there is currently no separate "reset password" page in the product — the reset flow after clicking the emailed link should be designed as a new page: a simple form with "New password" + "Confirm new password" fields, following the same password-field pattern as elsewhere, submitting to complete the reset.*

---

### PORTAL PAGES (all wrapped in the sidebar layout)

#### 4. Dashboard (`/dashboard`) — home/landing screen
- Personalized greeting: "Welcome back, {first name}."
- **Membership card** (hero element, styled like a physical membership/ID card): member name, "Class of {year} · {department}", status pill (Active/Inactive). If active: "Valid until {date}" + "View certificate" link. If inactive with unpaid dues: warning message + amount owed (with pensioner-rate note if applicable) + due date + "Pay now" button. If active but expiring within 30 days: expiry warning banner + "Renew" button. Otherwise: "You're all set for this year" message.
- **Arrears banner** (amber warning, shown only if the member owes dues from prior years): lists the unpaid years as pills + "Clear arrears" button.
- **4 stat tiles** (2 columns mobile, 4 desktop), each with icon + large number + label: Active campaigns (count), Total contributed (currency, all-time), This year (currency, current-year total), Upcoming events (count).
- **"Dues to pay" section** (only if unpaid current-year dues exist): rows of campaign title, amount, due date, "Pay now" button.
- **"Early renewal" section** (future-year membership campaigns not yet paid): rows with "View" button.
- **"Open campaigns" card** (non-membership fundraising campaigns): title, per-member suggested amount, progress bar with % of target raised, due date, "All" link.
- **Two-column layout**: "Upcoming events" card (up to 3 events: title, date, venue, Going/Open badge, "All" link) and "Recent payments" card (up to 5 payments: campaign, date, amount, status badge Confirmed/Pending/Rejected, "History" link).
- No forms on this page — entirely read-only summary + action buttons.

#### 5. Profile (`/profile`)
- **Identity card**: avatar with a camera-icon overlay for photo upload (accepts image files), name, email, "Class of {year}" badge, status badge.
- **Badges card** (conditional — only shown if the member has earned any, e.g. a "Referrer Badge").
- **Employment status toggle**: two large selectable option cards — "Employed" vs "Pensioner". Switching to Pensioner triggers a destructive confirmation modal ("This is permanent…", confirm label "Yes, I am a pensioner") and is irreversible once set.
- **Professional info form**:
  | Field | Type | Required |
  |---|---|---|
  | Company | text | No |
  | Job title | text | No |
  | Location | text | No |
  | Phone | text | No |
  | LinkedIn URL | url | No |
  | Bio | textarea (3 rows) | No |

  Submit button: "Save changes" (loading state "Saving…").
- **Change password form** (separate section):
  | Field | Type | Required |
  |---|---|---|
  | Current password | password w/ show-hide toggle | Yes |
  | New password | password | Yes |
  | Confirm new password | password | Yes, must match New password |

  Submit button: "Change password", disabled until valid.

#### 6. Settings (`/settings`)
- **Account card**: avatar, name, email, "Active Member" + "Class of {year}" badges, "Edit Profile" button linking to Profile page; if professional info is filled in, show it in a small detail grid.
- **Notification preferences card** — 8 independent toggle switches, each saves immediately on change:
  1. Membership Reminders — "Reminders about upcoming membership renewals"
  2. Campaign Alerts — "Notifications when new campaigns are launched"
  3. Event Reminders — "Get notified about upcoming alumni events"
  4. Job Alerts — "Notifications for new job postings"
  5. Class Notes — "Notifications when classmates post to your year group wall"
  6. Spotlight Updates — "Get notified about new alumni spotlights"
  7. SMS Notifications — "Also send important alerts via SMS"
  8. WhatsApp Notifications — "Also send important alerts via WhatsApp"
- **Security card**: same password-change form as Profile.
- **About card**: static app/institution name + link to full profile.

#### 7. My Contributions (`/contributions` list, `/contributions/[id]` detail, `/contributions/callback` payment redirect)

**List page**:
- Grid of active campaign cards: banner image (with "Membership" badge overlay if applicable), title, description, progress bar (% of funding target, or % of members paid for membership campaigns), amount-due box (with pensioner-rate note where relevant), a "Membership paid" confirmation banner where applicable, a pending-payment "Check status" button where applicable, and action buttons: "View details", "Certificate" (only if a paid membership exists), "Pay {amount}".
- **Payment history**: total-confirmed-amount stat, then a table (desktop) / card list (mobile) with columns **Campaign | Amount | Method | Status | Date**, paginated.
- **Payment status modal**: appears while a payment is pending — polls status every 10 seconds, shows loading/pending/success/error states with icon, "Check again (countdown)" and "Done" buttons.

**Detail page** (`/contributions/[id]`):
- Campaign title, meta (deadline, funding goal, contributor count), banner image or embedded video, progress bar, full description.
- "Share this campaign" box: public shareable link + "Copy" button + "Preview" link.
- Payment sidebar: for flexible-amount campaigns, 3 quick-pick multiplier buttons (1×/2×/5× the suggested per-member amount) plus a custom amount number field (min 0.01, 2 decimal places); for membership campaigns, amount is fixed and shown read-only. "Pay {amount}" button initiates payment.
- **Manual payment instructions box** (only shown if the campaign allows manual/offline payment): member reference number, bank transfer details (account name, account number, bank, branch) and/or mobile money details (provider, name, number) — read-only reference info, not an interactive form. *(Note: there is currently no way for a member to upload proof of a manual payment in the UI even though the backend supports it — consider designing an "Upload payment proof" file-upload affordance here as a suggested improvement.)*

**Callback page**: post-payment redirect landing page — polls payment status every 10s, shows status card + "Check status" / "Back to Contributions" buttons.

#### 8. Events (`/events` list, `/events/[id]` detail)

**List**: grid of event cards — banner image with status badge (Upcoming/Ongoing/Completed/Cancelled) and an RSVP-confirmed checkmark overlay, date + price overlay, title, venue, attendee count + spots-remaining, and an "RSVP" / "Cancel RSVP" button (or "Registration closed" label when full/past).

**Detail**: title, date/venue/attendee-count meta row, banner image, RSVP sidebar (price or "Free" tag, "You're going" confirmation banner if already RSVP'd, "Confirm RSVP" button opens a confirmation modal, "Cancel my RSVP" button), details list (venue with a "view on map" link, date, attendance count), full description, and a photo/video gallery section (if the event has media attached).

#### 9. Job Board (`/jobs` list, `/jobs/[id]` detail)

**List**: filter bar with Search text field (title/company), Location text field, and type filter pills (All/Full-time/Part-time/Contract/Internship), plus a "Clear filters" action. Grid of job cards: type-color-coded pill, title, company, location, application deadline (or "Closed"), description preview, "View details" link.

**Detail**: banner or company-name placeholder, title, meta row (company/location/posted date/deadline), full description. Sidebar: "Apply now" external-link button with a days-remaining countdown if an external application URL exists, or a "No application link" / "Applications closed" notice otherwise. Details list (company, location, job type, posted date, deadline).

#### 10. Alumni Directory (`/directory`)
- Filters: Search text field (name/company/location), Graduation year dropdown ("All graduation years" + each class year), "Clear" action.
- Grid of member cards: avatar, name, job title @ company, location, "Class of {year}" + department badges, LinkedIn icon link.
- Clicking a card opens a **profile detail panel** (right-side panel on desktop, bottom sheet on mobile): avatar, name, job title, class/department badges, company/location/bio, "View LinkedIn profile" button.
- No forms — pure browse/filter/view experience.

#### 11. News (`/news` list, `/news/[id]` detail)
- List: category filter pills (All/Announcement/Achievement/News/Event/Opportunity), grid of cards (banner, "pinned" badge, category badge, date, title).
- Detail: pinned + category badges, title, date + author, hero image, rich-text article body (headings/bold/links/inline images), photo/video gallery for additional attached media.
- No forms — read-only content.

#### 12. Forum (`/forum` list, `/forum/[id]` thread detail)

**List**:
- "New thread" button reveals an inline form:
  | Field | Type | Required |
  |---|---|---|
  | Category | dropdown | Yes |
  | Title | text, placeholder "What would you like to discuss?" | Yes |
  | Content | textarea (4 rows), placeholder "Share the details…" | Yes |

  Buttons: "Post thread", "Cancel".
- Search field ("Search threads…"), sort filter pills (All/Recent/Popular/Pinned), dynamic category filter pills.
- Thread rows: category badge, Pinned/Closed badges, title, date, reply count. Paginated.

**Detail**: back link, thread header (badges, title, date, reply count), paginated list of posts (avatar, author name, "OP" badge on the original post, timestamp, content). Reply box at the bottom (textarea + "Post reply" button, disabled until non-empty) — replaced with a "This thread is closed" notice if the thread is closed.

#### 13. Communities (`/communities` list, `/communities/[id]` detail)

**List**: cards showing name, description, member count, and a status action (Request to join / Member badge / Request pending badge / "You lead this" badge).

**Detail**: header (name, leader badge, description, member count, join/leave/pending action). If the member isn't an approved member yet, show a "Members only" locked state. Once approved, show **3 tabs**:
- **Discussion**: "New thread" button → inline form (Title text field, Message textarea, Post/Cancel buttons) → creates a community-scoped forum thread; list of existing threads.
- **Members**: member list (avatar, name, Leader badge); community leader can remove non-leader members.
- **Requests** (leader-only, badge shows pending count): pending join requests (avatar, name, requested date) with Approve/Reject buttons.

#### 14. Mentorship (`/mentorship`)
Tab nav: **Find a mentor / My requests / Incoming / Become a mentor** (Incoming tab only visible to members who are themselves approved mentors, with a pending-count badge).

- Status banner if the member has a mentor profile: area of expertise, mentee count, status badge (Approved/Pending/Rejected).
- **Find a mentor**: grid of mentor cards (avatar, name, area, Available/Full badge, mentee count, bio, "Request mentorship" / "Mentor full" / "Cancel" button). Selecting a mentor reveals a request form:
  | Field | Type | Required |
  |---|---|---|
  | Area of interest | text, placeholder "e.g. Career guidance, Mining Engineering…" | Yes |
  | Message | textarea (3 rows), placeholder "Introduce yourself and what you hope to gain…" | No |

  Buttons: "Send request", "Cancel".
- **My requests**: list of sent requests (area, mentor name, date, message, status badge Accepted/Pending/Rejected/Completed).
- **Incoming**: list of received requests (mentee avatar/name, area, date, message); pending ones get Accept/Decline buttons, each opening a confirmation modal.
- **Become a mentor**: registration form (or, if already applied, a static status card / rejection notice with a "Resubmit" option):
  | Field | Type | Required |
  |---|---|---|
  | Area of expertise | text, placeholder "e.g. Mining Engineering, Environmental Science…" | Yes |
  | Background | textarea (4 rows) | No |
  | Maximum mentees at one time | number, min 1, max 10, default 3 | Yes |

  Button: "Submit application" / "Resubmit application".

#### 15. Resources (`/resources` list, `/resources/[id]` detail)
- Search modal (command-palette style, live-filtered results).
- Filters: Type dropdown (All types/PDF/Video/Link/Document/Image/File), "Added after" date field, "Added before" date field, category pills (All/Career/Professional/Scholarship/Technical/General/Other), "Clear filters" action.
- Cards: banner or file/link-type icon placeholder, category + type badges, title, description, download count, added date, "View Details" button + an icon-only download/open button.
- Detail: breadcrumb, hero banner, category/type badges, title/meta, "Share" and "Save" actions, inline preview appropriate to file type (video embed / PDF viewer / image / external-link card), description, stats (downloads, published date), primary "Download Resource" / "Open Resource Link" CTA, and a "Related Resources" section (same category, up to 3).

#### 16. Leaderboard (`/leaderboard`)
Read-only ranked list of graduating-class cohorts: rank (medal icon for top 3), "Class of {year}" (highlighted if it's the current member's own class), member count, and three stat columns: **Membership %**, **Contributed (currency total)**, **Events attended (count)**. No forms.

#### 17. Spotlights (`/spotlights`)
Tabs: **Featured stories / My submissions**.
- Featured: cards (avatar, name, class year, featured-month badge, title, story text with a "Read full story" expand toggle, optional banner image); the first card is visually emphasized as "featured."
- My submissions: list with status indicator (Pending "Under review" / Approved "Published" / Rejected "Not selected").
- **"Share my story"** button opens a submission drawer (bottom sheet):
  | Field | Type | Required |
  |---|---|---|
  | Spotlight title | text, placeholder "e.g. From campus to Silicon Valley" | Yes |
  | Your story | textarea (6 rows), placeholder "Tell us about your journey…" | Yes, min 20 characters, live character counter |

  Button: "Submit for review".

#### 18. Refer a Friend (`/referrals`)
- 3 stat tiles: Total invited / Registered / Pending.
- Referral code box: read-only selectable code + "Copy" button.
- Referrer badge indicator (if earned).
- **Send invitation form**:
  | Field | Type | Required |
  |---|---|---|
  | Email address | email, placeholder "colleague@example.com" | Yes |

  Button: "Send invite" (also submits on Enter key).
- Referral history list: referred person's name/email, date, status badge (Pending/Registered/Activated).

#### 19. Class Notes (`/class-notes`)
Facebook-wall-style feed scoped to the member's own graduating class.
- Composer: avatar + textarea (placeholder "What's on your mind, Class of {year}?", 3 rows, 1000-character limit with a live counter that turns warning-colored under 50 remaining), "Post" button (disabled until non-empty).
- Feed: note cards (avatar/initials, author name, year-group tag, relative timestamp, content with "Read more/Show less" for long posts, optional attached image, Like toggle with heart icon + count, inline Delete for the author's own posts with an inline "Delete this? Yes/Cancel" confirmation).
- Paginated.

#### 20. Notifications (`/notifications`)
Full notification center (as opposed to the top-bar dropdown's fixed 20 items — this page paginates/loads more).
- Header: unread count, "Mark all read" button.
- Filter tabs: All / Unread (with count) / Jobs / Events / Campaigns / Contributions / Class notes.
- Rows: type-specific color-coded icon, type label, unread indicator dot, relative timestamp, title (bold if unread), 2-line body preview, inline "Mark as read" action for unread items; the whole row is clickable and deep-links to the related content.
- "Load more" button.

#### 21. Membership Certificate (`/membership-certificate`)
- Year selector pills, if the member has paid for multiple membership years.
- **Certificate artwork** (downloadable as PDF): decorative bordered card with institution logo/name, "Certificate of Membership" title, membership year, member name, Member ID, "Class of {year}", amount, paid-on date, a "Payment Verified & Confirmed" badge, and a transaction reference footer. "Download PDF" button.
- Payment details card: grid of Campaign / Membership year / Amount paid / Payment method / Payment date / Transaction reference / status badge / active-or-expired line.
- Empty state ("No certificates yet" + "Browse campaigns" button) if the member has no paid memberships.

---

### PUBLIC PAGES (no sidebar/nav — standalone, reachable without login, used for sharing/guest payment links)

#### 22. Guest Membership Activation (`/activate-membership/[campaignId]`)
- Campaign hero (banner, title, description, Amount/Deadline/Paid-count stat tiles, days-remaining warning, or an "expired" notice).
- Static "What happens after payment?" checklist.
- **Payment form**:
  | Field | Type | Required | Notes |
  |---|---|---|---|
  | Email | email | Yes | read-only, pre-filled from the invite link, shown with a lock icon |
  | Amount (currency) | text | — | read-only, fixed to the campaign's per-member rate |

  Button: "Pay {amount}" (disabled if online payments aren't enabled for this campaign, if it's expired, or already pending).
- Footer: "Already have an account? Sign in" link.

#### 23. Activation Payment Callback (`/activate-membership/callback`)
Polls payment status; shows loading/success/pending/error states. On success: displays the newly-assigned membership number, a "what's next" checklist, and a "Sign in to your account" button (pre-filling the email). On pending/error: "Check again" / "Try again" buttons and a support-email link.

#### 24. Public Campaign Contribution Page (`/payment-campaign/[campaignId]`)
Shareable public fundraising page (no login required).
- Campaign hero (banner, status/membership badges, title, description, optional embedded video).
- Progress card: Raised / Target / Backer-count stat tiles, % funded bar, deadline.
- **Contribution form**:
  | Field | Type | Required | Notes |
  |---|---|---|---|
  | Amount (currency) | number, min 0.01, step 0.01 | Yes (unless fixed) | locked/read-only for membership campaigns |
  | Email | email | No | behind a collapsible "Add your email (optional)" disclosure; used only to send a receipt |

  Button: "Pay {amount}" (disabled while a payment is pending or the amount is invalid) — opens a live "Processing payment" status dialog, then redirects to the payment provider.
- "Are you a member? Sign in for full credit" link.
- Share card: copy-link field + WhatsApp/Twitter/Facebook share buttons.

#### 25. Error / Not Found
- Global error boundary: "Something went wrong" message + "Try again" button.
- 404 page: not-found message + "Back to Dashboard" link.

---

## Design Priorities

1. **Trust and clarity around money.** Contributions, membership dues, and payment status are core flows — amounts, statuses (Confirmed/Pending/Rejected), and deadlines must be unmistakably clear at a glance, using consistent status-badge colors throughout the whole app.
2. **Mobile-first for high-frequency actions** (RSVP, pay dues, check notifications, post to class notes) — assume a large share of usage is on phones via the bottom nav.
3. **Community warmth.** This is an alumni social product, not just a transactional dashboard — class notes, spotlights, forum, and communities should feel warm/social, distinct visually from the more utilitarian dues/certificate/settings screens.
4. **White-label flexibility.** Every institution reskins this with their own brand color and logo — avoid any design choice that only works with one specific color; validate your design against at least two very different accent colors (e.g. a deep blue and a warm orange) to confirm it holds up.
5. **Empty and loading states matter.** Nearly every list page needs a designed empty state (no campaigns, no events, no notifications, etc.) and a skeleton-loading state, not just a bare "no data" text.

## Deliverables Requested

- Full design system (tokens, typography scale, spacing scale, component library per the list above)
- High-fidelity screens for all 25 pages/flows listed above, desktop + mobile
- At least one alternate brand-color theme applied to the same screens, to prove the token system works
- Redlines/specs sufficient for a React/Tailwind engineering team to implement (this will be built with Next.js, Tailwind CSS, and Radix-based components)
