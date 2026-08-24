# Redesign Brief: Alumni Member Portal — Public Landing Page (for Skywork.ai)

## Context

Redesign the **public marketing landing page** at the root URL (`/`) of a multi-tenant university alumni platform. This is the page a prospective or returning alumnus sees before logging in — its job is to explain the portal and convert visitors into "Join now" / "Sign in" clicks. It is NOT the logged-in dashboard (that's a separate, already-specified redesign).

**Multi-tenant / white-label**: every institution that uses this platform gets this same landing page reskinned with their own primary brand color, logo, and institution name — nothing here should be designed as a single fixed color. Two content pieces are also editable per-institution by admins (flagged below) — the design must degrade gracefully when that content is empty/default.

**Do not invent new sections or remove functionality** described below. If you want to propose something new (e.g., testimonials, an FAQ, a video), list it separately as a "suggested addition."

**Tech constraints for the deliverable**: will be implemented in Next.js + Tailwind CSS with CSS-variable-driven theming (no hardcoded hex colors) and Lucide icon set. Provide specs/redlines an engineering team can implement directly.

---

## Design Tokens to Respect

- **Color**: fully driven by CSS variables generated at runtime from one institution `primaryColorHex` (e.g. `--primary`, `--background`, `--foreground`, `--muted`, `--secondary`, `--border`, `--color-background-info`, `--color-border-info`, `--color-text-info`). Validate the design against at least two very different brand colors (e.g. deep blue and warm orange) to prove it holds up — do not rely on any specific hue for a design effect.
- **Typography**: two-font pairing — sans-serif for body/UI (`--font-sans`, currently Inter), serif/display for headings (`--font-display`, currently Lora). All `<h1>`/`<h2>` on this page use the display font.
- **Radius scale**: xs 4px / sm 8px / md 12px / lg 18px / xl 24px — cards generally use `rounded-2xl` (24px).
- **Motion**: the current page uses scroll-triggered fade-up-on-enter animations for nearly every section/card (IntersectionObserver-based, staggered delays per grid item), an animated count-up for the stats band, and subtle hover lift/scale on cards and photos. Preserve this "content reveals as you scroll, numbers count up" feel — it's a deliberate part of the page's personality, not decoration to strip out.

---

## Section-by-Section Specification (in page order)

### 0. Announcement banner (conditional, admin-editable)
A thin, full-width, brand-colored strip pinned above the header — only rendered when an institution admin has enabled a "news banner" in settings. Content: a short message + an optional link (either an anchor-scroll link to an in-page section, shown with a `↓`, or an external link shown with a `→`). Includes a small star icon on the left and a dismiss (×) button on the right that hides it for the session. **Design this as an optional element the page must look complete without** — most institutions will have it off by default.

### 1. Header / navbar
Sticky, translucent/blurred white background, bottom border, full width, ~64px tall. Contents left-to-right:
- Logo mark (rounded-square icon in brand color) + wordmark "Alumni Portal" (institution name), links to `/`
- Center/right nav links (desktop only): **Features**, **Stories**, **How it works** — each smooth-scrolls to an in-page anchor, not a real navigation
- Right-aligned actions: **"Sign in"** (ghost/text button) → `/login`, **"Join now"** (primary button with arrow icon) → `/register`
- Mobile: nav links + actions collapse into a hamburger menu that opens a full-screen overlay panel (nav links stacked, then Sign in / Join now buttons stacked at the bottom)

### 2. Hero
Two-column layout (desktop), stacked on mobile, with a decorative soft radial glow in the background (brand-colored, very subtle) and a faint background pattern.

**Left column:**
- Small pill/eyebrow label: "Verified alumni network" (dot + uppercase micro-text, brand-tinted background)
- Large display-font headline (~2.4rem–3.75rem responsive): *"The alumni portal **built** to connect, support, and grow together."* — the word "built" is brand-colored with a decorative underline stroke beneath it
- Supporting paragraph (max ~46 characters per line): *"One place for jobs, campaigns, mentorship, events, and verified connections — built to feel modern, reliable, and easy for graduates at every stage of their career."*
- Two CTA buttons side by side (stacked on mobile): **"Join the network →"** (primary, large) and **"Already a member"** (outline, large) → `/login`
- A horizontal "trust strip" of 4 inline icon+label pairs (not boxes, just icon + short text): Verified access, Jobs from grads, Mentor matching, Events & campaigns

**Right column:**
- One large photo (rounded-2xl, ~560px tall, students collaborating) with a diagonal dark gradient scrim
- Overlaid on the photo: top-left a small pill badge with a pulsing dot ("Built around alumni needs"); bottom an overlaid headline *"One network. Every graduate, wherever they are."* + a "Create your free account →" button sitting directly on the photo
- A floating stat card overlapping the top-right corner of the photo (white card, shadow): large number "5,000+" + "Verified graduates" caption

### 3. Alumni leadership spotlight (editorial/human-interest section)
Full-width band with a tinted background, separated by top/bottom borders. Purpose: showcase real people/leadership moments (currently a VC appointment + outgoing VC tribute — this is realistic sample content the institution would replace with its own).
- Section intro: eyebrow "Alumni in leadership" + display headline "A graduate will lead their alma mater." + supporting paragraph.
- **2-column grid of "spotlight cards"** (this exact card pattern also appears in the logged-in portal's Spotlights feature, so keep it consistent):
  - Photo banner (~210px tall) with a bottom-heavy dark gradient scrim, person's name + role overlaid in white text at the bottom
  - A small badge in the top-left of the photo (icon + label, e.g. "New appointment" / "A tribute", glassy/blurred pill on top of the image)
  - Below the photo: a short teaser paragraph, then a "Read more / Show less" toggle (chevron icon) that expands a longer paragraph with a top divider
- Below the grid, an inline callout box (tinted background, icon): *"Are you an alumnus in a leadership role? The alumni spotlight celebrates your achievements across the network. **Join to be featured.**"* (link to `/register`)

### 4. Stats band
Full-bleed dark section (uses `--foreground` as background — i.e. inverts to a near-black/near-brand-dark band), no card chrome — just a left-aligned intro block and a horizontal row of numbers.
- Left block (fixed ~280px on desktop): small uppercase label "Alumni impact" + display headline "What alumni are already doing here."
- Right: a 4-column grid (2 cols on mobile) of stat entries, divided by thin vertical rules, each with a large tabular-number count-up value (e.g. "5,000+", "120+", "GHS 2M+", "300+") and a label + short description beneath. Numbers animate counting up from 0 when the section scrolls into view.
- Current stats: **5,000+ Alumni registered**, **120+ Countries represented**, **GHS 2M+ Raised in campaigns**, **300+ Jobs posted**.

### 5. Features grid ("What's inside")
Tinted background section.
- Intro: eyebrow "What's inside" + headline "One portal for every alumni need." + supporting paragraph.
- **Bento-style grid** (2 cols mobile / 4 cols desktop) of 8 feature cards, where 2 of the 8 are visually larger (span 2 columns, bigger icon, horizontal icon+text layout instead of stacked) to break up the grid rhythm. Each card: small icon in a tinted rounded square, small uppercase category label above the title, semibold title, short description.
  - Features (label — title — description): **Careers** — "Jobs inside the network" — roles posted by alumni employers first, before public boards *(large card)*; **Directory** — "Find any grad in seconds" — search by class year, department, company, country; **Contributions** — "Fund what matters" — alumni-led campaigns for labs, scholarships, campus improvements; **Class Notes** — "Keep the conversation going" — post milestones, share knowledge by graduation year; **Events** — "Never miss a reunion" — homecomings, webinars, networking nights, RSVP in one place; **Mentorship** — "Give back. Get ahead." — connect with alumni who've done what you're trying to do *(large card)*; **Spotlights** — "Celebrate the wins" — recognition for alumni making a difference; **Notifications** — "Hear about what you care about" — jobs, campaigns, event invites, you choose.
- A 9th "filler" tile closes out the grid row: a full-width (2-col span), solid brand-colored card that reads *"That's everything — see it live / Create a free account and explore the full portal"* with an arrow, linking to `/register` — functions as a mid-page CTA disguised as a grid tile.

### 6. Stories / use cases ("The three reasons most alumni join") — admin-editable
- Intro: eyebrow "Real situations" + headline "The three reasons most alumni join." + supporting paragraph.
- **3-column grid of photo cards** (the middle card offset slightly lower on desktop for visual rhythm), each: a photo banner (~160px) with a small uppercase eyebrow label overlaid bottom-left, then below the photo a display-font "scenario" headline and a short descriptive paragraph.
  - Default content: **Career** — "The job that never reached a public board" — alumni employers post directly to the portal first; **Giving** — "The campaign that needed 200 people" — alumni-led campaigns pool contributions from graduates worldwide; **Mentorship** — "The mentor who's already done it" — find a graduate ahead of you on your exact path.
  - *This content (icon, eyebrow, scenario, description, image) is editable per-institution by admins — design must look complete with the default content above AND with institution-supplied replacements of arbitrary length.*
- Centered "Join the network →" button beneath the grid.

### 7. How it works (3-step timeline)
Tinted background, centered intro (eyebrow "Getting started" + headline "Three steps. That's all it takes.").
- **3-column layout connected by a thin horizontal line** running behind the step numbers (desktop only) — not boxed cards, more like a timeline/process diagram. Each step: a filled brand-colored circle icon badge, small "Step 01/02/03" label, semibold title, description.
  - **01** Shield icon — "Register in under two minutes" — create your account with alumni details, no long forms or waiting for approval emails.
  - **02** Map-pin icon — "Build out your profile" — add career, company, location; more context makes you easier to find.
  - **03** Zap icon — "Use it" — browse jobs, back a campaign, request a mentor, or show up in the directory.

### 8. Final CTA band
Full-bleed, solid brand-primary-colored background with soft decorative radial-glow circles (top-left and bottom-right, very subtle white glows).
- Left: small uppercase label "Take the next step" + large display headline "Your journey shaped you. Now shape what comes next." + supporting paragraph, all in white/near-white text.
- Right: two stacked buttons — **"Create my account →"** (white background, brand-colored text — the one high-contrast inverted button on the whole page) → `/register`, and **"Sign in instead"** (outline, white text/border, transparent) → `/login`.

### 9. Footer
Simple single-row footer (stacks on mobile): logo + wordmark on the left, copyright line centered (© year + institution name), and a right-aligned nav of 3 text links: **Sign in** (`/login`), **Join now** (`/register`), **Campaigns** (`/payment-campaign`).

---

## Design Priorities

1. **This page must convert.** Every section should build toward one of two actions — "Join now" or "Sign in" — both of which should feel reachable from anywhere on the page (header is sticky; multiple CTAs are placed throughout, not just at the top and bottom).
2. **Feels like a real institution, not a generic SaaS template.** The leadership spotlight and stories sections exist specifically to make the page feel populated by real people and real outcomes, not abstract feature bullets — keep a strong photography/human presence throughout, not just icon grids.
3. **White-label resilience.** Confirm the design works with a light, a dark, and a saturated brand primary color, and that all the tinted/info backgrounds (`--color-background-info`, `--secondary`, `--muted`) remain legible against arbitrary brand hues.
4. **Content flexibility.** The news banner and the "stories" section are admin-editable and can be empty, short, or long — design must not break in any of those states.
5. **Motion with restraint.** Scroll-reveal and count-up animations should feel premium and confidence-building, not gimmicky — respect `prefers-reduced-motion`.

## Deliverables Requested

- Full-page high-fidelity design, desktop + mobile, for the sequence of sections above (in order)
- The same page shown in at least one alternate brand-color theme, to prove the token system works
- States for the announcement banner (present/absent) and the stories section (default content vs. a differently-worded institution override)
- Redlines/specs sufficient for a React/Tailwind engineering team to implement (Next.js, Tailwind CSS, Lucide icons)
