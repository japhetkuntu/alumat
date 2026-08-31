# AlumUnion dashboard design system

Covers the three dashboard apps — `apps/institution`, `apps/platform`, `apps/member` (the `(portal)` routes; the marketing pages under `apps/member/src/app/_marketing` and `why-not-whatsapp` have their own established direction from an earlier pass and aren't covered here). Shared primitives live in `packages/ui/src/components/`.

## Hard constraint — tenant theming

`apps/member/src/lib/theme.ts`'s `themeStyleVars()` overrides `--brand-primary`/`--primary`/`--border`(-info)/etc. at runtime per institution, for both the institution portal and the member portal. **Never hardcode a color that bypasses this chain** — always route through the existing semantic tokens (`var(--primary)`, `var(--border-emphasis)`, etc.), not literal hex/rgb, on anything that renders inside a tenant-branded surface. The platform app has no tenant theming and is free to commit to fixed values.

## Depth strategy — borders only, alpha over foreground

`--card-radius: 0` everywhere (flat/Linear-style, deliberate — don't introduce radius on cards/tables). Elevation is **borders, not shadows** — `--card-shadow`/`--card-shadow-hover` are `none` in all three apps. Reserve real shadows for genuine floating overlays (dropdowns, modals, popovers) — never for a card sitting flat on the page.

Border tokens are alpha-over-the-app's-own-foreground color, not flat hex, so a border always reads correctly against whatever surface sits under it and importance can step without picking new colors:

- `--border-subtle` — the lightest step. Use for structure that shouldn't compete (rarely needed given the standard token already reads quietly).
- `--border` — the standard, used everywhere by default (unchanged from before this pass, just re-derived as alpha-over-foreground instead of a flat hex in institution/platform).
- `--border-emphasis` — for a card that's asking for action or carries more weight than its neighbors (a "needs attention" list with items in it, a pending-approvals card, a danger-zone card). Don't reach for this by default — it only means something because most cards don't use it.

All three apps register these as Tailwind utilities: `border-subtle`, `border` (default), `border-emphasis`. Light and dark mode both have all three steps defined.

## Spacing

Unchanged from before this pass — institution and member use `--space-card: 20px` / `--space-gap: 16px`; platform uses `--space-card: 28px` / `--space-gap: 24px` ("Paystack-style", a deliberate, coherent divergence for the internal ops tool — don't unify it with the other two).

## Typography

`Inter` (sans) + `Lora` (serif display, `var(--font-display)`) in all three apps. The serif is for **numbers and titles that should carry weight**, not for every heading:

- Institution: already used systematically on `StatCard` values and page headings — keep doing this.
- Member: used on ~18 pages for headings/amounts/titles — the established pattern, keep extending it to new pages the same way.
- Platform: was loaded but **never applied** before this pass. Now used on the dashboard's hero `StatCard` value (via `StatCard` itself, which always renders `value` in `font-display`). Extend it the same way to any other page that gets a hero metric — not to routine body text.

## Component patterns

### `StatCard` (`packages/ui/src/components/stat-card.tsx`)

```
StatCard — variant="default": 9-10px icon badge (w-9/10 h-9/10) · value clamp(1.15rem,3.2vw,1.6rem) · border-border
StatCard — variant="hero":    11-12px icon badge, filled solid           · value clamp(1.75rem,4.6vw,2.5rem) · border-border-emphasis
```

Rule: **exactly one `variant="hero"` per stat row, the rest stay `"default"`.** The hero is the number that answers "what do I check first" for that audience — money collected (institution), platform revenue (platform). Never make a whole row hero, never make zero of them hero on a dashboard that has a clear lead metric. Layout pattern for a hero + row of 3:

```tsx
<div className="grid grid-cols-1 lg:grid-cols-[minmax(280px,1.3fr)_2fr] gap-3.5 items-stretch">
  <StatCard variant="hero" ... />
  <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
    <StatCard ... /> <StatCard ... /> <StatCard ... />
  </div>
</div>
```

`tone="accent"` alternates the icon color to the institution's brand-accent instead of primary — use sparingly (one or two cards in a row at most), not as a rotation.

`StatCardSkeleton` takes the same `variant` prop — always match it to the real card it's standing in for, so loading→loaded doesn't jump size.

### `PageHeader` / `PageShell` (`packages/ui/src/components/page-shell.tsx`)

Already existed, wasn't adopted anywhere in the audit. `PageHeader` (`eyebrow`/`title`/`description`/`children` action slot) is the intended source for a page's title — worth wiring into pages that still hand-roll their own `<h1>` block in a follow-up pass. Not retrofitted everywhere in this pass; the topbar page-title (below) doesn't depend on it.

### Sidebar shells (`institution-layout.tsx`, `platform-layout.tsx`, `member-layout.tsx`)

- Nav item weight: the home/dashboard item is visually distinct from leaf items — `text-[13.5px] font-semibold` + `py-2.5` + icon `size={17}` vs `text-[13px] font-medium` + `py-2` + icon `size={16}` for everything else, plus `strokeWidth={2.25}` on active/home items vs `2` for the rest.
- Group headers (institution, platform): a short `h-px w-2.5` divider tick before the uppercase label, not just floating text — keep this for any new group.
- Press feedback: `active:scale-[0.99]` on inactive nav links, `transition-all duration-150 ease-[cubic-bezier(0.2,0,0,1)]` — this curve (not a default Tailwind ease) is the house curve for sidebar/interactive-row motion in this pass.
- Brand mark: pull `theme.iconUrl || theme.logoUrl` where a per-tenant query already runs (institution, member); fall back to a solid `bg-primary` square with the portal name's first letter — never a static generic letter. Platform has no tenant, keeps its fixed `alumunion-mark.svg`.
- **Topbar page title**: every shell now derives the current page's title from its own nav-items list (`usePathname()` matched against the flat/grouped nav array) and renders it left-aligned in the desktop topbar (`text-[14px] font-semibold`, `font-display` on member to match its serif-titles convention). Institution/platform also mirror it into the mobile header in place of the old static "X Portal" string. Extend this pattern — don't hand-roll a different page-title source per page.

### Border-emphasis for "this card wants action"

Applied so far: institution dashboard's "Pending approvals" card (`borderColor: pendingApprovals > 0 ? var(--border-emphasis) : undefined`), platform dashboard's "Needs attention" card (same pattern keyed off `attentionList.length`), institution settings' "Log out" card (permanent — it's always a distinct destructive action, tinted `color-mix(in oklch, var(--destructive) 4%, var(--card))` background + `color-mix(in oklch, var(--destructive) 30%, transparent)` border, not the full-strength `--destructive` color). Reuse this conditional-border-color pattern rather than inventing a new "urgent card" component.

## Motion

- Sidebar/list-row interactive elements: `transition-all duration-150 ease-[cubic-bezier(0.2,0,0,1)]`.
- `Button` (packages/ui) already has `active:scale-[0.98]` and the same curve — don't override per-instance, it's already correct.
- Toggle switches: `duration-200 ease-[cubic-bezier(0.2,0,0,1)]` + `active:scale-[0.96]` on the track (updated this pass in institution's `Toggle`; apply the same values if any other app hand-rolls a switch instead of using a shared one).
- Never `transition: all` / `transition-all` without also naming it works here only because the whole className is a short, deliberate list — if a transition starts picking up unrelated properties, name them explicitly instead.

## Tables

`packages/ui/src/components/table.tsx`'s `TableRow` already carries `hover:bg-muted/30` — every table gets row-hover for free, don't re-add it per page. Do add `tabular-nums` (and `font-medium` if it's a lead figure like revenue) to any numeric `TableCell` — this was missing on platform's institutions list (member count, revenue) and is now fixed there; check for the same gap before shipping a new table.

## What's NOT done yet (deliberately out of scope for this pass)

This pass covered: foundation tokens, all three sidebar/topbar shells, and one dashboard + one list/form page per app (institution: `dashboard`, `members` (light touch, already solid), `settings`; platform: `dashboard`, `institutions`; member: `dashboard` confirmed already-correct, no rebuild needed). The remaining ~50 pages across the three apps (jobs, events, contributions, forum, mentorship, reports, business-directory, etc.) still use the pre-existing generic patterns (flat equal-weight cards, no hero metric where one would apply, hand-rolled headers instead of `PageHeader`). Bring them forward using the patterns above rather than inventing new ones — the point of this doc is that a future pass doesn't have to rediscover these decisions.
