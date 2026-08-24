# Reuse the shared Badge component for Job Board list status pills

Written against: 321479c (plus two prior improve-ui fixes: header alignment + empty-state copy parity)

## Evidence chain

- Surface: `frontend/apps/institution/src/app/(dashboard)/jobs/page.tsx` (Job Board list view)
- Problem: `job.status` is rendered with a hand-rolled `<span>` and a locally-defined color map instead of the shared `Badge` component.
- Design evidence:
  - `jobs/page.tsx:36-40` — `const statusColors: Record<string, string> = { Active: "bg-emerald-500/10 text-emerald-600 border-emerald-200", Closed: "bg-rose-500/10 text-rose-600 border-rose-200", Draft: "bg-slate-500/10 text-slate-600 border-slate-200" };`
  - `jobs/page.tsx:318` — `<span className={\`px-2 py-0.5 rounded-full text-[9px] font-black uppercase tracking-widest border ${statusColors[j.status] ?? "bg-muted text-muted-foreground border-border"}\`}>{j.status}</span>`
  - `jobs/[id]/page.tsx:35-39` — the same three status values, on the same field, mapped to the shared `Badge` component's `variant` prop: `{ Active: { variant: "success", ... }, Closed: { variant: "secondary", ... }, Draft: { variant: "warning", ... } }`, consumed via `<Badge variant={sc.variant}>` at `jobs/[id]/page.tsx:...` (rendered in the detail-page breadcrumb area).
  - `src/components/ui/badge.tsx` defines the `variant` contract (`default/secondary/destructive/outline/success/warning/info/neutral`) all other list pages use for status pills (e.g. `campaigns/page.tsx:373`, `members/page.tsx:251`).
- Owner: `src/components/ui/badge.tsx` (`Badge`, `badgeVariants`).
- Scope and affected surfaces: `jobs/page.tsx` only — its detail page (`jobs/[id]/page.tsx`) is already correct and is the exemplar.
- Uncertainty: none — the correct status→variant mapping for these exact three values already exists and is proven correct in the detail page.

## Design decision

Replace the raw `<span>` status pill in the Job Board list card with `<Badge>`, using the same status→variant mapping already established in `jobs/[id]/page.tsx`. Leave the job-type pill (`typeColors`, line 315) untouched — no evidence was found that it contradicts any existing component contract; it is not part of this finding.

## Reuse

- Component: `Badge` from `@/components/ui/badge` (`variant` values: `success`, `secondary`, `warning`)
- Exemplar mapping: `jobs/[id]/page.tsx:35-39` (`statusConfig`)
- Exemplar usage in a list card: `campaigns/page.tsx:373` (`<Badge variant={statusVariant[c.status]}>{c.status}</Badge>`)

No new primitive needed.

## Changes

1. `frontend/apps/institution/src/app/(dashboard)/jobs/page.tsx`
   - Change: add `import { Badge } from "@/components/ui/badge";` near the other UI imports (after line 12, alongside `Card, CardContent, CardHeader, CardTitle`). Add a local status→variant map mirroring `jobs/[id]/page.tsx`'s `statusConfig`:
     ```ts
     const statusVariant: Record<string, "success" | "secondary" | "warning"> = {
       Active: "success",
       Closed: "secondary",
       Draft: "warning",
     };
     ```
     placed next to the existing `statusColors`/`typeColors` constants (around line 36). Replace the `<span>` at line 318 with:
     ```tsx
     <Badge variant={statusVariant[j.status] ?? "neutral"} size="sm">{j.status}</Badge>
     ```
   - Preserve: the `statusColors` constant is no longer used by this replaced span — if nothing else in the file references it, remove it as dead code in the same change (verify with a grep before deleting). Preserve the adjacent `typeColors` span and its markup exactly as-is; preserve the `Badge`'s position inside the `flex gap-1.5 flex-wrap justify-end` wrapper alongside the type pill.
   - Verify: Job Board list cards show a `Badge` (pill shape, `rounded-full`, token-driven background/text color) for status, visually consistent with how `campaigns/page.tsx` and `members/page.tsx` render their status column, and matching the color that `jobs/[id]/page.tsx` already shows for the same job.

## Scope

- Inherit: `jobs/page.tsx` only.
- Verify: confirm `jobs/[id]/page.tsx` is untouched (it's already correct — this plan doesn't modify it), and that removing `statusColors` (if dead) doesn't break another reference in the same file.
- Exclude: `typeColors`/the job-type pill, `jobs/[id]/page.tsx`, and any other list page — none of those are implicated by this finding.

## Validation

- Product: open Job Board with jobs in each of the three statuses (Active/Closed/Draft) and confirm each renders as a `Badge` matching the color the same job shows on its detail page.
- Interface: default list/grid viewport (`grid-cols-1 md:grid-cols-2 xl:grid-cols-3`) — no layout change expected since `Badge`'s `size="sm"` closely matches the prior span's compact sizing; check the card header row doesn't wrap awkwardly at narrow widths.
- System: confirm no second, parallel status-color system remains in this file after the change (grep for `statusColors` should return no usages, or the constant should be removed).
- Repository: `cd frontend/apps/institution && npx tsc --noEmit && npx eslint "src/app/(dashboard)/jobs/page.tsx"` → no errors, no unused-variable warning for `statusColors` if left in place unused.

## Stop conditions

- Stop if `Badge`'s fixed `rounded-full`/padding scale visibly clashes with the card's compact `text-[9px]` sibling type-pill (e.g. mismatched heights) — in that case, flag the sizing mismatch rather than forcing it, since `Badge`'s `size="sm"` (`text-[10px] h-[18px]`) is close but not pixel-identical to the original `text-[9px]` span.

## Design documentation

- None — no `DESIGN.md` exists; the `Badge` component's own source comment remains the only documented contract.
