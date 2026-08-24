# Align Settings and Billing page headers to the Institution Portal list-page pattern

Written against: 321479c

## Evidence chain

- Surface: `frontend/apps/institution/src/app/(dashboard)/settings/page.tsx`, `frontend/apps/institution/src/app/(dashboard)/billing/page.tsx`
- Problem: These two routes are the only two of the 19 sibling routes under `AdminSidebar`'s nav (`frontend/apps/institution/src/components/admin/admin-layout.tsx` `baseNavItems`) that use a different page-container width and a different `h1` scale/weight than every other route.
- Design evidence: 17 sibling routes share one implementation:
  - Container: `<div className="p-[26px] max-w-[1240px] mx-auto ...">` (or a route-specific max-width variant such as `max-w-[1300px]` on Members, `max-w-[1100px]` on Admins — all still `p-[26px]`)
  - Header: `<h1 className="text-[25px] font-bold m-0">{Title}</h1>` immediately followed by `<p className="text-muted-foreground text-[13px] mt-1.5">{subtitle}</p>`
  - Exemplars: `members/page.tsx:154-158`, `news/page.tsx:222-226`, `campaigns/page.tsx:285-288`, `dashboard/page.tsx:63-66`
  - Settings and Billing instead use `<div className="p-6 lg:p-10 max-w-5xl mx-auto">` and `<h1 className="text-3xl font-extrabold tracking-tight">` (`settings/page.tsx:121,124`; `billing/page.tsx:30,33`)
- Owner: no shared header/container component exists yet — each route inlines the same markup independently.
- Scope and affected surfaces: `settings/page.tsx`, `billing/page.tsx` only. No other route deviates.
- Uncertainty: none — the pattern and the two outliers are both directly observable in source.

## Design decision

Bring the Settings and Billing page containers and `h1`/subtitle markup in line with the pattern used by the other 17 sibling routes in the same nav, so moving between any two items in the sidebar no longer changes the page's base scale and width. Do not touch tab content, form fields, cards, or any behavior — this is header-region and outer-container markup only.

## Reuse

- Container pattern: `p-[26px] max-w-[1240px] mx-auto` (from `news/page.tsx:222`, `dashboard/page.tsx:63`)
- Header pattern: `<h1 className="text-[25px] font-bold m-0">` + `<p className="text-muted-foreground text-[13px] mt-1.5">`
- Exemplar: `frontend/apps/institution/src/app/(dashboard)/news/page.tsx:222-231`

No new primitive is required — this is a direct copy of an existing, already-repeated pattern.

## Changes

1. `frontend/apps/institution/src/app/(dashboard)/settings/page.tsx`
   - Change: line 121 outer `<div>` className from `"p-6 lg:p-10 max-w-5xl mx-auto"` to `"p-[26px] max-w-[1240px] mx-auto"`. Line 124 `<h1>` className from `"text-3xl font-extrabold tracking-tight"` to `"text-[25px] font-bold m-0"`. Line 125 subtitle `<p>` className from `"text-muted-foreground text-sm font-medium"` to `"text-muted-foreground text-[13px] mt-1.5"`.
   - Preserve: the `header` wrapper's `flex items-end justify-between gap-4 mb-6 animate-in fade-in slide-in-from-bottom-3 duration-700` classes, the tabs row, and all tab content below — none of that is part of this finding.
   - Verify: page renders at the same width/scale as Members or News; tabs and form content still align inside the narrower `max-w-[1240px]` container without overflow or clipping.

2. `frontend/apps/institution/src/app/(dashboard)/billing/page.tsx`
   - Change: line 30 outer `<div>` className from `"p-6 lg:p-10 max-w-5xl mx-auto"` to `"p-[26px] max-w-[1240px] mx-auto"`. Line 33 `<h1>` className from `"text-3xl font-extrabold tracking-tight"` to `"text-[25px] font-bold m-0"`. Line 34 subtitle `<p>` className from `"text-muted-foreground text-sm font-medium"` to `"text-muted-foreground text-[13px] mt-1.5"`.
   - Preserve: the `header` wrapper's existing layout classes, the "Change plan" button, the illustrative-data notice banner, and the usage/invoice cards below.
   - Verify: page renders at the same width/scale as Members or News; the two-column `grid-cols-[1.2fr_.8fr]` card layout below still reads cleanly inside the narrower container.

## Scope

- Inherit: `settings/page.tsx`, `billing/page.tsx` only — these are the only two routes with this deviation.
- Verify: after the change, visually diff Settings and Billing against Members/News/Dashboard to confirm the header block now matches; confirm no other route was touched.
- Exclude: any change to tab logic, form validation, card content, or the `--card-radius`/token system — none of that is implicated by this finding.

## Validation

- Product: navigate from Members → Settings → Billing → Campaigns in the sidebar; the outer page frame (width, header scale) should feel continuous, not like a scale jump.
- Interface: check at desktop width (page is `lg:`-oriented already) and at typical laptop widths (1280–1440px) where `max-w-[1240px]` vs `max-w-5xl` (`1024px`) previously produced visibly different content widths.
- System: confirm no new CSS classes or tokens were introduced — this reuses exact class strings already present elsewhere in the same directory.
- Repository: `cd frontend/apps/institution && npx tsc --noEmit && npx eslint src/app/\(dashboard\)/settings/page.tsx src/app/\(dashboard\)/billing/page.tsx` → no errors.

## Stop conditions

- Stop if Settings or Billing content (tab panels, usage bars, invoice table) turns out to depend on the wider `max-w-5xl` container and visibly breaks/overflows at `max-w-[1240px]` — in that case flag it rather than forcing the narrower width.

## Design documentation

- None — no `DESIGN.md` exists for this surface; the convention remains implicit in the repeated route markup.
