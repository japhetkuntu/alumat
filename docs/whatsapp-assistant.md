# AlumUnion WhatsApp assistant: how it works, how to switch it on, how to switch it off

Built on the "Phase 3" of `docs/whatsapp-bridge-assessment.md`. No WhatsApp tables were added: a member's own `Phone` is the
identity, conversation state lives in Redis for half an hour, and connected groups are a small list on the institution.

## What it does

- **Assistant (one shared WhatsApp number).** A member messages it and gets a numbered menu: dues and fundraisers, events, jobs,
  community updates, their community, switch community, help. Someone who is not connected sees only what an anonymous visitor to
  the portal could see. A connected member sees what the portal itself would show them, because the assistant calls the same services.
- **Connecting a member.** Done from the portal (Profile, then Connect WhatsApp). The portal creates a one-time code for the signed-in
  member; the member sends it to the assistant **from the WhatsApp account they want to connect**. Both halves are needed, so a link,
  a screenshot or a forwarded code connects nothing. The number WhatsApp reports is saved on the member (`PhoneVerifiedAt`).
  Someone with the same verified email in other communities is connected there too.
- **Notifications.** For members who connected *and* switched on WhatsApp alerts, notifications go over WhatsApp first and SMS only if
  WhatsApp could not deliver. This is the SMS saving. Everyone else is unchanged.
- **Groups.** An administrator connects a group (portal: Settings, WhatsApp): create a code, add the assistant to the group, post
  `!link CODE`, then confirm in the portal. A connected group answers `!help !events !campaigns !jobs !updates !community` with
  public information only. Administrators can post an event, job, news article or fundraiser to a group after seeing the message.

- **Doing things (connected members only).** `rsvp 1` books a place at an event they were just shown, `cancel rsvp 1` cancels,
  `suggest` walks through sending a job or opportunity for administrator review, and `post update` posts to their year group's wall
  after showing them the text. Every action goes through the same service the portal uses, as that member, and nothing is sent
  until they answer *yes*. `stop` switches WhatsApp alerts off without disconnecting; `alerts on` turns them back on.
- **Other message kinds.** Voice notes, pictures, locations and files get a plain "I can't use that, here is what works" reply.
  Nothing from them is fetched or kept.
- **Finding it.** The portal's get-started checklist offers "Get your alerts on WhatsApp"; the new-member welcome and the sign-up
  confirmation mention it where the assistant is on; an SMS carries a short invitation only when it still fits one SMS.
- **Joining a group.** An administrator can paste a group's own invite link (`https://chat.whatsapp.com/...`, nothing else is
  accepted); members of that community then see a "Join group" button on the Connect WhatsApp page and on the community page.

## WhatsApp before email

For a member who connected WhatsApp **and** switched WhatsApp alerts on, these go to WhatsApp and the email is not sent. Anything that
stops WhatsApp (not connected, no consent, channel off, quiet hours, the daily limit, a failed send) leaves the email to go as before.

| Message | Kind | Rules |
|---|---|---|
| Receipt, rejection, approval, RSVP changes, mentorship decision, store and service updates, spotlight decision | Receipt/decision | Always sent, any hour; SMS fallback only for the original receipts |
| Dues reminder, pledge reminder, event reminder (day before) | Reminder | Quiet hours and daily limit apply; falls back to email |
| New job, new fundraiser, upcoming-event alert, weekly/monthly digest, "we miss you", mentorship request, forum reply, referral joined | News | Same limits; WhatsApp only, never SMS |

- **Quiet hours:** `WhatsAppAssistant__QuietFromHourUtc` to `QuietToHourUtc` (default 21 to 6 UTC, which is Ghana time). Only receipts
  and decisions go in that window.
- **Daily limit:** `WhatsAppAssistant__DailyLimit` (default 3) reminder/news messages per member per day; the rest go by email.
- **Pace:** `WhatsAppAssistant__PaceMilliseconds` (default 1200) between sends from the worker, so a long list goes at a human pace.
- Set these on **operations-worker**. Counts of what went by WhatsApp or SMS feed the adoption report.
- **Not yet:** administrators are not messaged on WhatsApp (their number is not proved the way a member's is), and broadcasts
  still use the channels the sender picks.

## Shortcuts in the assistant

`pay` (also `dues`, `give`, `donate`) opens dues and fundraisers; `invite` gives a message to forward with the member's own
referral link, tagged `src=whatsapp` so the institution's referral report counts it.

## Reporting

- **Institution administrators** (Settings, WhatsApp): members connected out of active members, how many have alerts on, connections
  this month, and alerts delivered by WhatsApp versus SMS for the last six months, plus invitations by where they were shared.
  Counts only; no figures in money.
- **Platform** (WhatsApp page): the same by month for the whole platform and by institution. Set `WhatsAppAssistant__SmsUnitCost`
  (what one SMS costs, any currency) to see an *estimated* saving; without it only counts are shown.
- Counts come from a small month-keyed tally on each institution (`Institutions.MessagingUsage`, migration
  `AddInstitutionMessagingUsage`): numbers only, no recipient and no text. Only alerts sent from this release on are counted.

## What it never does

- Read or keep ordinary group conversation: only messages starting with `!` are looked at, and only in connected groups.
- Show anything personal in a group, or to someone whose number is not connected.
- Send to a number nobody proved, or to anyone who did not switch WhatsApp alerts on.
- Post anything by itself: every group post is previewed and confirmed by an administrator.
- Ask for a password or payment details.

## Switching it on (in this order)

1. In WaSenderAPI: create a session for the assistant's phone, scan the QR code, note the API key.
2. Set, on **member-api**, **operations-worker** (and the read-only ones on institution-api and platform-api; see
   `deploy/*.env.example`):
   - `WaSenderConfig__ApiKey`
   - `WaSenderConfig__WebhookSecret` (a long random string; the same value goes in the WaSender dashboard)
   - `WaSenderConfig__AssistantNumber` (digits with country code)
   - `WaSenderConfig__Enabled=true`
3. In the WaSender dashboard set the webhook URL to `https://<any member portal host>/api/v1/callbacks/whatsapp`, the secret as
   above, and subscribe to personal-message and group-message received events plus session status.
4. Apply the database migrations (they run on start): `AddMemberPhoneIdentity`, `AddInstitutionWhatsAppGroups`, `AddInstitutionMessagingUsage`.
5. Open the platform portal, WhatsApp: every setup row should be "yes" and the session "Connected".
6. Pilot with one institution first: connect your own number, send `hi`.

## Switching it off

- **From the platform portal, instantly (recommended):** WhatsApp page, "WhatsApp bot for every institution". Takes effect within
  seconds on every API and the worker, with no restart and no deploy. The assistant and group commands go quiet, group posting is
  refused, and every notification goes by SMS and email exactly as before the bot. Members stay connected; switch it back on at any
  time. Only a super administrator can change it, and the change is written to the platform audit log.

- Whole platform: `WaSenderConfig__Enabled=false` on member-api and operations-worker and restart. Replies stop, notifications go
  back to SMS exactly as before. Nothing else about AlumUnion changes.
- One institution: Settings, WhatsApp, switch off (or the WhatsApp feature in the platform portal).
- One member: Profile, Connect WhatsApp, Disconnect (or send `unlink` to the assistant).

## Things to know

- **WaSender is an unofficial, session-based connection** to a normal WhatsApp account. WhatsApp can restrict such accounts, so
  messages go only to members who connected and consented, one at a time at a human pace. The provider is behind an interface
  (`IWhatsAppProvider`) so the official WhatsApp Cloud API can replace it.
- The webhook is authenticated by a shared secret in a header (that is all WaSender offers, it is not a signature). Keep the secret
  long and private; an nginx IP allow-list for WaSender's addresses is a sensible extra.
- Incoming messages are answered while the request is handled; if the API restarts mid-message the person simply sends it again.
  Replies are queued durably and are not doubled by a redelivered webhook.
- Rate limits and event identifiers beyond the ones in WaSender's published reference were not available: confirm them against a real
  delivery during the pilot.
- Not built yet: submitting events through WhatsApp (jobs and updates are), reading voice notes, and per-link click analytics.
- "Delivered" means the provider accepted the message; WaSender delivery receipts are not read yet.
