# Platform team structure and the Representative role

Status: the structure is built (this document, sections 1 to 5). The wider Campus Representative Programme in the brief
(KPI scorecards, incentives, prospect pipeline, weekly reporting) is designed here but **not built**; section 6 lists what is
needed from the business first.

## 1. What already existed (found by inspecting the repository)

| Need in the brief | What the platform already has |
|---|---|
| Staff accounts and roles | `PlatformStaff` with roles `SuperAdmin`, `Support`, `Billing`, `Sales`, JWT role claims, `[Authorize(Roles=…)]` on every platform endpoint |
| Prospect pipeline | `OnboardingLead` with stages `New, Contacted, DemoBooked, Trial, Approved, Rejected`, an assignee, follow-up dates and reminders, internal notes, CSV import, source attribution |
| Institution activation tracking | The activation scorecard (members, payments, team activity, setup steps), per-institution minimum members, funnel by week, stalled/overdue flags and a worker that nudges |
| Onboarding an institution | `POST /institutions` creating the tenant, first admin, optional settlement/subaccount |
| Settlement approval | Institution payout submissions become `PendingPayoutChanges` and are approved by `SuperAdmin`/`Billing` only |
| Audit log | Platform audit log written by every sensitive action |
| Notifications and reminders | Platform notifications and a scheduled follow-up reminder workflow |

Not present: ownership of an institution, a reporting structure between staff, per-person visibility limits, rep KPIs,
incentive ledger, evidence capture on meetings, blockers.

## 2. Roles and what each can do

| Capability | SuperAdmin | Representative | Support / Billing / Sales (unchanged) |
|---|---|---|---|
| See every institution, lead and person on the team | Yes, assigned or not | Only what they and the people under them own | As before |
| Onboard an institution | Yes, sets fee and settlement | Yes; owns it. Fee is the platform default; settlement details are held for approval | `Sales` as before |
| Approve settlement / payouts | Yes | **No** (never reaches Paystack until a SuperAdmin or Billing approves) | Billing yes |
| Change commercial terms (fees) | Yes | No | Billing |
| Work leads (notes, stage, follow-up) | All | Only leads assigned within their branch | `Sales` all |
| Hand institutions/leads to someone | Anyone | Only to themselves or someone beneath them | n/a |
| Change who reports to whom | Yes | No | No |
| See platform money, revenue, support | Yes | No | By role |

`Representative` is added alongside the existing roles. `Sales` keeps working exactly as before; whether to retire it in
favour of `Representative` is a decision for the business (section 6).

## 3. The structure

* A SuperAdmin reports to nobody. A Representative reports to a SuperAdmin or to another Representative, so
  representatives can have representatives (up to 8 levels).
* A loop is refused, as is reporting to a disabled account or to Billing/Support staff.
* Someone who leads a team cannot be disabled or changed to another role until their people are moved.
* **Visibility** is computed per request from the reporting tree. A representative sees themselves and everyone beneath them.
  A SuperAdmin is never narrowed.
* An institution outside someone's view is reported as *not found*, never *forbidden*, so it does not confirm it exists.

### Ownership
* `Institution.OwnerStaffId`: who is responsible for onboarding it and helping it grow. Set to whoever creates it
  (a SuperAdmin may name someone else). It never changes who administers the institution itself.
* Leads use the existing `AssigneeStaffId`.
* Existing institutions are credited to whoever created them when that was a platform staff member; the rest are unassigned
  and only SuperAdmins see them. The Team page counts them so none are forgotten.

## 4. What was built

Backend (`Platform.Api`, `PostgresDb.Sdk`)
* `PlatformStaff.ReportsToStaffId`, `Institution.OwnerStaffId` (+ migration `AddPlatformTeamStructure`, with the backfill above).
* `StaffHierarchy` (pure rules, fully unit tested), `StaffScope` (per-request visibility).
* Scoping applied to institutions (list/detail), onboarding leads (list/detail/update/notes/assignees) and the activation
  scorecard, funnel and per-institution views.
* Representative onboarding: owner = themselves, platform default fee, settlement held as `PendingPayoutChanges` with
  `PayoutStatus = Pending`, no Paystack call.
* `GET /team`, `GET /team/assignees`, `PUT /team/{id}/manager` (SuperAdmin), `POST /team/reassign`.
* Staff create/update validate role and reporting line; an older client that omits the manager leaves it unchanged.

Frontend (platform portal)
* **Team** page: the tree with each person's institutions, active institutions, members, open leads and leads won, rolled up
  for anyone who leads a branch. SuperAdmins can change a reporting line and see unowned work.
* Staff form: `Representative` role with a **Reports to** choice.
* Institution page: **Owner** with a way to hand it over; list has an Owner column.
* Representatives see a reduced navigation (Team, Institutions, Activation, Onboarding & Demos), land on Team, and do not see
  revenue or the fee and settlement controls.

Tests: hierarchy rules, visibility, restricted onboarding, hand-over, reporting-line changes and staff validation.

## 5. Deployment notes
* Run migration `AddPlatformTeamStructure` (nullable columns and indexes; the backfill is one `UPDATE`).
* Optional setting `Institutions:DefaultPlatformFeePercentage` (defaults to 5) is the fee a representative-onboarded
  institution starts on.
* Existing signed-in representatives need to sign in again only if their role changed.

## 6. The wider programme: design and decisions needed

Not built, because each needs a business decision first. Everything below reuses the structure above.

1. **KPI scorecard (monthly, 100 points).** Compute from verified events already in the system: approved communities
   (institutions created or converted from a lead, with an admin signed in), activated members (verified members who did a
   qualifying action), meaningful activity (first announcement/event/fundraiser/dues), qualified meetings (needs a new
   meeting record). Store raw values, scores, period and policy version; never rewrite a finalised period.
2. **Meetings and evidence.** New `RepMeeting` records (attendee authority, outcome, next action, evidence link, dedupe key).
   This is the only KPI input with no source in the platform today.
3. **Pipeline stages.** The existing lead stages are coarser than the brief's ten. Either extend `OnboardingLeadStatuses`
   (touches the funnel) or keep the funnel and add a `Stage` detail on the lead.
4. **Attribution rules.** First owner wins at creation; reassignment moves future credit only; a community pays one
   acquisition reward once. Needs explicit approval of the rules for shared leads and reassigned prospects.
5. **Incentive ledger.** Append-only ledger of milestone events with lifecycle
   `Pending → Verified → Approved → Scheduled → Paid / Rejected / Reversed`, idempotent per (event, rep, rule), approved by
   someone other than the earner, no automatic disbursement.
6. **Blockers and exceptions**, **weekly report**, **onboarding of reps**, **Campus Lead** and **Community Ambassador**
   (the ambassador role is a tenant-level concept that already exists as year-group admins and must stay separate).

Decisions for the business:
* Retire `Sales` into `Representative`, or keep both?
* Are the pilot targets and reward amounts in the brief the starting policy, and who may change them?
* Should an unassigned inbound lead be claimable by representatives, or always triaged by a SuperAdmin (current behaviour)?
* What counts as evidence for a meeting, and who verifies it?
* Is a representative's fee exposure (they cannot see or set fees) correct, or should they see revenue for their own institutions?
* ~~Is there an approved mechanism for paying incentives (mobile money, payroll), or is this ledger-only?~~ **Decided: settlement to representatives is entered manually.** There is no payout integration. The ledger only records what is owed and what a SuperAdmin/Billing user has marked as paid (amount, date, reference), and nothing is sent through Paystack or any other rail.

## Incentive ledger (built)

Settlement to representatives is entered by hand, so the ledger only records; it never moves money.

* Table `RepresentativeIncentiveEntries` (migration `AddRepresentativeIncentiveLedger`). Lines are `Earned` or `Paid`, with amount
  (GHS), reason, optional payment reference, optional institution, date and who recorded it. Lines are never edited or deleted:
  a mistake is voided with a reason and drops out of the totals.
* Owed = earned − paid. A payment larger than what is owed is refused; voiding an earning that payments depend on is refused.
* `GET /incentives`: a SuperAdmin sees everyone, a representative sees only their own lines (not their team's).
  `POST /incentives` and `POST /incentives/{id}/void` are SuperAdmin only. The Billing role is not involved.
* Portal: **Incentives** page for SuperAdmin (balances per rep, record earning, record payment, void) and a read-only
  version for representatives.
* Not built: automatic earnings from milestones (an institution going live, activation). Earnings are recorded by a SuperAdmin.
