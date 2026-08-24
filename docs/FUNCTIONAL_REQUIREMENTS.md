# Alumni Platform — Full Functional & Technical Requirements Specification

**Purpose of this document:** This is a complete, exhaustive inventory of the existing single-tenant Old Student / Alumni platform (originally built for one institution) — every page, every component, every form field, every API endpoint, every database table — extracted directly from the current codebase. It is intended as the single source of truth for rebuilding the platform from scratch as a multi-tenant SaaS product on a different technology stack.

**How this document is organized:**
1. Platform Overview
2. Part 1 — Member Portal (public-facing alumni web app)
3. Part 2 — Admin Portal (back-office web app)
4. Part 3 — Backend (APIs, database schema, integrations)
5. Cross-Cutting Rebuild Considerations (consolidated)

---

# 1. Platform Overview

The system is a university alumni-association management platform ("Old Student / Alumni Portal") consisting of:

- **Member web app** (Next.js) — self-service portal for alumni: registration/approval, membership dues & renewal, campaign/event/job/news/forum/directory/mentorship/resources/leaderboard/spotlights/referrals/class-notes, notifications, profile/settings, membership certificate.
- **Admin web app** (Next.js) — back-office for staff: member approval/moderation, campaign & contribution management, membership renewal campaigns, content moderation (jobs/events/news/forum/resources/spotlights/mentorship), reporting/exports, notifications, admin user management, settings.
- **Backend** (.NET 8 / ASP.NET Core, PostgreSQL, Redis, S3-compatible storage, Paystack, SMTP) split into two REST APIs (Member API, Admin API) sharing one database.

Core business domain: a university alumni association that collects annual membership dues (with pensioner discount), runs fundraising campaigns, and provides community features (events, jobs, forum, mentorship, class notes, directory) to paid/active members, gated by an admin-approval workflow tied to Paystack online payments.

---

# PART 1 — MEMBER PORTAL (Alumni-Facing Web App)

Source: Next.js App Router, TypeScript, TanStack Query, react-hook-form + zod, Tailwind CSS (CSS-variable theme), Radix UI primitives, axios API client, Paystack payments, TipTap rich text (viewer only), html2canvas/jsPDF certificate generation.

## 1.0 Global Architecture

### Layouts
- **Root layout**: loads Inter + Lora fonts, wraps everything in `Providers`. PWA metadata (manifest.json, apple-web-app).
- **Auth layout** (`(auth)`): split-screen — left 42–48% width photo panel with branding + testimonial (desktop only); right panel centers the form (max-width 480px). Used by Login, Register, Forgot Password.
- **Portal layout** (`(portal)`): wraps children in the authenticated `MemberLayout` shell (sidebar/topbar). All member-only pages live here.
- **Providers stack**: `QueryClientProvider` (staleTime 60s, retry 1) → `AuthProvider` → `TooltipProvider` (350ms delay) → `Toaster` (sonner, rich colors, top-right).

### Full Route Inventory
- **Auth (public, unauthenticated):** `/login`, `/register`, `/forgot-password`
- **Portal (requires member auth):** `/dashboard`, `/contributions`, `/contributions/[id]`, `/contributions/callback`, `/events`, `/events/[id]`, `/jobs`, `/jobs/[id]`, `/directory`, `/news`, `/news/[id]`, `/forum`, `/forum/[id]`, `/mentorship`, `/resources`, `/resources/[id]`, `/leaderboard`, `/spotlights`, `/referrals`, `/class-notes`, `/notifications`, `/profile`, `/settings`, `/membership-certificate`
- **Public, no auth required:** `/` (marketing landing page), `/activate-membership/[campaignId]`, `/activate-membership/callback`, `/payment-campaign/[campaignId]`
- **System:** `/error` (global error boundary), `/not-found` (404)

---

## 1.1 Authentication

### Auth mechanism
- React Context (`useAuth`) holds `{ user, tokens, isLoading, login, logout, isMember }`.
- Hydrates from `localStorage["user"]` and `localStorage["tokens"]` on mount (guarded against corrupted JSON).
- **Login**: `POST /auth/login {email, password}` (unauthenticated axios instance) → `{data:{user, tokens}}`. Persists to state + localStorage keys `user`, `tokens`, plus flattened `access_token`, `refresh_token`.
- **Logout**: clears all 4 localStorage keys, hard-redirects to `/login`.
- `isMember = user?.role === "Member"`.

### HTTP client behavior
- `memberClient` — authenticated axios instance, base URL `NEXT_PUBLIC_MEMBER_API_URL` (default `http://localhost:5200/api/v1`), 30s timeout.
  - Request interceptor attaches `Authorization: Bearer <access_token>`.
  - Response interceptor: on 401, attempts silent token refresh (`POST /auth/refresh {accessToken, refreshToken}`), queues concurrent failed requests during refresh, retries originals on success, else clears auth and hard-redirects to `/login`.
- `publicMemberClient` — separate unauthenticated instance (15s timeout) for guest flows (registration, guest payments, activation status).
- `handleApiError(error)` — extracts `.errors[].errorMessage` joined by comma, else `.message`, else generic fallback; used for all error toasts app-wide.

### Login page (`/login`)
- **Fields**: Email (email input, validated), Password (password input, min 8 chars, show/hide toggle).
- Prefills email from `?email=` query param (used after registration/activation redirects).
- Submit → `login()` → success: redirect to `/dashboard`, toast "Welcome back!"; error: toast via `handleApiError`.
- Links: "Forgot password?" → `/forgot-password`; "Create an account" → `/register`.

### Register page (`/register`) — multi-step wizard
Top-level steps: `form` (3 sub-steps) → `otp` → `pending`.

**Sub-step 1 — Personal details**: First name (min 2), Last name (min 2), Email (valid), Phone (optional). "Continue" validates only these.

**Sub-step 2 — Alumni information**: Student ID (required), Graduation year (dropdown 1952–current year, populated with departments if loaded), Department (optional dropdown, from `getDepartments()`), Referral code (optional; prefilled + locked if `?ref=` query param present). "Continue" validates studentId + graduationYear.

**Sub-step 3 — Secure your account**: Password (min 8, show/hide), Confirm password (must match, show/hide). "Create account" submits the full form.

- **Submit** → `POST /auth/register` (unauthenticated) with full payload → success moves to OTP step, resets resend counter to 3, success toast.
- **OTP step**: 6 individual digit boxes (auto-advance, backspace, paste support). "Verify & continue" → `POST /auth/verify-otp {email, otp}` → success moves to `pending` step. "Resend" → `POST /auth/resend-otp {email}`, decrements a 3-use counter (disabled at 0).
- **Pending step**: checklist (Email verified ✓ / Awaiting admin review ⏳). Fetches `getCurrentMembershipCampaign()` (public) and if found, shows a "pay now to speed up approval" card linking to `/activate-membership/[campaignId]?email=...`. Buttons: "Register another account" (reset wizard) / "Go to sign in".

### Forgot Password page
- Single field: Email (valid). Submit → `POST /auth/forgot-password {email}`. On success, form is replaced with a "Check your email" confirmation card.

---

## 1.2 Member Portal Shell (`MemberLayout`)

**Auth guard**: if not loading and not a member and not already on `/login`, redirects to `/login`; renders nothing while loading/unauthenticated.

### Sidebar navigation (desktop, 240px)
In order: Dashboard, My Contributions, Job Board, Events, Alumni Directory, News, Forum, Mentorship, Resources, Leaderboard, Spotlights, Refer a Friend, Class Notes, Notifications, My Profile.
Active state = exact match or path prefix match. Footer: avatar/name/email, "Settings" button, "Log out" button.

### Mobile bottom nav (5 items)
Home, Pay (Contributions), Jobs, Events, Profile — floating pill bar with active highlight.

### Header
Desktop: right-aligned notification bell only. Mobile: logo+title left, notification bell + hamburger (opens slide-in drawer with backdrop) right.

### Notification bell / panel
- Unread-count badge (polls every 30s, "99+" cap).
- Click toggles dropdown (closes on outside click).
- Header: title, unread count chip, "Mark all read" button, close button.
- Rows: unread dot, title (bold if unread), 2-line body, relative time, hover "mark read" action.

---

## 1.3 Portal Pages — Full Detail

### `/dashboard`
**Purpose**: home base — membership status, dues owed, campaigns, upcoming events, recent payments.

**Data**: my campaigns, my contributions, upcoming events (status=Upcoming), my RSVPs, membership status (`{isMembershipActive, membershipExpiry, membershipYearsPaid, lastMembershipPaidAt, isCurrentYearPaid, hasArrears, arrearsCount, arrearsYears}`), current-year unpaid membership campaigns, my profile.

**UI**:
- Greeting header.
- **Membership card**: gradient (green=active/gray=inactive), name, class year+department, Active/Inactive badge; if active and expiring ≤30 days → countdown + Renew button; if active & no warning → "You're all set"; "View certificate" link if active; if inactive with an unpaid campaign → amount due (pensioner-aware) + Pay now button.
- **Arrears banner** (only if active membership + has arrears): amber card listing unpaid-year badges + "Clear arrears" button.
- **4 stat tiles**: Active campaigns, Total contributed (all-time confirmed), This year contributed, Upcoming events count.
- **Dues to pay** section: unpaid current-year membership campaigns as rows (title, pensioner-aware amount, due date, Pay now).
- **Early renewal** section: future membership-year campaigns not yet paid (View, not Pay).
- **Open campaigns card**: non-membership active campaigns with % progress bars.
- **Upcoming events card**: first 3, icon/title/date+venue/RSVP badge; empty state.
- **Recent payments card**: last 5 contributions (campaign/date/amount/status badge); empty state with CTA.

Business rule: `isPensioner = profile.employmentStatus === "Pensioner"`; amount charged = `pensionerAmountPerMember` if set and pensioner, else `amountPerMember`.

### `/contributions` (campaigns + payment history)
**Data**: my active campaigns, paginated contributions (page 20), all contributions (for status matching, 500), my profile.

**UI**:
- **Active campaigns grid**: card per campaign — banner (+ "Membership" pill), title/description, status badge, progress bar (membership: paid/eligible; else collected/target), amount+deadline row, "Membership paid" banner if applicable, "payment processing" row for Pending payments (opens status modal), action buttons: View details, Certificate (if membership paid), Pay {amount} (if online payments allowed and not yet paid).
- **Payment history**: table (desktop) / stacked cards (mobile) — Campaign, Amount, Method, Status, Date; total-confirmed sum shown; paginated.
- **Payment status modal**: polls payment status every 10s with visible countdown; states loading/pending/success/error; "Check again" / "Done" buttons; auto-opens if a pending Paystack reference exists in localStorage.

**Payment logic**: membership campaigns call `renewMembership`; regular campaigns call `initiatePaystackPayment`. On success, stores the Paystack reference locally and redirects the browser to Paystack's hosted checkout.

### `/contributions/[id]` (campaign detail + pay)
Two-column layout. Left: title/meta, banner or YouTube embed, full description, public share link + copy button + preview link. Right (sticky): progress, amount/deadline tiles, membership-paid confirmation + certificate link, **amount selector** for non-membership campaigns (1×/2×/5× presets + custom input), Pay button, and — if manual payments allowed — a card with the member's reference number plus bank account and/or mobile money details.

### `/contributions/callback`
Paystack redirect target: reads `reference`/`trxref`, stores to localStorage, redirects to `/contributions` (which auto-opens the status modal).

### `/events` (list) / `/events/[id]` (detail)
**List**: card grid — banner/placeholder, status badge (Upcoming/Ongoing/Completed/Cancelled), RSVP checkmark badge, date + price/Free overlay, title, venue, attendee count + spots-left, RSVP/Cancel button, paginated.
**Detail**: title/meta, banner, description, media gallery (images + YouTube). Sidebar: ticket price/Free, "you're going" banner, Confirm RSVP (modal-confirmed) / Cancel RSVP, registration-closed message if not open, venue with "open in Maps" link, date, attendance + spots remaining.

### `/jobs` (list) / `/jobs/[id]` (detail)
**List filters**: search text (title/company), location text, job-type pills (All/Full-time/Part-time/Contract/Internship), Clear filters.
**Card**: icon, colored type pill, title, company, location+deadline (red if expired), description preview, View details CTA. Paginated.
**Detail**: banner/placeholder with company name, meta row, full description; sidebar: Apply now (external link with deadline countdown, or "Applications closed"/"No application link"), details list.

### `/directory`
Search (name/company/location) + graduation-year dropdown + Clear. 4-col card grid (avatar, name, job title+company, location, class+department badges, LinkedIn icon). Click toggles a profile drawer (bottom sheet mobile / side panel desktop) with full details. Paginated.

### `/news` (list) / `/news/[id]` (detail)
**List filters**: category pills (All/Announcement/Achievement/News/Event/Opportunity).
**Card**: banner (Pinned badge + category pill + date overlay), title (3-line clamp), category pill + Read link. Paginated.
**Detail**: pinned/category badges, title, date+author, hero image, body rendered as rich HTML, remaining images + YouTube videos in a media gallery.

### `/forum` (list + create) / `/forum/[id]` (thread)
**List**: sort tabs (All/Recent/Popular/Pinned), category pills, search. "New thread" form (Category select, Title, Content textarea) → creates a thread. Thread rows: category pill, Pinned/Closed badges, title, date + reply count. Paginated.
**Thread detail**: header (category/pinned/closed, title, date, reply count), posts list (avatar, author, "OP" badge, date, content), reply box (disabled if closed).

### `/mentorship`
Tabs: Find a mentor / My requests / Incoming (only if user has a mentor profile) / Become a mentor.
- **Find a mentor**: cards (avatar, name, area, Available/Full badge, mentee count, bio); "Request mentorship" reveals inline form (Area of interest required, Message optional).
- **My requests**: list with area, mentor name, date, message, status badge (Accepted/Pending/Rejected/Completed).
- **Incoming** (mentor-only): Accept/Decline buttons (modal-confirmed) on Pending requests.
- **Become a mentor form**: Area of expertise (required), Background/bio (optional), Max mentees (1–10, default 3). Existing non-rejected profile shows a status card instead; Rejected profiles allow resubmission with a warning.

### `/resources` (list) / `/resources/[id]` (detail)
**Filters**: search modal, type select (All/PDF/Video/Link/Document/Image/File), added-after/added-before dates, category pills (Career/Professional/Scholarship/Technical/General/Other), Clear.
**Card**: banner/icon placeholder, category+type pills, title, description, download count + date, View Details + Download/Open icon button (tracks download then opens link).
**Detail**: hero banner/color placeholder, category+type badges, Share (copy link) / Save (localStorage-only toggle), type-aware inline preview (YouTube embed / PDF iframe / image / external-link card), description, stats, primary Download/Open button, related resources (same category).

### `/leaderboard`
Ranked list of year-groups by membership rate/contributions/attendance; top 3 get trophy/medal styling; "Your class" badge on the viewer's own year; columns: Membership %, Total Contributed, Event attendance.

### `/spotlights`
Tabs: Featured stories / My submissions.
- **Featured**: cards — banner (Featured badge on first), avatar/initials, name+year, featured-month badge, title, story (expandable if long), submitted date. Paginated.
- **My submissions**: status-striped cards (Approved/Rejected/Pending) with humanized status label.
- **Submit** (drawer): Title, Story (min 20 chars, live counter), "Submit for review" button.

### `/referrals`
Stats: Total/Registered/Pending referrals. Referral code box with copy button, "Referrer badge earned" pill if applicable. **Send invitation form**: Email field + Send button. History list: name/email, date, status badge (Pending/Registered/Activated).

### `/class-notes`
Facebook-wall-style feed scoped to the member's graduation year.
- **Composer**: textarea (max 1000 chars, live counter) + Post button.
- **Feed**: avatar, author, year-group tag, relative time, content (expandable if long), optional image, Like button (heart, toggles + count), Delete (only own notes, inline confirm). Paginated.

### `/notifications` (full page)
Infinite-scroll list with filter tabs: All / Unread / Jobs / Events / Campaigns / Contributions / Class notes. Row: type icon, unread dot, relative time, title, body, "mark as read" action; rows with an action URL are clickable links. "Mark all read" header action. "Load more" button.

### `/profile`
- **Avatar upload**: camera-icon overlay on avatar → file picker → immediate upload (no crop step).
- **Badges card** (if any earned): 🏅 + humanized badge type.
- **Employment status card**: Employed / Pensioner toggle — switching to Pensioner requires confirmation and is a **one-way, irreversible** action; both options disabled afterward with a persistent notice.
- **Professional info form**: Company, Job title, Location, Phone (2-col grid), LinkedIn URL, Bio (textarea) — all optional.
- **Change password form**: Current password, New password, Confirm new password (mismatch shown inline); submit disabled unless valid.

### `/settings`
- **Account overview card**: read-only summary + "Edit Profile" link.
- **Notifications card**: 6 toggle switches — Membership Reminders, Campaign Alerts, Event Reminders, Job Alerts, Class Notes, Spotlight Updates — each saves immediately on toggle.
- **Security card**: Current/New/Confirm password (shared show/hide toggle), client-side match + min-length validation.
- **About footer**: static branding + "Full Profile" link.

### `/membership-certificate`
- Derives paid membership years by cross-referencing membership campaigns against confirmed contributions.
- Year-selector pills if multiple certificates exist.
- **Certificate design**: A4-landscape styled card — decorative border/corners, logo, "Certificate of Membership" title, member name, member ID/class-year/amount/paid-date row, "Payment Verified & Confirmed" badge, transaction ref footer.
- **Download**: client-side render to canvas → embedded into a landscape A4 PDF via jsPDF, filename `Certificate-{year}-{firstName}-{lastName}.pdf`; falls back to browser print on error.
- Payment details card: campaign/year/amount/method/date/ref + overall membership status.
- Empty state with "Browse campaigns" CTA if no certificates exist.

---

## 1.4 Public / No-Auth Pages

### `/` — Landing page
Marketing site (not gated). Sections: dismissible announcement banner, sticky navbar (in-page anchors, Sign in/Join now), hero (headline, CTAs, trust tiles, animated feature card), leadership spotlight section, animated counter stats, features grid, use-case story cards, "how it works" steps, final CTA banner, footer. Entirely static content, no API calls.

### `/activate-membership/[campaignId]` — Guest membership activation
Lets a newly-registered (pending-approval) user pay membership dues via a direct link without being logged in. Shows campaign hero, "What happens after payment?" checklist, payment form (read-only email from query param, read-only amount, Pay button) → redirects to Paystack.

### `/activate-membership/callback`
Reads the payment reference, polls activation status: loading / success (shows assigned member number + "Sign in" button) / pending (retry) / error (retry + support contact).

### `/payment-campaign/[campaignId]` — Public campaign donation page
Fully unauthenticated, shareable donation page. Campaign hero, optional YouTube embed, progress card (Raised/Target/Backers), contribute card (fixed amount for membership campaigns, free numeric input otherwise, optional email for receipt), share card (copy link + WhatsApp/Twitter/Facebook buttons), "sign in for full credit" nudge.

### System pages
`error.tsx` — generic error card with "Try again". `not-found.tsx` — 404 with "Back to Dashboard".

---

## 1.5 Reusable Component Library (Member App)

Button (variant/size/loading state), Badge (8 variants), Card family, Dialog (Radix) + ConfirmModal wrapper (destructive/default variants), Select/FormSelect, Table family + TableEmpty, Pagination, Skeleton family (text/circular/rectangular/Card/Stat/Table presets), EmptyState, ImageUpload (single, drag-drop, preview), MultiImageUpload (grid, add/remove), MediaGallery (banner + grid + lightbox with keyboard nav), RichTextEditor (TipTap, admin-authored content) + RichTextViewer (renders stored HTML), SearchModal (input that expands into a full dialog with live results), YearGroupPicker (tag-style multi-year input — present but unused on read member pages), YouTubeEmbed/Preview/Grid, Avatar/AvatarFallback (deterministic color-by-name) + UserAvatar (auto image-fallback), DropdownMenu (full Radix set), Input/Textarea (error/success states, auto-resize textarea), Label (required-asterisk), Checkbox, Progress (optional traffic-light coloring), Separator, Tooltip, PageHeader/PageShell (legacy, mostly unused), StatCard (adaptive font size + trend indicator).

---

## 1.6 Data Model (Member-App TypeScript Types)

```
ApiResponse<T> = { message, code, subCode, data: T|null, errors?: ErrorResponse[]|null }
ErrorResponse = { field, errorMessage }
PagedResult<T> = { pageIndex, pageSize, count, totalCount, totalPages, lowerBoundSize, upperBoundSize, results: T[] }
BaseFilter = { page?, pageSize?, sortColumn?, sortDir?, search? }

UserRole = "SuperAdmin" | "Admin" | "Member"
MemberStatus = "Pending" | "Active" | "Suspended" | "Banned" | "Blocked"
AuthData = { id, email, name, role, graduationYear? }
AuthTokens = { accessToken, refreshToken, expiresIn }

Faculty = { id, name, createdAt }
Department = { id, name, facultyId, faculty?, createdAt }

Member = { id, firstName, lastName, email, phone?, graduationYear, departmentId, department?,
  profilePictureUrl?, status, role, createdAt, isMembershipActive?, membershipExpiry?,
  membershipYearsPaid?, lastMembershipPaidAt?, company?, jobTitle?, location?, linkedInUrl?,
  bio?, departmentName?, employmentStatus?, memberNumber?, isEmailVerified?, rejectionCount?, banReason? }

CampaignStatus = "Draft" | "Active" | "Closed" | "Completed" | "Archived"
Campaign = { id, title, description?, targetAmount, amountPerMember, pensionerAmountPerMember?,
  deadline, status, collectedAmount, paidCount, yearGroups?, createdAt, bannerImageUrl?,
  youtubeVideoUrl?, allowOnlinePayments, allowManualPayments,
  bankAccount?: {accountNumber, accountName, bankName, branch},
  mobileMoneyAccount?: {mobileMoneyNumber, name, provider},
  isMembershipCampaign?, membershipYear?, totalEligibleMembers? }

ContributionStatus = "Pending" | "Confirmed" | "Rejected"
ContributionMethod = "Manual" | "Paystack" | "MobileMoney" | "BankTransfer"
Contribution = { id, campaignId, campaignTitle?, memberId, memberName?, memberEmail?,
  memberProfilePictureUrl?, memberNumber?, amount, paymentMethod, status, transactionRef?,
  notes?, confirmedAt?, createdAt }

JobType = "Full-time" | "Part-time" | "Contract" | "Internship"
JobStatus = "Active" | "Closed" | "Draft"
Job = { id, postedBy, title, company, location, type, description?, applyUrl?, deadline?,
  status, yearGroups?, createdAt, bannerImageUrl? }

EventStatus = "Upcoming" | "Ongoing" | "Completed" | "Cancelled"
AlumniEvent = { id, title, description?, startDate, endDate?, venue, capacity?, isTicketed,
  ticketPrice?, status, yearGroups?, rsvpCount, createdAt, bannerImageUrl?, imageUrls?,
  youtubeVideoUrls?, googleLocationUrl? }
EventRegistration = { id, eventId, eventTitle?, memberId, memberName?, memberEmail?,
  memberProfilePictureUrl?, status: "Confirmed"|"Cancelled", createdAt, updatedAt? }

NewsStatus = "Draft" | "Published" | "Archived"
NewsPost = { id, authorId, authorName?, title, content, category, isPinned, status,
  yearGroups?, publishedAt?, createdAt, imageUrls?, youtubeVideoUrls? }

ForumCategory = { id, name, description?, sortOrder?, createdAt }
ForumThread = { id, categoryId, categoryName?, authorId, authorName?, authorProfilePictureUrl?,
  title, isPinned, isClosed, replyCount, createdAt }
ForumPost = { id, threadId, authorId, authorName?, authorProfilePictureUrl?, content, createdAt }

MentorProfileStatus = "Pending" | "Approved" | "Rejected"
MentorProfile = { id, memberId, memberName?, memberProfilePictureUrl?, area, bio?, yearGroups?,
  maxMentees, currentMenteeCount, status, createdAt }
MentorshipStatus = "Pending" | "Accepted" | "Rejected" | "Completed"
MentorshipRequest = { id, mentorProfileId, mentorProfileName?, menteeId, menteeName?,
  menteeProfilePictureUrl?, area, message?, status, createdAt }

Resource = { id, title, description?, category, type: "File"|"Link", fileUrl?, externalUrl?,
  yearGroups?, uploadedBy?, downloadCount?, createdAt, bannerImageUrl? }

MemberDashboardStats = { totalContributions, amountContributed, activeCampaigns, upcomingEvents }

YearGroupLeaderboardEntry = { yearGroup, totalMembers, membershipPaidCount, membershipRate,
  totalContributed, eventAttendanceCount }
MemberBadge = { id, memberId, memberName?, badgeType, description?, earnedAt, createdAt }
Spotlight = { id, memberId, memberName?, memberProfilePictureUrl?, memberGraduationYear?,
  title, story, imageUrl?, status: "Pending"|"Approved"|"Rejected", featuredMonth?, createdAt }
Referral = { id, referrerId, referrerName?, referredEmail, referredMemberId?,
  referredMemberName?, status: "Pending"|"Registered"|"MembershipPaid", createdAt }
ReferralInfo = { referralCode, totalReferrals, registeredReferrals, pendingReferrals, hasReferrerBadge }
ClassNote = { id, authorId, authorName?, authorProfilePictureUrl?, yearGroup, content,
  imageUrl?, likeCount, isLikedByMe, createdAt }
NotificationPreference = { id, membershipReminders, campaignAlerts, eventReminders,
  jobAlerts, classNoteAlerts, spotlightAlerts }
NotificationItem = { id, recipientId, recipientType, title, body, type, isRead, readAt?,
  relatedEntityId?, relatedEntityType?, actionUrl?, createdAt }
```

---

## 1.7 API Surface (Member API, as consumed by the member frontend)

All requests go through the authenticated client (bearer token, auto-refresh on 401) unless marked **[public]**.

**Departments**: `GET /departments`

**Auth / Profile**: `GET /auth/me`, `PUT /auth/me` (multipart — company/jobTitle/location/linkedInUrl/bio/phone/employmentStatus/profilePicture), `PUT /auth/changepassword`, plus (not exported as helpers but used directly) `POST /auth/login`, `POST /auth/register`, `POST /auth/verify-otp`, `POST /auth/resend-otp`, `POST /auth/forgot-password`, `POST /auth/refresh`

**Campaigns**: `GET /campaigns`, `GET /campaigns/{id}`, `GET /campaigns/membership/current` **[public]**, `GET /contributions/membership/status`, `GET /contributions/membership/current-unpaid`, `POST /contributions/membership/renew`

**Contributions**: `GET /contributions`, `POST /contributions/paystack/initiate`, `POST /contributions/paystack/initiate/guest` **[public]**, `GET /contributions/paystack/verify/{reference}`, `GET /contributions/paystack/status/{reference}`, `GET /contributions/paystack/activation-status/{reference}` **[public]**, `POST /contributions/proof`

**Events**: `GET /events`, `GET /events/{id}`, `POST /events/rsvp`, `DELETE /events/{id}/rsvp`, `GET /events/my-rsvps`

**Jobs**: `GET /jobs`, `GET /jobs/{id}`

**News**: `GET /news`, `GET /news/{id}`

**Forum**: `GET /forum/categories`, `GET /forum/threads`, `GET /forum/threads/{id}/posts`, `POST /forum/threads`, `POST /forum/threads/{id}/reply`

**Mentorship**: `GET /mentorship/mentors`, `POST /mentorship/mentor-profile`, `POST /mentorship/requests`, `GET /mentorship/requests/mine`, `GET /mentorship/mentor-profile/mine`, `GET /mentorship/requests/incoming`, `PUT /mentorship/requests/{id}/accept`, `PUT /mentorship/requests/{id}/reject`

**Uploads**: `POST /uploads/image`

**Directory**: `GET /directory`

**Resources**: `GET /resources`, `GET /resources/{id}`, `POST /resources/{id}/download`

**Leaderboard**: `GET /leaderboard`

**Badges**: `GET /badges`, `POST /badges/evaluate`

**Spotlights**: `GET /spotlights`, `GET /spotlights/{id}`, `POST /spotlights`, `GET /spotlights/mine`

**Referrals**: `GET /referrals`, `POST /referrals/invite`, `GET /referrals/list`

**Class Notes**: `GET /classnotes`, `POST /classnotes`, `POST /classnotes/{id}/like`, `DELETE /classnotes/{id}`

**Notification Preferences**: `GET /notificationpreferences`, `PUT /notificationpreferences`

**Notifications**: `GET /notifications`, `GET /notifications/unread-count`, `PUT /notifications/{id}/read`, `PUT /notifications/read-all`

---

## 1.8 Member-App Cross-Cutting Notes

1. **Paystack** is used in 4 flows: authenticated campaign payment, authenticated membership renewal, guest membership activation (pre-approval), and fully public campaign donation. All rely on **client polling** (10s interval) plus localStorage persistence of the pending reference, not push notifications.
2. **Pensioner-rate logic** is duplicated in three places (dashboard, contributions list, campaign detail) rather than centralized.
3. **Certificate generation is entirely client-side** — no backend certificate endpoint; eligibility is derived by cross-referencing contributions against membership campaigns in the browser.
4. **Referral flow** is end-to-end: server-generated code → displayed/copied on Referrals page → consumed via `?ref=` on Register → funnel status tracked (Pending/Registered/MembershipPaid).
5. **"Saved resources"** is purely client-local (localStorage), not synced to any backend.
6. **Notification bell polls every 30s** — no websocket/push; a rebuild could upgrade to real-time.
7. **Auth tokens are stored in plain localStorage** (not httpOnly cookies) — a security consideration for the rebuild.
8. **Guest vs. member payment duality**: three separate payment entry points (member, guest-activation, public-campaign) have overlapping but not identical UX — worth unifying.

---

# PART 2 — ADMIN PORTAL (Back-Office Web App)

Source: Next.js App Router, TypeScript, TanStack Query, react-hook-form + zod, axios, Tailwind, Radix UI, TipTap rich text editor, Recharts, sonner toasts.

## 2.0 Global Structure

Same provider stack pattern as the member app (`QueryClientProvider` → `AuthProvider` → `TooltipProvider` → `Toaster`). Root `/` redirects to `/login`. Route groups: `(auth)` (Login, Forgot Password) and `(dashboard)` (all authenticated admin pages, wrapped in `AdminLayout`).

## 2.1 Authentication & Authorization

Same architecture as the member app but against a separate `adminClient` (base URL `NEXT_PUBLIC_ADMIN_API_URL`, default `http://localhost:5100/api/v1`), separate localStorage keys, silent 401 refresh with request queuing.

**Roles**: `SuperAdmin`, `Admin`, `Member` (shared enum; `isAdmin = role is Admin or SuperAdmin` gates the whole dashboard). Fine-grained checks are done ad hoc per page:
- **SuperAdmin-only**: forum moderation, admin user management, year-group targeting on content, membership campaign creation.
- **Admin**: implicitly year-group-scoped (enforced server-side) for most content areas.
- **Admin + SuperAdmin**: mentorship management.

### Login page
Email + Password (min 8, show/hide toggle) → `POST /auth/login` → success redirects to `/dashboard`.

### Forgot Password page
Email field → `POST /auth/forgot-password` → confirmation card.

### AdminLayout guard
Renders nothing while loading or unauthenticated; client-redirects to `/login` once hydration completes if not an admin. **No server-side/middleware route protection exists.**

## 2.2 AdminLayout & Navigation

**Sidebar** (240px, gradient background): Dashboard, Members, [Admins — SuperAdmin only], Campaigns, Membership Renewal, Contributions, — Community — Job Board, Events, News, Forum (SuperAdmin only), Mentorship (SuperAdmin only in nav, but page itself allows Admin too), Resources, Spotlights, — Reports — Reports & Exports, Notifications. Footer: avatar/name/role, Settings, Log out.

**Header**: desktop shows notification bell only; mobile adds hamburger + avatar circle.

**Notification panel**: same pattern as member app (30s poll, dropdown, mark read/mark all read).

## 2.3 Page-by-Page Specification

### `/dashboard`
Read-only operational overview, no forms/CRUD.
- 4 stat cards: Total Members (+ pending count), Active Campaigns, Total Collected, Upcoming Events (+ open jobs count).
- **Contribution trend chart** (bar chart, last 7 months, bucketed client-side from confirmed contributions).
- **Pending Approvals card**: first 3 pending members + "View all" link.
- **Active Campaigns card**: per-campaign progress bars (membership: paid/eligible; else collected/target).
- **Recent Contributions table**: last 5 (Member, Campaign, Amount, Method, Date).

### `/members`
**Purpose**: approve/reject registrations, ban/unban, bulk import, export.
- Filter bar: search modal + status select (All/Active/Pending/Suspended/Banned/Blocked).
- **Table**: Member (avatar+name+email+rejection count), Member No., Year, Status badge, Verified icon, Joined, Actions.
- Row actions (status-dependent): Approve/Reject (Pending), Ban (Active/Suspended), Unban (Banned), View details (always).
- **Confirm modals**: Approve ("grants active access, assigns member number"); Reject (destructive, optional reason, "3 rejections → permanently blocked" warning); Ban (destructive, optional reason); Unban.
- **Import Members dialog**: large textarea for pasting a raw JSON array (schema: firstName, lastName, email, graduationYear required; phone, studentId, departmentId, paidMembershipYears[] optional) → shows "N imported, M skipped" + first 3 errors if any.
- **Export CSV**: fetches up to 5000 rows client-side, generates CSV (id, firstName, lastName, email, phone, graduationYear, department, status, memberNumber).
- Row-density toggle (Compact/Comfortable). Pagination.

### `/members/[id]`
Full profile + moderation + membership activation.
- Header: avatar, name, status badge, email-verified indicator, member number; contextual action buttons (same rules as list).
- Personal Info card (grad year, joined, phone), Professional Info card (company/title/location/LinkedIn), Bio card (if present).
- **Membership card** (if Active status + membership campaigns exist): membership badge, years-paid count, "Activate Membership" button → checklist modal of available membership years → activates selected years directly (admin backfill, bypasses payment).
- Admin Notes card (rejection count, ban reason) if applicable.
- "Share profile" (copy link) button.

### `/admins` (SuperAdmin only)
Create/manage other admin accounts.
- **Create Admin form**: First name, Last name, Email, Password, Role (SuperAdmin/Admin), Year group (single-year picker, optional).
- **Edit Admin form**: First name, Last name, Role, Year group, Disabled (checkbox).
- Table: Name, Email, Role, Year group, Status (Active/Disabled badge), Created, Actions (Edit inline, Enable/Disable toggle).

### `/campaigns`
CRUD for non-membership fundraising campaigns; lifecycle Draft→Active→Closed→Completed→Archived.
- **Campaign form** (create/edit): Title, Description, Target amount, Minimum contribution ("amount per member"), Deadline, Target year groups (SuperAdmin only — "All years" checkbox + year picker), Banner image, YouTube URL + preview, Allow Online Payments checkbox, Allow Manual Payments checkbox (reveals Bank Account: account number/name/bank name/branch, and Mobile Money: number/name/provider).
- Card grid: banner + status badge + % complete overlay, title, year-group badge, description, progress bar, Members Paid, Deadline; actions: View Details, Edit (if Active), Close (if Active, confirm), Archive (if not Active/Archived), Re-open (if Completed), Restore (if Archived, confirm).
- Status filter pills. Pagination.

### `/campaigns/[id]`
Single-campaign management: Paystack disbursement tracking (Total Paid/Disbursed/Outstanding/Confirmed count/Disbursed count; "Mark as disbursed" for SuperAdmins on closed campaigns with outstanding funds), inline edit form (superset of create form — adds pensioner amount, membership-campaign toggle + year for SuperAdmins), campaign media (banner + YouTube, with lightbox), contributions table for this campaign, paginated.

### `/contributions`
Global contribution ledger.
- **Record Manual Payment form**: Campaign (select, Active only), Member Number (required), Member Name, Member Email, Amount (required), Payment Method (Manual/BankTransfer/MobileMoney), Transaction Ref, Notes, "Mark as confirmed" checkbox (default true, reveals Paid-at datetime when checked).
- Filter: search modal + status select (All/Pending/Confirmed/Rejected).
- Table: Member (avatar+name+email/number), Campaign, Amount, Ref, Method badge, Status badge, Date.
- Export CSV (up to 5000 rows: id, memberName, memberNumber, memberEmail, campaignName, amount, status, paymentMethod, transactionRef, paidAt, createdAt). Row-density toggle. Pagination.

### `/membership` (Membership Renewal)
Membership campaigns grouped by Current Year / Future Years / Past Years.
- **Create form** (SuperAdmin only): Campaign title (auto-fills "Membership Renewal {year}"), Membership Year (drives title + contextual helper text), Description, Amount for employed members, Amount for pensioners (optional, defaults to same), Deadline, Banner image, Allow Online Payments (default true), Allow Manual Payments (default false, reveals bank/mobile-money sub-fields). Target amount computed client-side as `amount × eligible member count`.
- 4 summary stats: Eligible Members, Paid (year), Unpaid (year), Total Campaigns.
- Current/Future sections: full campaign cards with 3-stat row (Paid/Unpaid/Total), progress bar, contextual info box. Past section: compact cards.

### `/jobs` (list) / `/jobs/[id]` (detail)
**Job form**: Title, Company, Location, Job Type (select), Target year groups (SuperAdmin only), Deadline, Apply URL, Banner image, Status (edit only: Active/Closed/Draft), Description.
**List**: filters (search modal, location, posted-after/before dates, status pills, type pills). Card grid with Edit/Preview(external)/Delete actions. Pagination.
**Detail**: edit-mode toggle (full form) / view-mode (banner, meta, description); sidebar: status card, "View Application" external link, Edit/Close/Delete actions, meta blocks.

### `/events` (list) / `/events/[id]/rsvps` (RSVP management)
**Event form**: Title, Description, Start Date, End Date, Venue, Google Maps URL, Capacity, Target year groups (SuperAdmin only), Status (edit only), Banner image, Event Photos (multi-upload), YouTube URLs (comma-separated + preview). **Note: ticketing (isTicketed/ticketPrice) exists in the data model but is not exposed in this form — free events only, currently.**
**List**: card grid — banner, status badge, year-group badge, title, description, meta (start date, venue linked to Maps, RSVP count/capacity); actions: View RSVPs, Edit (if not Cancelled/Completed), Cancel (confirm, destructive), Delete (if Cancelled/Completed, confirm destructive).
**RSVPs page**: status filter (Confirmed/Cancelled/All), table (Member, Email, Status badge, Changed date). Currently **read-only** in the UI (a reopen-RSVP API exists but has no button wired to it).

### `/news` (list) / `/news/[id]` (detail)
**Post form**: Title, Category (select), Status (Draft/Published/Archived), Pin post (Yes/No), Target year groups (SuperAdmin only), Content (rich text editor), Post Images (multi-upload), YouTube URLs.
**List**: search + status pills (All/Draft/Published/Archived); card grid with cover image, pin/category/status badges, stripped-HTML preview, published date, image count; actions: View, Edit (inline), Publish (confirm, if Draft), Archive (confirm).
**Detail**: full rendered HTML content, image gallery with lightbox, YouTube videos grid, Publish/Archive actions. (Note: an "Edit" link points to a `/news/[id]/edit` route that does not exist — editing only works from the list page's inline form.)

### `/forum` (SuperAdmin only)
Toggle "Threads"/"Categories" views.
- **Categories**: Add Category form (Name, Description); list with sort-order badge.
- **Threads**: search + category filter + sort pills (All/Recent/Popular/Pinned); cards with Pin/Unpin (instant), Close/Reopen (confirm), Delete (confirm) actions.

### `/mentorship` (Admin + SuperAdmin)
Toggle "Mentors"/"Requests" views.
- **Mentors**: search + status pills (All/Pending/Approved/Rejected); cards (avatar, name, area, status badge, bio, mentee capacity bar); Approve/Decline (confirm) if Pending.
- **Requests**: read-only list (mentee, mentor profile, area, date, message, status badge) — no accept/reject actions currently exposed to admins.

### `/resources` (list) / `/resources/[id]` (detail)
**Resource form**: Title, Category (select), Description, Type toggle (External Link / File-URL), Target year groups (SuperAdmin only), External URL (conditional) or Upload File (conditional, any file type), Banner image.
**List**: filters (search, category select, type select, added-after/before dates); card grid; View/Edit/Delete actions. Pagination.
**Detail**: content-aware preview (YouTube embed / PDF iframe / image / external-link card / unavailable), stats, Download/Open CTA, related resources (same category).

### `/spotlights`
- Status filter pills (All/Pending/Approved/Rejected).
- Spotlight cards: gradient header (status-colored), avatar, status badge, title, member+year+date, featured-month badge, story preview, optional image, Approve/Reject (confirm) if Pending.
- **Feature-a-Member dialog**: step 1 — member search (min 2 chars) → select; step 2 — Title (required), Story (required), Image URL (optional) → directly creates and publishes a spotlight for the chosen member (admin-curated, bypasses member self-submission review).

### `/reports`
- 5 stat cards: Total Members, Total Contributions, Total Collected, Campaigns, Events.
- Campaign Performance card: per-campaign progress bars + "Export Campaigns CSV".
- Campaign Status Breakdown card: Active/Closed/Total counts, overall progress.
- **Data Exports card**: 5 independent-loading-state buttons — Campaigns CSV, Members CSV (2000 rows), Contributions CSV (2000 rows), Events CSV (2000 rows), Jobs CSV (2000 rows) — all built client-side from paginated fetches (no dedicated server export used by the UI, though a generic `/reports/export/{entity}` streaming CSV endpoint exists server-side).

### `/notifications` (full page)
Infinite-scroll inbox; filter tabs: All / Unread / Payments (Payment Received, Contribution Confirmed, Contribution Rejected); "Mark all read"; "Load more".

### `/settings`
- Account card (avatar, name, email, role badge — read-only).
- **Notifications card**: 4 toggles (New Registrations, Pending Approvals, New Contributions, System Alerts) — **persisted to localStorage only**, not synced to backend.
- **Change Password form**: Current/New/Confirm (shared show/hide toggle), client validation (match + min 8 chars) before submit.
- About card (static branding).

## 2.4 Reusable Component Library (Admin App)

Same core set as the member app (Avatar/UserAvatar, Badge, Button, Card family, Checkbox, ConfirmModal, Dialog family, DropdownMenu, EmptyState, ImageUpload, MultiImageUpload, Input, Label, MediaGallery [built but unused], PageHeader/PageShell, Pagination, Progress, RichTextEditor/Viewer, Select/FormSelect, Separator, Skeleton family, SearchModal, Table family, Textarea, Tooltip, YouTubeEmbed/Preview/Grid, StatCard), plus:
- **YearGroupPicker** — tag-style multi-year selector (type/comma/Enter to add, removable chips, min/max range validation, datalist autocomplete) — actively used across Campaign/Job/Event/News/Resource forms for SuperAdmin year-group targeting.

## 2.5 Data Model (Admin-App TypeScript Types)

Shares the same core types as the member app (`ApiResponse`, `PagedResult`, `Member`, `Campaign`, `Contribution`, `Job`, `AlumniEvent`, `NewsPost`, `ForumCategory/Thread/Post`, `MentorProfile/MentorshipRequest`, `Resource`, `Spotlight`, `NotificationItem`), plus admin-specific additions:

```
AdminUser = { id, firstName, lastName, email, role: UserRole, graduationYear?, isDisabled?, createdAt }
CreateAdminRequest = { firstName, lastName, email, password, role, graduationYear? }
UpdateAdminRequest = { firstName, lastName, role, graduationYear?, isDisabled? }

Campaign additionally includes: isPaystackDisbursed, paystackDisbursedAt?, paystackDisbursedBy?
PaystackDisbursementSummary = { totalPaidToPaystack, totalDisbursed, totalOutstanding, confirmedCount, disbursedCount }
ReportSummary = { totalMembers, totalContributions, totalCollected, totalCampaigns, activeCampaigns, closedCampaigns, totalEvents, totalJobs }
Member additionally includes: studentId?
```

## 2.6 API Surface (Admin API, as consumed by the admin frontend)

**Auth/Profile**: `GET /auth/me`, `PUT /auth/changepassword`, `POST /auth/login` (via auth hook), `POST /auth/refresh`, `POST /auth/forgot-password`

**Members**: `GET /members`, `GET /members/{id}`, `PUT /members/{id}/approve`, `PUT /members/{id}/reject`, `PUT /members/{id}/ban`, `PUT /members/{id}/unban`, `POST /members/import`, `PUT /members/{id}/activate-membership`

**Admins**: `GET /admins`, `POST /admins`, `PUT /admins/{id}`

**Campaigns**: `GET /campaigns`, `GET /campaigns/{id}`, `POST /campaigns` (multipart), `PUT /campaigns/{id}` (multipart), `DELETE /campaigns/{id}` (unused in UI), `PUT /campaigns/{id}/archive`, `PUT /campaigns/{id}/unarchive`, `PUT /campaigns/{id}/activate`

**Contributions/Reports**: `GET /contributions`, `POST /contributions/manual`, `PUT /contributions/{id}/confirm`, `PUT /contributions/{id}/reject`, `GET /campaigns/{id}/paystack-summary`, `PUT /campaigns/{id}/paystack-disburse`, `GET /reports/summary`

**Jobs**: `GET /jobs`, `GET /jobs/{id}`, `POST /jobs` (multipart), `PUT /jobs/{id}` (multipart), `DELETE /jobs/{id}`, `PUT /jobs/{id}/close`

**Events**: `GET /events`, `POST /events` (multipart), `PUT /events/{id}` (multipart), `DELETE /events/{id}`, `PUT /events/{id}/cancel`, `GET /events/{id}/rsvps`, `PUT /events/{eventId}/rsvps/{rsvpId}/reopen` (unused in UI)

**News**: `GET /news`, `GET /news/{id}`, `POST /news` (multipart), `PUT /news/{id}` (multipart), `PUT /news/{id}/publish`, `DELETE /news/{id}` (unused in UI)

**Forum**: `GET /forum/categories`, `POST /forum/categories`, `GET /forum/threads`, `PUT /forum/threads/{id}/pin`, `PUT /forum/threads/{id}/close`, `DELETE /forum/threads/{id}`

**Mentorship**: `GET /mentorship/profiles`, `PUT /mentorship/profiles/{id}/approve`, `PUT /mentorship/profiles/{id}/reject`, `GET /mentorship/requests`

**Resources**: `GET /resources`, `GET /resources/{id}`, `POST /resources` (multipart), `DELETE /resources/{id}`, `PUT /resources/{id}` (multipart)

**Uploads**: `POST /uploads/image`, `POST /uploads/file` (both unused directly — uploads are bundled into entity multipart requests instead)

**Spotlights**: `GET /spotlights`, `POST /spotlights`, `POST /spotlights/{id}/approve`, `POST /spotlights/{id}/reject`

**Notifications**: `GET /notifications`, `GET /notifications/unread-count`, `PUT /notifications/{id}/read`, `PUT /notifications/read-all`

## 2.7 Admin-App Cross-Cutting Notes

1. **No server-side auth enforcement** — the dashboard route group is only protected client-side.
2. **Role scoping is inconsistent**: nav visibility, page-level "Access denied" screens, and field-level SuperAdmin conditionals are three separate, ad hoc mechanisms — a rebuild should centralize a permission matrix (entity × action × role).
3. **Dead/incomplete features observed** (decide whether to fix or drop in the rebuild): `/news/[id]/edit` and `/resources/[id]/edit` routes are linked but don't exist; RSVP "reopen" has no UI; contribution confirm/reject mutations on Campaign Detail have no rendered buttons; ticketed events are modeled but disabled in the form; the built `MediaGallery` component isn't used anywhere; the Mentorship "Requests" tab is view-only despite the status model supporting accept/reject.
4. **CSV export pattern**: no dedicated server export is used by most UI buttons — pages re-fetch up to 2000–5000 rows and build CSV client-side via Blob download (even though the backend does expose a generic streaming `/reports/export/{entity}` endpoint that the UI doesn't call).
5. **Local-only settings**: admin notification preferences persist to localStorage only, not the backend.

---

# PART 3 — BACKEND (APIs, Database, Integrations)

Source: `.NET 8`, ASP.NET Core Web API (MVC controllers, not Minimal APIs), Entity Framework Core / PostgreSQL, Akka.NET actors for async dispatch, JWT bearer auth, Redis, S3-compatible object storage, Paystack, SMTP/Mailtrap.

Two REST APIs share one PostgreSQL database (schema `alumni`) and a set of shared SDK libraries:
- **Member.Api** — public-facing alumni portal API.
- **Admin.Api** — back-office API for staff.

Both are versioned (`api/v{version}/[controller]`, lowercase), return a uniform envelope `ApiResponse<T> { Message, Code, SubCode, Data, Errors }` mapped to real HTTP status codes, use Serilog logging, and auto-apply EF Core migrations on startup. There are **no cron/scheduled jobs** anywhere in the system — all "background" behavior is one-shot async dispatch (Akka.NET actors) triggered by a specific user action (e.g., a payment webhook, a new class note).

## 3.0 Cross-Cutting Backend Concerns

### 3.0.1 Authentication & Authorization
- **JWT Bearer only** — no cookies/sessions.
- **Two independent signing keys** (`AdminSigningKey`, `MemberSigningKey`) sharing one Issuer/Audience — a member token cannot authenticate the admin API and vice versa.
- **Claims**: sub (user id), Email, GivenName, Surname, Role, Jti, optional picture claim; admin tokens additionally carry a `year_group` claim.
- **Roles**: `Member` (flat, single role); `Admin`/`SuperAdmin` (two-tier, year-group-scoped for regular Admins).
- **Refresh tokens**: opaque random 64-byte base64 string, stored in **Redis** (`member:refresh:{id}` / `admin:refresh:{id}`), TTL = configurable days (default 30). Refresh endpoint validates the (possibly expired) access token ignoring lifetime, extracts the user id, compares the supplied refresh token against Redis.
- **Password hashing**: BCrypt.
- **Member registration is OTP-gated**: register → 6-digit OTP emailed → payload cached in Redis 15 min (password already bcrypt-hashed) → verify-otp persists the Member row as `Pending` → requires admin approval (or auto-approves on successful membership payment — see §3.0.4).
- **Account status machine**: `Pending → Active` (approved) or `Suspended` (rejected, can re-register) or `Active → Banned`/`Blocked` (blocked automatically after 3 cumulative rejections).
- **Admin content scoping**: non-SuperAdmin Admins can only view/modify content whose `YearGroups` includes their own graduation year, or content they personally created; SuperAdmin bypasses this.

### 3.0.2 API Conventions
- Pagination: `BaseFilter { Page=1, PageSize=10, SortColumn?, SortDir?, Search? }` → `PgPagedResult<T> { PageIndex, PageSize, Count, TotalCount, TotalPages, LowerBoundSize, UpperBoundSize, Results }`.
- JSON: Newtonsoft.Json, camelCase, nulls omitted, UTC datetimes.
- File uploads: multipart form binding, mixed with regular fields on the same request DTO.
- Request body limit: 10 MB.
- CORS: wide open (any origin/header/method) in both APIs — **must be locked down for a SaaS rebuild**.

### 3.0.3 Redis Usage
Two isolated logical DBs on one Redis server (DB1=Member, DB0=Admin), JSON-serialized values, simple GET/SET/DEL only (no pub/sub, no lists/hashes):

| Key pattern | TTL | Purpose |
|---|---|---|
| `reg:otp:{email}` | 15 min | Cached pending registration payload awaiting OTP |
| `member:refresh:{memberId}` | 30 days | Opaque refresh token |
| `admin:refresh:{adminId}` | 30 days | Opaque refresh token |
| `paystack:ref:{reference}` | 24 h | Maps a Paystack reference → `{MemberId, CampaignId}` for reconciling webhook/verify calls that arrive before the DB record fully exists |

### 3.0.4 Async/Background Processing (Akka.NET actors — not schedulers)
- **PaystackCallbackActor** (Member.Api) — receives payment-webhook commands, processes them asynchronously in a fresh DI scope so Paystack gets an immediate 200 response.
- **NotificationDispatcherActor** (both APIs) — fans out in-app `Notification` rows for class-note alerts, payment-received events, job/campaign/event/spotlight alerts, etc.
- **No recurring jobs exist** (no membership-expiry sweep, no scheduled digests) — a SaaS rebuild will likely need to add real scheduled jobs for these.

### 3.0.5 Notification Types (in-app)
`JobAlert | CampaignAlert | EventReminder | SpotlightUpdate | ClassNoteAlert | MembershipReminder | PaymentReceived | ContributionConfirmed | ContributionRejected`. Each carries `RelatedEntityId`/`RelatedEntityType` and an `ActionUrl` (currently hardcoded to `localhost:3000`/`localhost:3001` — must be externalized). Members can opt in/out per category via `NotificationPreference` (defaults all true).

---

## 3.1 Member API — Full Endpoint Inventory

### Auth / Members (`api/v1/auth`)
| Method & Route | Auth | Purpose |
|---|---|---|
| POST `/register` | Anonymous | Register; sends OTP; caches payload in Redis 15 min |
| POST `/verify-otp` | Anonymous | Verify OTP, create Member row (Pending), link referral |
| POST `/resend-otp` | Anonymous | Resend, max 3, else must re-register |
| POST `/send-email-verification` | Anonymous | Send 24h verification link |
| POST `/forgot-password` | Anonymous | Send 24h reset link |
| POST `/reset-password` | Anonymous | Complete reset via token |
| POST `/login` | Anonymous | Blocked for non-Active statuses |
| POST `/refreshtoken` | Anonymous | Rotate tokens |
| GET `/me` | Authenticated | Own profile |
| PUT `/me` | Authenticated | Update profile + optional picture (multipart, jpg/png/webp/gif ≤5MB) |
| PUT `/changepassword` | Authenticated | Invalidates refresh token |
| GET `/verify-email` | Anonymous | Verify via link token (query: token, email) |

### Badges (`api/v1/badges`) — Authenticated
`GET /` (my badges), `POST /evaluate` (run rules engine, idempotent). Rules: FirstContribution (≥1 confirmed contribution), EventAttendee3 (≥3 confirmed RSVPs), Referrer (≥1 registered referral), SuperReferrer (≥5), MembershipStreak2/3/5 (consecutive paid years).

### Campaigns (`api/v1/campaigns`)
`GET /` (Authenticated, active campaigns visible to member), `GET /{id}` (Anonymous), `GET /membership/current` (Anonymous).

### Class Notes (`api/v1/classnotes`) — Authenticated
`GET /` (own year-group wall), `POST /` (triggers ClassNoteAlert to year-group peers), `POST /{id}/like` (toggle, unique per member+note), `DELETE /{id}` (soft-delete, own only).

### Contributions / Payments (`api/v1/contributions`)
| Route | Auth | Purpose |
|---|---|---|
| GET `/` | Authenticated | My contributions, paginated, filterable by campaign |
| GET `/membership/status` | Authenticated | Full membership status incl. arrears |
| GET `/membership/current-unpaid` | Authenticated | Current-year unpaid membership campaigns |
| POST `/membership/renew` | Authenticated | Renew (online or manual); Years must == 1 |
| POST `/paystack/initiate` | Authenticated | Initiate campaign payment |
| POST `/paystack/initiate/guest` | Anonymous | Guest checkout |
| GET `/paystack/verify/{reference}` | Anonymous | Verify a transaction |
| GET `/paystack/status/{reference}` | Authenticated | Poll status |
| GET `/paystack/activation-status/{reference}` | Anonymous | Read-only, for post-payment landing page |
| POST `/proof` | Authenticated | Upload manual-payment proof; notifies admins |

### Departments (`api/v1/departments`) — public
`GET /` — list all.

### Directory (`api/v1/directory`) — Authenticated
`GET /` — search by department/graduation year/text.

### Events (`api/v1/events`) — Authenticated
`GET /`, `GET /{id}`, `POST /rsvp`, `DELETE /{id}/rsvp`, `GET /my-rsvps`.

### Forum (`api/v1/forum`) — Authenticated
`GET /categories`, `GET /threads`, `POST /threads`, `GET /threads/{id}/posts`, `POST /threads/{id}/reply`.

### Jobs (`api/v1/jobs`) — Authenticated
`GET /`, `GET /{id}`.

### Leaderboard (`api/v1/leaderboard`) — Authenticated
`GET /` — year-group ranking by membership rate & contributions.

### Mentorship (`api/v1/mentorship`) — Authenticated
`GET /mentors`, `POST /mentor-profile` (creates Pending profile), `POST /requests`, `GET /requests/mine`, `GET /mentor-profile/mine`, `GET /requests/incoming`, `PUT /requests/{id}/accept`, `PUT /requests/{id}/reject`.

### News (`api/v1/news`) — Authenticated
`GET /`, `GET /{id}`.

### Notification Preferences (`api/v1/notificationpreferences`) — Authenticated
`GET /`, `PUT /`.

### Notifications (`api/v1/notifications`) — Authenticated
`GET /`, `GET /unread-count`, `PUT /{id}/read`, `PUT /read-all`.

### Paystack Webhook (`api/v1/callbacks/paystack`) — public
`POST /` — validates HMAC-SHA512 signature (logged but **not currently enforced** — a rebuild must decide whether to reject invalid signatures), hands off to actor, always returns 200 to stop Paystack retries.

### Referrals (`api/v1/referrals`) — Authenticated
`GET /`, `POST /invite`, `GET /list`.

### Resources (`api/v1/resources`) — Authenticated
`GET /`, `GET /{id}`, `POST /{id}/download`.

### Spotlights (`api/v1/spotlights`) — Authenticated
`GET /`, `GET /{id}`, `POST /` (Pending, awaits review), `GET /mine`.

### Uploads (`api/v1/uploads`) — Authenticated
`POST /image` — jpeg/png/gif/webp/svg only, ≤5MB.

---

## 3.2 Admin API — Full Endpoint Inventory

### Admin Auth (`api/v1/auth`)
`POST /login` (blocked if disabled), `POST /refreshtoken`, `GET /me` (Authenticated), `PUT /changepassword` (Authenticated, re-issues tokens).

### Admin User Management (`api/v1/admins`) — SuperAdmin only
`GET /`, `POST /` (409 on duplicate email), `PUT /{id}`.

### Campaigns (`api/v1/campaigns`) — Admin/SuperAdmin
`GET /`, `GET /{id}`, `POST /` (multipart), `PUT /{id}` (multipart), `DELETE /{id}`, `PUT /{id}/archive`, `PUT /{id}/unarchive`, `PUT /{id}/activate`, `GET /{id}/paystack-summary`, `PUT /{id}/paystack-disburse`.

### Contributions (`api/v1/contributions`) — Authenticated
`GET /`, `POST /manual` (records offline contribution, optionally pre-confirmed), `PUT /{id}/confirm` (notifies member), `PUT /{id}/reject` (notifies member).

### Events (`api/v1/events`) — Admin/SuperAdmin
`GET /` (year-group scoped for non-SuperAdmin), `POST /` (multipart, triggers EventReminder alert), `PUT /{id}` (multipart), `PUT /{id}/cancel`, `DELETE /{id}`, `GET /{id}/rsvps`, `PUT /{id}/rsvps/{rsvpId}/reopen`.

### Forum (`api/v1/forum`) — SuperAdmin only
`GET /categories`, `POST /categories`, `GET /threads`, `PUT /threads/{id}/pin`, `PUT /threads/{id}/close`, `DELETE /threads/{id}`.

### Jobs (`api/v1/jobs`) — Admin/SuperAdmin
`GET /`, `GET /{id}`, `PUT /{id}/close`, `POST /` (multipart, triggers JobAlert), `DELETE /{id}`, `PUT /{id}` (multipart).

### Members (`api/v1/members`) — Authenticated
`GET /`, `GET /{id}`, `PUT /{id}/approve` (assigns member number), `PUT /{id}/reject` (increments rejection count, blocks after 3), `PUT /{id}/ban`, `PUT /{id}/unban`, `POST /import` (bulk, with optional prepaid membership years for migration), `PUT /{id}/activate-membership` (manual backfill).

### Mentorship (`api/v1/mentorship`) — SuperAdmin only
`GET /profiles`, `PUT /profiles/{id}/approve`, `PUT /profiles/{id}/reject`, `GET /requests`.

### News (`api/v1/news`) — Admin/SuperAdmin
`GET /`, `GET /{id}`, `POST /` (multipart), `PUT /{id}` (multipart), `PUT /{id}/publish`, `DELETE /{id}`.

### Notifications (`api/v1/notifications`) — Authenticated
Same shape as Member.Api, scoped to `RecipientType=="Admin"`.

### Reports (`api/v1/reports`) — Authenticated
`GET /summary`, `GET /export/{entity}` — streams CSV for the named entity.

### Resources (`api/v1/resources`) — Authenticated
`GET /`, `GET /{id}`, `POST /` (multipart), `DELETE /{id}`, `PUT /{id}` (multipart).

### Spotlights (`api/v1/spotlights`) — Authenticated
`GET /`, `POST /` (admin directly creates & features), `POST /{id}/approve`, `POST /{id}/reject`.

### Uploads (`api/v1/uploads`) — Authenticated
`POST /image` (≤5MB), `POST /file` (≤20MB).

---

## 3.3 Database Schema (PostgreSQL, schema `alumni`)

**Universal notes**: every entity inherits `BaseEntity { Id: string (Guid "N"), CreatedAt, UpdatedAt?, CreatedBy (default "system"), UpdatedBy? }`. **There are no real foreign-key constraints anywhere** — relationships are plain indexed string Id columns, and historical/display data is preserved via **JSONB "snapshot" columns** (e.g. `MemberSnapshot`, `CampaignSnapshot`) embedded directly on the referencing row, so a payment's displayed member name survives even if the source member row changes. A rebuild must decide whether to keep this denormalization pattern (fast list queries, point-in-time accuracy) or normalize with real FKs (stronger integrity, requires joins, loses point-in-time snapshot semantics).

### `Admin`
FirstName, LastName, Email (unique), Password (bcrypt), Role (Admin|SuperAdmin), YearGroup?, IsDisabled, LastLoginAt?

### `Member`
FirstName, LastName, Email (unique), Phone?, Password (bcrypt), StudentId?, GraduationYear (indexed w/ DepartmentId), DepartmentId (indexed), Company?, JobTitle?, Location?, LinkedInUrl?, Bio?, ProfilePictureUrl?, Status (Pending|Active|Suspended|Banned|Blocked, indexed, default Pending), LastLoginAt?, IsEmailVerified, EmailVerificationToken?, EmailVerificationSentAt?, RejectionCount, MemberNumber?, BanReason?, EmploymentStatus (Employed|Pensioner, default Employed, one-way transition), IsMembershipActive (default false), MembershipExpiry?, MembershipYearsPaid, LastMembershipPaidAt?, ReferralCode?, ReferredById?

### `Department`
Name, ShortCode?

### `Campaign`
Title, Description?, TargetAmount, AmountPerMember, PensionerAmountPerMember?, Deadline, Status (Active|Closed|Completed|Archived, indexed), CollectedAmount, PaidCount, YearGroups? (integer[]), BannerImageUrl?, YoutubeVideoUrl?, IsPaystackDisbursed, PaystackDisbursedAt?, PaystackDisbursedBy?, AllowOnlinePayments (default true), AllowManualPayments (default true), IsMembershipCampaign, MembershipYear?, BankAccount? (jsonb: AccountNumber/AccountName/BankName/Branch), MobileMoneyAccount? (jsonb: MobileMoneyNumber/Name/Provider enum MTN|Telecel|AT)

### `Contribution`
CampaignId (indexed) + Campaign snapshot (jsonb: Id/Title), MemberId (indexed) + Member snapshot (jsonb: Id/FirstName/LastName/Email/ProfilePictureUrl/MemberNumber), Amount, PaymentMethod (Paystack|Manual), Status (Pending|Confirmed|Rejected, indexed, default Pending), TransactionRef? (indexed), ProofUrl?, Notes?, ConfirmedAt?, ConfirmedBy?

### `PaymentTransaction` (raw Paystack ledger, separate from Contribution)
MemberId + Member snapshot, CampaignId + Campaign snapshot, Reference (unique index), Amount, Status (Pending|Confirmed|Failed), PaymentMethod?, GatewayResponse?, Channel?, Currency?, MembershipYears?, FailureMessage?, CallbackPayload? (raw JSON, for debugging), ProcessedAt?

### `AlumniEvent`
Title, Description?, StartDate (indexed), EndDate?, Venue, Capacity?, RsvpCount, IsTicketed, TicketPrice?, Status (Upcoming|Ongoing|Completed|Cancelled, indexed), YearGroups? (integer[]), GoogleLocationUrl?, BannerImageUrl?, ImageUrls? (jsonb array), YoutubeVideoUrls? (jsonb array)

### `EventRsvp`
EventId (indexed), MemberId (indexed), Status (default Confirmed). **Unique composite index (EventId, MemberId)**.

### `Job`
Title, Company, Location, Type, Description?, ApplyUrl?, Deadline?, Status (default Active, indexed), PostedBy, BannerImageUrl?, YearGroups?. Index on Type.

### `NewsPost`
Title, Content, Category (indexed), IsPinned, Status (Draft|Published, indexed, default Draft), PublishedAt? (indexed), AuthorId + Author snapshot, ImageUrls? (jsonb), YoutubeVideoUrls? (jsonb), YearGroups?

### `ForumCategory`
Name, Description?, SortOrder

### `ForumThread`
CategoryId (indexed) + Category snapshot, Title, AuthorId (indexed) + Author snapshot, IsPinned, IsClosed, ReplyCount

### `ForumPost`
ThreadId (indexed) + Thread snapshot, AuthorId (indexed) + Author snapshot, Content, IsDeleted

### `MentorProfile`
MemberId (indexed) + Member snapshot, Area, Bio?, MaxMentees, CurrentMenteeCount, Status (Pending|Approved|Rejected|Paused, indexed, default Pending), YearGroups?

### `MentorshipRequest`
MentorProfileId (indexed) + MentorProfile snapshot, MenteeId (indexed) + Mentee snapshot, Area, Message?, Status (Pending|Accepted|Rejected, indexed, default Pending)

### `Resource`
Title, Description?, Category (indexed), Type, ExternalUrl?, FileUrl?, BannerImageUrl?, UploadedBy?, DownloadCount (default 0), YearGroups?

### `MemberBadge`
MemberId (indexed) + Member snapshot, BadgeType (indexed: FirstContribution|EventAttendee3|Referrer|SuperReferrer|MembershipStreak2|MembershipStreak3|MembershipStreak5), Description?, EarnedAt

### `Spotlight`
MemberId (indexed) + Member snapshot, Title, Story, ImageUrl?, Status (Pending|Approved|Rejected, indexed, default Pending), FeaturedMonth?, AdminNotes?

### `Referral`
ReferrerId (indexed) + Referrer snapshot, ReferredEmail (indexed), ReferredMemberId? + ReferredMember snapshot, Status (Pending|Registered|MembershipPaid, default Pending)

### `ClassNote`
AuthorId (indexed) + Author snapshot, YearGroup (indexed), Content, ImageUrl?, LikeCount, IsDeleted

### `ClassNoteLike`
ClassNoteId (indexed), MemberId. **Unique composite index (ClassNoteId, MemberId)**.

### `NotificationPreference`
MemberId (**unique index**, one row per member), MembershipReminders/CampaignAlerts/EventReminders/JobAlerts/ClassNoteAlerts/SpotlightAlerts (booleans, default true)

### `Notification`
RecipientId (indexed, composite w/ RecipientType), RecipientType (Member|Admin, default Member), Title, Body, Type (see §3.0.5), IsRead (indexed), ReadAt?, RelatedEntityId?, RelatedEntityType? (Job|Campaign|Event|Spotlight|ClassNote|Contribution), ActionUrl?

### Snapshot value objects (jsonb-embedded, not their own tables)
`MemberSnapshot`, `CampaignSnapshot`, `EventSnapshot`, `ForumCategorySnapshot`, `ForumThreadSnapshot`, `MentorProfileSnapshot`.

### Migration history (schema evolution order)
Initial → added_google_map_url → AddSnapshotJsonbColumns → added_addition_prop → updated_contribution → added_download_flow_to_resources → added_compensation_to_campaign (pensioner amounts) → added_membership_revewal → updated_campaign_to_have_year → AddEmploymentStatusAndPensionerAmount → AddEngagementFeatures (badges/spotlights/referrals/class notes/mentorship) → AddNotification (most recent).

---

## 3.4 Integrations

### 3.4.1 Paystack
- Config: SecretKey, PublicKey, BaseUrl (`https://api.paystack.co`), CallbackUrl.
- `InitializePaymentAsync` → `POST /transaction/initialize` (amount in kobo/pesewas — decimal × 100).
- `VerifyPaymentAsync(reference)` → `GET /transaction/verify/{reference}`.
- **Webhook**: `POST api/v1/callbacks/paystack` (Member.Api only), HMAC-SHA512 signature check (currently logged, not enforced), async processing, always 200 response.
- **Business flows**: one-off campaign contribution, membership renewal (member & guest), reconciled via both synchronous client-polling verify and async webhook, converging on one idempotent handler.
- **Payment logic details**: creates a `Pending PaymentTransaction` before redirecting to Paystack; reference cached in Redis 24h for reconciliation; pensioners charged `PensionerAmountPerMember` if set else `AmountPerMember`; on confirmed success, upserts a `Contribution` (Confirmed), increments campaign totals, recomputes `Member.IsMembershipActive` (all years from graduation year through current year must be paid), sets `MembershipExpiry` to Dec 31 23:59:59 UTC of the current year, and **auto-approves** a still-`Pending` member (assigns sequential `MemberNumber` = `{InstitutionPrefix}-{GraduationYear}-{0001}`) — this is a second, payment-triggered approval path alongside manual admin approval.
- **Admin disbursement tracking**: campaigns track `IsPaystackDisbursed`/`PaystackDisbursedAt`/`PaystackDisbursedBy` since Paystack does not auto-transfer collected funds to the campaign's real bank account in this design — an admin must mark disbursement manually.

### 3.4.2 Email
Two interchangeable providers behind `IEmailService`; currently wired to Mailhog/SMTP (Mailtrap available but disabled in config).
- **SMTP (active)**: MailKit-based, HTML templates rendered from disk with `{{variable}}` placeholder substitution, generated fallback template if file missing. Templates on disk: `email-verification.html` (OTP), `email-verification-link.html`, `reset-password.html`, `referral-invitation.html`, `notification.html`.
- **Mailtrap API (available, disabled)**: HTTPS transactional send API using template UUIDs; configured templates include ResetPassword, Registration, AdminRegister, ContributionConfirmed, EventRsvpConfirmed, EmailVerification, EmailVerificationLink, ReferralInvitation — a rebuild should decide which of these to actually wire up (some are configured but not triggered in current code; contribution-confirmed/rejected currently only fire in-app notifications, not email).
- All email sends are fire-and-forget; failures are logged, not thrown.

### 3.4.3 Storage
S3-compatible object storage (works against any S3-compatible endpoint — e.g., DigitalOcean Spaces, MinIO).
- Config: AccessKey, SecretKey, BucketName, Region, Endpoint, CdnEndpoint, FolderName (default "alumni").
- Uploads are stored with public-read ACL; URLs returned are CDN URLs (`{CdnEndpoint}/{BucketName}/{folder}/{fileName}`), filenames randomized (`{guid}{ext}`).
- **What gets uploaded**: member profile pictures (≤5MB, jpg/jpeg/png/webp/gif), campaign/event/job/news banner images, event photo galleries, resource files + banners, generic admin uploads (image ≤5MB / file ≤20MB).

### 3.4.4 Redis
See §3.0.3 — session/OTP/refresh-token cache only, no queues or pub/sub.

### 3.4.5 Common SDK Building Blocks
- `ApiResponse<T>` / `IApiResponse<T>` / factory helpers (Ok/Created/NotFound/BadRequest/Unauthorized/Forbidden/Conflict/ServerError) mapping to real HTTP status — the uniform envelope used by every endpoint.
- `AuthData` — principal shape extracted from JWT claims.
- `BaseFilter` / `PagedResult<T>` — the generic pagination contract.
- `BearerTokenConfig` — shared JWT config (two signing keys, one issuer/audience).
- A single shared `ResponseDtos.cs` file containing nearly all cross-cutting response DTOs used by both APIs.

---

# 4. Consolidated Cross-Cutting Rebuild Considerations

These are implementation details and quirks of the current system that a SaaS rebuild should address deliberately rather than silently inherit:

1. **No real foreign keys** anywhere in the database — relationships rely on indexed string IDs plus JSONB "snapshot" duplication for historical accuracy. Decide: keep denormalization for point-in-time accuracy and query speed, or normalize with FKs for stronger integrity.
2. **Paystack webhook signature validation is computed and logged but not enforced** — must be fixed for production-grade security.
3. **Dual membership-approval paths**: a member can be approved either manually by an admin, or automatically the instant their membership payment clears. Both paths must be preserved or intentionally redesigned.
4. **Two independent JWT signing keys** for the two APIs/audiences (deliberate token isolation) — worth preserving conceptually even if implemented differently (e.g., via `aud` claims) in a rebuild.
5. **Role/permission enforcement is inconsistent and largely client-side on the frontend** (nav visibility, ad hoc page checks) — a rebuild should implement a single server-enforced permission matrix (entity × action × role × year-group scope) and not rely on hiding UI as a security boundary.
6. **No auth middleware/server-side route protection** on either frontend — both admin and member portals are guarded only by client-side redirects; API authorization is the only real boundary.
7. **CORS is wide open** on both APIs — must be restricted per environment/tenant in a SaaS rebuild.
8. **No scheduled/cron jobs exist** — no membership-expiry sweep, no digest emails, no automated reminders. A SaaS rebuild should likely add these (e.g., approaching-expiry reminders, weekly digests, arrears follow-ups).
9. **Hardcoded portal base URLs** in the notification dispatcher (`localhost:3000`/`3001`) — must be externalized/configurable, and become tenant-aware in a multi-tenant SaaS.
10. **Auth tokens stored in plain localStorage** on both frontends (not httpOnly cookies) — a security hardening opportunity.
11. **Pensioner-rate business logic is duplicated across multiple frontend locations** rather than centralized — should be a single backend-computed value in a rebuild.
12. **Client-side-only features with no backend persistence**: "saved resources" (member app), notification preferences (admin app) — decide whether these need real backend sync in the SaaS version.
13. **Certificate generation is entirely client-side** (canvas + PDF) with no backend certificate record — a rebuild might want a durable, server-generated/stored certificate artifact instead.
14. **CSV exports are built client-side** by re-fetching large pages (up to 5000 rows) rather than using true server-side streaming exports (though a generic streaming export endpoint exists server-side but is unused by the current admin UI) — a rebuild should standardize on server-side export generation for scalability.
15. **Multi-tenancy is entirely absent** from the current design (single university, single dataset) — the biggest structural change for a SaaS rebuild is introducing tenant isolation (e.g., a `TenantId`/`OrganizationId` on every entity, tenant-scoped auth, tenant-aware branding/URLs/email templates/storage folders/Paystack sub-accounts) since the current system assumes exactly one alumni association.
16. **Dead/incomplete features** noted in each portal section above (broken edit-route links, unused mutations, disabled ticketing, view-only mentorship requests, unused components) should be triaged: either finish them properly in the rebuild or consciously drop them as out of scope.
17. **Notification delivery is polling-based** (30s intervals) rather than real-time — a rebuild could adopt WebSockets/SSE/push notifications for a more modern SaaS experience.
18. **Default admin bootstrap** (`DataSeeder` creates a hardcoded default SuperAdmin account on first run) is unsuitable for a multi-tenant SaaS and needs a proper tenant/organization onboarding flow instead.
