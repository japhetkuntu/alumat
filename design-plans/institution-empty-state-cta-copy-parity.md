# Match empty-state CTA copy to the page-header CTA for the same action

Written against: 321479c

## Evidence chain

- Surface: `frontend/apps/institution/src/app/(dashboard)/news/page.tsx`, `frontend/apps/institution/src/app/(dashboard)/events/page.tsx`, `frontend/apps/institution/src/app/(dashboard)/resources/page.tsx`
- Problem: each of these pages renders a page-header button for "create the first record" and, when the list is empty, also renders an `EmptyState` with its own action button for the identical action — but the two buttons carry different copy.
- Design evidence (direct contradiction within the same rendered view):
  - `news/page.tsx:229` header button: `Create article`  vs. `news/page.tsx:314` empty-state button: `New Post`
  - `events/page.tsx:256` header button: `Create event`  vs. `events/page.tsx:274` empty-state button: `New Event`
  - `resources/page.tsx:176` header button: `Add resource`  vs. `resources/page.tsx:254` empty-state button: `Add Resource` (capitalization mismatch only)
  - Both buttons in each pair call the same handler (`setShowCreate(true)`/`setShowCreate(!showCreate)`) and open the same create form, confirming they represent one action, not two.
- Owner: no shared copy constant exists — each button's label is a literal string inlined at its call site.
- Scope and affected surfaces: `news/page.tsx`, `events/page.tsx`, `resources/page.tsx`. Other list pages with `EmptyState` (e.g. `campaigns/page.tsx:359` "Create campaign" matching its header `campaigns/page.tsx:292` "Create campaign") already agree and are out of scope.
- Uncertainty: none — both buttons are visible in the same empty-list render and both trigger the same create flow.

## Design decision

Make each `EmptyState` action button reuse the exact label already used by that page's header create button, so a user does not see two different names for the same action on screen at once. Keep whichever exact string the header already uses as the source of truth per page (do not invent a new global label).

## Reuse

- Exemplar of already-consistent pair: `campaigns/page.tsx:292` and `campaigns/page.tsx:359`, both "Create campaign".
- No new component or token needed — this is a literal string edit at three call sites.

## Changes

1. `frontend/apps/institution/src/app/(dashboard)/news/page.tsx`
   - Change: line 314, `EmptyState`'s `action` button label from `New Post` to `Create article` (matching header at line 229). Keep the leading `<Plus size={14} />` icon.
   - Preserve: the `title`/`description` text of the `EmptyState`, the `onClick={() => setShowCreate(true)}` handler, and the `icon` prop.
   - Verify: with zero news posts, the empty-state button reads "Create article", identical to the header button.

2. `frontend/apps/institution/src/app/(dashboard)/events/page.tsx`
   - Change: line 274, `EmptyState`'s `action` button label from `New Event` to `Create event` (matching header at line 256).
   - Preserve: `title`/`description`/`icon` props and the `onClick={() => setShowCreate(true)}` handler.
   - Verify: with zero events, the empty-state button reads "Create event", identical to the header button.

3. `frontend/apps/institution/src/app/(dashboard)/resources/page.tsx`
   - Change: line 254, `EmptyState`'s `action` button label from `Add Resource` to `Add resource` (matching header at line 176's exact casing).
   - Preserve: `title`/`description`/`icon` props and the `onClick={() => setShowCreate(true)}` handler.
   - Verify: with zero resources, the empty-state button reads "Add resource", identical to the header button.

## Scope

- Inherit: the three files above only.
- Verify: re-check every other route using `EmptyState` (`campaigns/page.tsx`, `jobs/page.tsx`, `spotlights/page.tsx`, `forum/page.tsx`, `mentorship/page.tsx`, `members/page.tsx`, `notifications/page.tsx`) to confirm their empty-state and header labels already agree — no change expected there, but worth a quick grep after editing to make sure no other pair was missed.
- Exclude: `EmptyState` component implementation itself (`src/components/shared/empty-state.tsx` or wherever it lives) — this is a call-site copy fix, not a component change.

## Validation

- Product: temporarily view News, Events, and Resources with an empty dataset (or trust the source-level match) and confirm one consistent label appears in both places.
- Interface: default desktop viewport is sufficient — this is a text-only change with no layout impact.
- System: confirm no other page's header/empty-state pair was accidentally introduced or broken by this change (the grep in the Evidence chain already confirms the other pages are clean).
- Repository: `cd frontend/apps/institution && npx tsc --noEmit && npx eslint src/app/\(dashboard\)/news/page.tsx src/app/\(dashboard\)/events/page.tsx src/app/\(dashboard\)/resources/page.tsx` → no errors.

## Stop conditions

- Stop if a page's header button and empty-state button turn out to trigger genuinely different actions (e.g. one opens a form, the other opens an import flow) — in that case the differing copy would be correct and this finding would not apply to that page.

## Design documentation

- None — no `DESIGN.md` exists; this fix does not need to be recorded anywhere beyond the diff itself.
