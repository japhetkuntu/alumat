# WhatsApp Community Bridge: current-state assessment and Phase 1 plan

Enhancement to the existing product. Nothing here replaces, renames or restructures an existing feature. Rule throughout: **reuse, then extend, then add**.

## A. What already exists

| Need | Already in the product | Gap |
|---|---|---|
| Share content | `ShareLinkButton` (`packages/ui/src/components/share-link-button.tsx`): Web Share API with copy fallback. Used on events, news, jobs, albums, services, resources, business directory, communities, campaigns. | Shares title and URL only. No contextual message, no explicit "WhatsApp" choice, no attribution. |
| WhatsApp share buttons | Member `referrals/page.tsx`, member `payment-campaign/.../campaign-detail-client.tsx`, institution `invite-kit-card.tsx` (copy, `wa.me`, QR). | Three separate hand-written implementations. The institution invite kit link has no `?ref=`. |
| Link previews | `lib/entity-og.ts` + `GET /public/preview/{type}/{id}` give real OG tags for event, job, news, resource, business, community, album, service, campaign. Runs server-side before the login gate. | Preview works; the page behind it does not (see below). |
| Deep links | Every module has an `[id]` route under `(portal)`. | The detail routes under `(portal)` are login-gated: a guest tapping a WhatsApp link sees the login page, not the content. Only fundraisers (`/payment-campaign`, `/fundraiser`) are viewable without login. |
| Auth continuation | Gate redirects to `/login?redirect=<path+query>`; login validates it (`isSafeRedirectPath`) and returns the member to it, including Google. | The redirect is **dropped on the way to `/register`** (login's "Create account" link and register's "Sign in" link carry nothing), and after registration the member lands on a bare `/login`. A new member from a WhatsApp link loses the destination. |
| Join and invite | `?ref=CODE` on `/register` (Referral entity, points, `GET /public/referral-preview`), invite by email only. | No group-aware invitation. Invitations are person-to-person by email. |
| Groups / chapters | `Community` (name, description, cover, active) and `CommunityMembership` (request to join, `Role` = Member or Leader, scoped moderation). `Batch` models year groups. Campaigns, events, jobs, news, resources, forum already take an optional `CommunityId`. | No invite link, no leader growth view, no external-channel field. There is **no separate "Ambassador" entity**, so Community Leader is the right place. |
| Import | `POST members/import`, CSV with email required; welcome email carries a reset-token link. | Email mandatory, so phone-only WhatsApp groups cannot be imported. |
| Sign-in | Email and password, Google (existing members). Phone required at registration but never verified. | No phone/OTP sign-in. |
| Notifications | Temporal `NotificationDispatchWorkflow`: in-app, email, SMS, push; WhatsApp (WaSender) wired but `WhatsAppEnabled = false`; broadcasts never use WhatsApp. | Fine for Phase 2. Not needed for Phase 1. |
| Analytics | Marketing share attribution exists on the platform side; member `?ref=` attribution for fundraisers (`sharedByMemberId`). | No member-side "opened from WhatsApp" signal. |
| Tenant isolation | `ITenantScoped` query filters and host-based tenant resolution; `IAlumniPgRepository<T>` only. | Reuse as is. |

## B. Feature mapping (what small change connects each one)

| Feature | Already there | Small enhancement | Backend change? |
|---|---|---|---|
| Events, Jobs, News, Albums, Business, Services, Resources | Detail page, OG preview, share button | Shared message builder with contextual text; guest-readable view (decision 1) | Only for guest view |
| Fundraising | Public page, guest payment, `?ref=`, published thank-you page | Contextual message with raised and goal; use the same builder | No |
| Membership dues | Dues page | Message builder for admin to paste into groups; reminder deep link via existing in-app/SMS | No |
| Spotlight, Store | Pages exist | Add the share button to the shared builder if missing | No |
| Mentorship | Page, mentor WhatsApp contact | Message builder | No |
| Directory | Registration creates the profile | Carry group context from the invite into registration | Yes, with invitations |
| Groups / chapters | `Community` | Optional external channel link; invite link | Yes, small |
| Community Leaders | `CommunityMembership.Role = Leader` | Invite and growth card for the leader's own group | Yes, with invitations |
| Notifications | Dispatch workflow | Phase 2 only | Phase 2 |

## C. Proposed domain additions (minimum)

1. **Reuse, no change:** Member, Event, Job, Campaign, Business, Album, Service, Referral.
2. **Extend `Community`:** `ExternalChannels` as a small JSONB list (`type`, `displayName`, `inviteUrl`, `connectedAt`), the pattern already used for `Campaign.PublicPage`. Reason: a Community is the existing group identity; a WhatsApp group is only a communication channel for it. Channel-generic (`type` = "WhatsApp" today) without a new table. Tenant owner: the Community's institution. Edit permission: institution admins and that Community's Leaders.
3. **Extend `Referral`:** optional `CommunityId` and `Channel` (and a short `InviteCode` that is not a member id). Reason: attribution of a join to a group, leader and channel. A "group invite" is a Referral whose referrer is the leader, so points, preview and registration plumbing are reused.
4. **No new tables** for Phase 1. No `WhatsApp*` entity of any kind.

## D. Architecture

```
WhatsApp group  ->  shared link / invite link  ->  existing member web app (mobile)
                                                    -> existing APIs and services
Phase 2:  channel delivery in the existing NotificationDispatchWorkflow (WhatsApp is just another channel)
```

All business logic stays in the existing services. The only WhatsApp-specific code is a text builder and a `wa.me` URL helper in the shared UI package.

## E. Journeys and the friction to remove

1. **Existing member opens an event from WhatsApp:** already works (login, then back to the event). Gap: the preview is good but the page needs login every time. Fix: carry the destination reliably and show a lighter guest view (decision 1).
2. **New member joins from a group invite:** gap: no group context, destination lost through register. Fix: invite link with group context, redirect kept through register and verification.
3. **Leader invites from their own group:** gap: no leader tooling. Fix: invite card on the Community page for leaders (copy, WhatsApp, QR, count of joined).
4. **Admin shares a fundraiser:** works; use the shared message with raised and goal.
5. **Member gives from the link:** works (guest payment, `?ref=` attribution).
6 to 12. Job, spotlight, business, album, dues, product, service: same share builder, same deep link.
13. **Guest opens a deep link, signs in, returns:** gap is only the create-account branch. Fix in Phase 1.
14. **Invitation carries group context:** Phase 1, via Referral extension.

## F. Security and privacy review (before building)

- **Return URL:** keep `isSafeRedirectPath`; reuse it for register and verification so no open redirect is introduced.
- **Guest views:** show only what the existing `/public/preview` already exposes (title, description, image) plus a sign-in prompt; no member data, no community-restricted content (`CommunityId != null` stays gated).
- **Invite codes:** random, unguessable, revocable, optional expiry; never a member or community id in the URL. Server resolves tenant from the host, never from the link.
- **Leader scope:** a Leader can only see and invite for their own Community, checked live as today.
- **WhatsApp URLs:** stored invite URLs are validated (`https://chat.whatsapp.com/...`), shown only to approved members of that Community.
- **No scraping:** we never read group members, numbers or messages.
- **Consent:** no outbound WhatsApp in Phase 1, so no new consent surface.

## G. Phased plan

**Phase 1 (deployable slices, each backward compatible)**
1. **Destination continuity** through login, register and verification. Frontend only. *Built.*
2. **Shared message builder** in `packages/ui` (`shareMessages`) used by `ShareLinkButton`: contextual text per content type. *Built for events, jobs, news, albums, services, businesses, communities, fundraisers and dues, member and admin side.* An explicit WhatsApp button and attribution parameter are still to do. Replace the three hand-written variants; add it to events, jobs, news, albums, business, services, spotlights, store, dues.
3. **Admin broadcast and invite kit (not built):** copy-ready WhatsApp text for announcements; add `?ref` to the invite kit link.
4. **Guest-readable event, job and news pages** (decision 1: read-only summary with a sign-in call to action). *Built.*
5. **Community external channel + leader invite.** *Built:* `Community.ExternalChannels`, `Referral.CommunityId/Channel`, invite-aware registration (approved only when the sharer is a live Leader), leader panel, admin channel field and growth card. `Community.ExternalChannels`, group invite via extended `Referral`, join with context, leader growth card.
6. **Phone-only import and invite** (decision 2).
7. **Basic growth analytics.** *Built for invitations* (joins by source and community); share-to-view counts are not tracked. registrations by source, group and leader; share to view counts.

**Phase 2:** official WhatsApp delivery as a channel in the existing notification workflow, templates, consent and preferences.
**Phase 3:** WhatsApp assistant over existing APIs.

## H. Decisions

1. Guest view: **read-only summary with a sign-in call to action** (decided).
2. Phone-only members: **email-first for now** (decided). Revisit after Phase 1 shows how many group members lack email.

### Original questions

1. **Guest-readable pages.** Today a WhatsApp link to an event or job lands on a login screen. Do you want a read-only guest view (title, date, place, description) with "Sign in to RSVP", or keep everything members-only?
2. **Phone-only members.** Many people in WhatsApp groups may not have email. Should members be able to join and be imported with phone only (needs phone verification by SMS code), or stay email-first for now?
