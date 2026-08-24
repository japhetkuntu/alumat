# Align Campaign detail back-navigation to the shared detail-page breadcrumb pattern

Written against: 321479c

## Evidence chain

- Surface: `frontend/apps/institution/src/app/(dashboard)/campaigns/[id]/page.tsx`
- Problem: this is the only one of four peer record-detail pages that doesn't use the shared breadcrumb component pattern for returning to its list page.
- Design evidence:
  - `members/[id]/page.tsx:110-117` — `<Link href="/members"><Button size="sm" variant="ghost" className="h-8 px-2 text-muted-foreground hover:text-foreground font-medium">Members</Button></Link>` + `<ChevronRight size={14} .../>` + current record name.
  - `jobs/[id]/page.tsx:166-178` — `<Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg hover:bg-black/5 dark:hover:bg-white/5 font-bold group" onClick={() => router.push("/jobs")}><ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />Jobs</Button>` + `<ChevronRight .../>` + current record name.
  - `news/[id]/page.tsx:80-88` and `resources/[id]/page.tsx:80-88` — identical shape: `<Link href="/news"|"/resources"><Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg font-bold group"><ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />News|Resources</Button></Link>` + `<ChevronRight .../>` + current record name.
  - `campaigns/[id]/page.tsx:118-120` instead renders: `<Link href={backLink} className="inline-block text-[13px] font-bold hover:underline" style={{ color: "#006DB8" }}>&larr; {campaign.isMembershipCampaign ? "Membership" : "Campaigns"}</Link>` — no `Button`, no `ArrowLeft` icon component (uses the HTML entity `&larr;` instead), no `ChevronRight`, no current-record-name segment, and a hardcoded inline hex color instead of a design token.
- Owner: no shared `Breadcrumb` component exists yet — each detail page inlines the same three-part markup independently, but three of four already agree on its exact shape.
- Scope and affected surfaces: `campaigns/[id]/page.tsx` only.
- Uncertainty: `campaigns/[id]/page.tsx` has a dynamic `backLink` (`/membership` when `campaign.isMembershipCampaign`, else `/campaigns`) with a corresponding dynamic label ("Membership" vs "Campaigns") — this dynamic behavior must be preserved, not flattened to a single hardcoded destination.

## Design decision

Rebuild the Campaign detail page's back-navigation using the same `Button variant="ghost" size="sm"` + `ArrowLeft` (with the `group-hover:-translate-x-0.5` micro-interaction) + `ChevronRight` + current-record-name breadcrumb structure used by Members, Jobs, News, and Resources detail pages — while preserving the existing dynamic `backLink`/label logic for membership campaigns.

## Reuse

- Exemplar: `frontend/apps/institution/src/app/(dashboard)/news/[id]/page.tsx:80-88` (closest match: wraps `Button` in a `Link`, same icon/hover treatment, same `ChevronRight` + truncated current-title segment).
- Icon: `ArrowLeft` from `lucide-react` (already imported in `campaigns/[id]/page.tsx:6`).
- Component: `Button` from `@/components/ui/button` (already imported), `ChevronRight` from `lucide-react` (not yet imported — add it).

No new primitive required.

## Changes

1. `frontend/apps/institution/src/app/(dashboard)/campaigns/[id]/page.tsx`
   - Change: add `ChevronRight` to the existing `lucide-react` import at line 6 (`import { ArrowLeft, XCircle, X, Expand, Pencil, ChevronRight } from "lucide-react";`). Replace lines 118-120:
     ```tsx
     <Link href={backLink} className="inline-block text-[13px] font-bold hover:underline" style={{ color: "#006DB8" }}>
       &larr; {campaign.isMembershipCampaign ? "Membership" : "Campaigns"}
     </Link>
     ```
     with:
     ```tsx
     <nav className="flex items-center gap-1.5 text-sm">
       <Link href={backLink}>
         <Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg font-bold group">
           <ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />
           {campaign.isMembershipCampaign ? "Membership" : "Campaigns"}
         </Button>
       </Link>
       <ChevronRight size={14} className="text-muted-foreground/50" />
       <span className="text-[13px] font-semibold text-foreground/70 truncate max-w-[200px] sm:max-w-xs">{campaign.title}</span>
     </nav>
     ```
   - Preserve: the existing `backLink` variable and its `isMembershipCampaign` conditional exactly as computed above this block; the surrounding `<div className="flex items-start justify-between gap-3">` title/edit-button row directly below stays untouched.
   - Verify: the back-navigation reads and behaves identically (still routes to `/membership` for membership campaigns, `/campaigns` otherwise) but now visually matches the breadcrumb shown on Members/Jobs/News/Resources detail pages, including the current campaign's title as the trailing breadcrumb segment.

## Scope

- Inherit: `campaigns/[id]/page.tsx` only.
- Verify: confirm the `#006DB8` hardcoded color and the `&larr;` entity have no other usage in this file worth flagging separately (out of scope if found elsewhere — note only).
- Exclude: `members/[id]/page.tsx`, `jobs/[id]/page.tsx`, `news/[id]/page.tsx`, `resources/[id]/page.tsx` — already correct, not touched. `events/[id]/rsvps/page.tsx` uses a simpler "Back to Events" button without a breadcrumb trail, but that page is a sub-view of a record (an RSVP list), not a peer record-detail page, so it is excluded from this finding.

## Validation

- Product: open a regular campaign detail page and a membership-campaign detail page; confirm the back button routes correctly in both cases and the breadcrumb label switches between "Campaigns" and "Membership" as before.
- Interface: check at narrow widths that the new `truncate max-w-[200px] sm:max-w-xs` on the campaign title segment doesn't clip awkwardly for very long campaign titles — this matches the same truncation already accepted on the other three detail pages.
- System: confirm no second back-navigation pattern remains in this file, and that no other page copied the old inline-color pattern.
- Repository: `cd frontend/apps/institution && npx tsc --noEmit && npx eslint "src/app/(dashboard)/campaigns/[id]/page.tsx"` → no errors.

## Stop conditions

- Stop if `campaign.title` is ever empty/undefined at render time in a way the other detail pages don't handle — in that case match whatever guard those pages use rather than inventing new fallback text.

## Design documentation

- None — no `DESIGN.md` exists; convention remains implicit in the repeated route markup.
