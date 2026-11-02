# Website builder: plan

Status: proposed, not started. Agreed so far: block builder with a few templates on top, only SuperAdmins edit, images in any section, fast and smooth on every device for people who are not technical. **The website is one single page** that visitors scroll, with a menu that jumps to sections. It is not a set of separate pages. (This replaces the earlier idea of dynamic multi-page sites.)

## What exists today

- The member app serves one fixed landing page (`apps/member/src/app/landing-page.tsx`, 1,243 lines) to every institution. It is branded from the institution's theme (`lib/theme.ts`) and a few settable fields (hero images and headline, banner, stories, leaders, contact). News, events, businesses and communities fill in from public endpoints (`PublicController`).
- The page is chosen by the request's Host header; the bare platform domain shows the platform marketing site instead.
- Images are uploaded as-is (`UploadsController`, 5 MB, one stored file, no resizing or variants).
- Per-institution features are switched on and off with `InstitutionFeatures`.

## Principles

1. **Content is data, never code.** A site is JSON validated on the server against a fixed list of block types. No raw HTML, no scripts. Sign-in tokens live in browser storage on the same host, so one custom script would mean account takeover. Rich text is stored as a small structured format and rendered by us. Links and embeds are checked against rules (https only, video hosts on an allowlist).
2. **Fast by construction.** Pages are server-rendered from published JSON and cached. Almost every block is a server component that sends no JavaScript. Only menus, carousels and accordions hydrate.
3. **Easy for people who are not technical.** Start from a template. Click what you see to edit it. Plain words. Undo. Nothing can be broken beyond repair.
4. **The current landing page does not regress.** An institution that never touches the builder keeps the current page. The current page also becomes the first template, rebuilt from blocks.

## Data model (PostgreSQL, tenant-scoped like everything else)

- `SiteSettings` (one per institution): template id, design tokens (palette, font pairing, corner style, button style, spacing density), menu (ordered links to pages or addresses), footer, social links, default share image, site status.
- `SitePage` (exactly one per institution): `DraftJson`, `PublishedJson`, published at, SEO title, SEO description, share image, and a version counter. It is a list of sections; each section can carry a menu name and an on/off for appearing in the menu.
- `SitePageVersion`: the last N published versions of the page, so "restore this version" works.
- `MediaAsset`: the institution's image library. Original URL, generated variants (320, 640, 960, 1600 wide, WebP), width, height, dominant colour and a tiny blur placeholder, alt text, size, who uploaded it. Deleting is refused while a published page uses the image.
- Feature flag `Website` in `InstitutionFeatures`.

A block is `{ id, type, version, props, style }`. `style` is the same small set for every block: background (colour, image, overlay), vertical spacing, alignment, width (narrow, normal, full). Every block can take a background image, and image-bearing blocks take images in their props, which is how "an image in any section" works. The server migrates old block versions when it loads them, so changing a block later does not break saved pages.

## Blocks for the first release

Typed content: hero (several layouts), text, image and text, image gallery (grid or carousel), feature cards, stories and testimonials, FAQ, call to action, leaders and team, contact details, video (YouTube and Vimeo), spacer and divider, buttons row.

Live data (read at render time, only what is already public): latest news, upcoming events, communities, business directory, community numbers (members, graduating years, events held). Each takes simple settings: how many, which category, what heading.

Each block offers 2–4 layout variants, chosen from small pictures, so the choice is visual.

## Features on the website (Services, Store, Fundraisers and more)

The institution's other features are part of the default templates, switched on or off by the admin. Prototype: `docs/website-builder-prototype/index.html`.

- **One list, one switch each.** "Features on your website" lists Services, Store, Fundraisers, Jobs, Business directory and Communities. Each has a switch. The names match the per-institution feature flags that already exist (`InstitutionFeatures`).
- **Two levels of off.** If the platform has switched a feature off for the institution (`DisabledFeatures`), the admin sees it greyed out with "ask the platform team", and cannot turn it on. If the platform allows it, the admin decides whether it shows on the website.
- **What a switch controls.** Turning a feature on shows its live section on the page, and in the menu and footer if the admin has ticked it for the menu. Turning it off hides the section and its menu entry from visitors but keeps every edit, so turning it back on restores it exactly. In the editor, hidden sections are dimmed with a note saying why.
- **In the default templates.** Every template ships with Services, Store and Fundraisers sections on the page. Jobs, Business directory and Communities sections are present too, hidden until the feature is switched on. A feature the institution does not have simply does not show, so nobody sees an empty block.
- **Picker.** A section for a switched-off feature is listed but not selectable, with the reason.
- **Only what the institution chose to publish.** Each service, product, fundraiser, job and business gets a "Show on website" setting on its own management screen. Nothing appears on the public site unless it is marked. This is the rule that keeps private or draft items off the web.
- **Buying stays in the member portal.** The website shows the catalogue. Buttons such as "Get this service", "View product" and "Give now" open the member portal item, where the member signs in and pays through the existing payment flow. This keeps one checkout, one set of receipts and one place to secure. (Open choice: guest checkout for the store without an account.)
- **Data and speed.** These blocks read the same public data the member portal already uses, resolved on the server with the page and cached with it. Changing a service or product busts the cache for pages that show it.
- **Search.** Products, events and fundraisers can carry structured data so they appear properly in search results.

## Templates

Four at launch, each a set of design tokens plus a starting set of sections. The current landing page is rebuilt as the first, so the default site matches today's. Switching template changes the look only (colours, fonts, corners, light or dark) and never touches the admin's words and photos. A separate, confirmed "start over with this template's layout" replaces the sections, and can be undone.

## The page and its menu

- **One page.** The whole site is a single scrolling page of sections. There is no page list, no "add a page" and no page addresses to manage. Sections that need more room (a full news post, a product, an event) open in the member portal, which already has those screens, or in an overlay.
- **The menu is the sections.** Each section has an "In menu" tick and a short name (for example "Give" for Fundraisers). Menu order follows section order, so reordering sections reorders the menu. Clicking an item scrolls smoothly to that section, leaving room under a sticky header. The item for the section on screen is highlighted as the visitor scrolls. Home always scrolls to the top.
- **It scales with the number of items.** On a wide screen it shows as many items as fit and tucks the rest under "More", measured from the real header width, so it adapts to the logo length, the window and the device without anyone setting it up. On a phone the Menu button opens a full list with 44-pixel tap targets. Long names are shortened with an ellipsis, "More" closes with Escape or a click outside, and Home stays first.

## Footer

- Its own surface so the page visibly ends: dark (the brand colour darkened), the brand colour, or light. Text colour is always chosen for contrast.
- One brand block with the logo, a short line, **Join the community** and **Member sign in**, and the social links as buttons.
- Links grouped by purpose, built from the menu: **Explore** for ordinary sections and **Get involved** for feature sections (services, store, fundraising and so on). They scroll to the section. Switched-off features disappear from it with the rest of that feature.
- Contact details as real links (email opens the mail app, the phone number dials). Empty fields are hidden.
- A legal row with Privacy and Terms (the platform's pages for that community), Back to top, and an optional "Powered by" credit.
- Columns reflow by the width of the site itself: four across on a wide screen, brand on top with the columns below on a tablet, two across on a phone.

## Public API and caching

- `GET /public/site` returns the published page (settings, menu, footer and sections) in one call, with live-data sections resolved on the server in the same response (parallel, each with its own timeout, so one slow section cannot hold up the page).
- Cached in Redis per institution. Publishing busts that cache, so a change shows up within a second without a rebuild.
- Responses carry ETags and `stale-while-revalidate` so a CDN or the browser can serve a copy while checking in the background.

## Speed budget and how we keep it

- Target: largest content paint under 2 seconds on a mid-range phone on 4G, near-zero layout shift, and under about 60 KB of JavaScript on a typical page.
- Images: the upload is resized in the background into the variants above. The page uses `srcset` and `sizes`, lazy loading below the fold, a priority hint for the first image only, and the blur placeholder painted instantly.
- Fonts: a curated set of self-hosted font pairings, subset, with a size-matched fallback so text does not jump.
- Navigation: moving between sections is a smooth scroll within the page, so there is nothing to load. Links that leave for the member portal prefetch when they scroll into view or on hover.
- Live blocks never block the first paint: content above the fold is server-rendered, and the rest streams in. Members' data uses the existing query cache.
- A performance test (Lighthouse and a payload check) runs against every template in CI, and a template that misses the budget does not ship.

## The editor (institution portal, SuperAdmin only)

- **Website** in the sidebar: the page and its sections, templates, design, menu, media library.
- The editor is a live preview of the real site (the member app in draft mode, shown in a frame) with a thin side panel. Click a section in the preview to select it. Edits are sent to the preview with `postMessage`, so typing and dragging show instantly with no server round trip.
- Left: sections list, drag to reorder, "Add section" opens a gallery with pictures. Right: simple settings for the selected section, with the image picker (upload, drag in, pick from the library, crop with a focal point).
- Top: desktop, tablet and phone preview, undo and redo, a saved indicator, and Publish.
- **Autosave in the background** every few seconds (debounced) as a draft. Uploads happen in the background with an instant local preview. If the connection drops, edits are kept locally and sent when it returns. Two admins editing the same page are warned, not silently overwritten.
- Publish validates the page (missing alt text on images, empty required fields, broken links) and explains problems in plain words before going live. Scheduled publishing is a later addition.
- Everything an admin does is audit-logged.

## Delivery in stages (each usable on its own)

1. **Foundation.** Data model, validation, public API with caching, media library and background image variants. Tests.
2. **Renderer.** Block components and design tokens in the member app, the page route, menu and footer, the four templates, current landing page rebuilt as a template. Performance budget in CI. At this stage a site can be created from a template by data alone.
3. **Editor.** The full editing experience above, including autosave, previews and publish.
4. **Polish and hardening.** Version history and restore, redirects on rename, SEO and share images, accessibility pass, and load testing with real content.
5. **Afterwards (not now).** Custom lists, contact form to the institution's inbox, scheduled publishing, scoped custom CSS, custom domain (parked).

## Risks

- **The editor is most of the work.** Drag, live preview and mobile behaviour decide whether non-technical people can use it. Stage 3 gets the most time and real testing with a non-technical admin.
- **Ugly sites.** Free choice produces them. The design tokens are curated, text and background contrast is checked on publish, and templates set sane defaults.
- **Image cost.** Variants multiply storage. Originals are capped, variants are WebP, and unused assets can be cleaned.
- **Support load.** "Why does my page look like this" needs good previews, restore, and clear errors.
- **Search and sharing.** The one page needs a proper title, description and share image, with sensible defaults. With a single page, headings and structured data per section carry the search value, so the editor prompts for clear section headings.

## Open choices

1. Image processing library on the server (ImageSharp is the usual choice for .NET; it has a commercial licence above a revenue threshold, so we should check it fits).
2. Whether the site's public home replaces the portal's current landing for every institution at once, or only for those who switch it on (recommended: only those who switch on, then default for new ones).
