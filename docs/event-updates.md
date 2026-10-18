# Event updates

Organisers post updates to an event for the people who are going.

## What an update is

`EventUpdate`: a title, a category, an optional pin, and a list of parts (`Blocks`, one jsonb column replaced whole on each save).
A part is one of `Text`, `Images`, `Video`, `Link`, `Files`. An update can hold any number of parts in any order; photos and files parts hold as
many items as needed. The limits (`EventUpdateRules`) are generous and exist to stop mistakes: 50 parts, 60 items per part, 15 MB per photo,
30 MB per file, 300 MB per save. Only web links are accepted (so a link cannot run code), videos must be YouTube (stored in one standard form),
and photo and file types are checked by extension before anything is uploaded.

Categories: a standard list (`EventUpdateRules.Categories`) or the organiser's own label. A typed label that matches a standard one in
other letters becomes the standard one, so the filters do not split.

## Who sees what

- **Institution** (`/events/{id}/updates`, same scope rules as the event): list, create, edit, pin, delete.
- **Member** (`GET /events/{id}/updates`): a member with a seat (a Confirmed RSVP row, free or a paid ticket) gets the updates. Anyone else gets
  `locked: true`, a count and the categories, and **no content**: the response is built so nothing from an update can be in it.
  A ticket still being paid for, a failed payment and a cancelled RSVP are not a seat.
- A community event's updates need an approved community member, like the event itself.

## Notifications

Posting notifies each person with a seat: an in-app notice and a push, **never SMS or email** (an event can have many attendees and each
message would cost). Members who switched event reminders off are skipped. Editing does not notify again. The notice opens the event at
`#updates`. See `ProcessEventUpdatePostedAsync` in the notification workflow.

## Editing and files

New files are sent beside the update's JSON, each named with the id of the part it belongs to. On an edit, a file address is kept only if it
was already on that update, so an edit cannot smuggle in an arbitrary address. Files an edit drops are not deleted from storage.

## Deploying

Run the `AddEventUpdates` migration first.
