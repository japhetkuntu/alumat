"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Badge, Card, Copy, Check, EmptyState, Eye, EyeOff, Input, Landmark, LoadError, Pagination, Search, SearchableSelect, cn, formatDate,
} from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";
import {
  getInstitutions, getPayoutAccounts, revealPayoutAccountNumber,
  type PayoutAccount, type PayoutAccountKind, type PayoutAccountState,
} from "@/lib/platform-api";
import { PageHeading } from "@/components/platform/page-heading";

const PAGE_SIZE = 20;
const REVEAL_SECONDS = 20;

const KINDS: { value: string; label: string }[] = [
  { value: "all", label: "Everything" },
  { value: "Institution", label: "Institutions" },
  { value: "YearGroup", label: "Year groups" },
  { value: "Group", label: "Groups" },
];
const KIND_LABEL: Record<PayoutAccountKind, string> = { Institution: "Institution", YearGroup: "Year group", Group: "Group" };

type StateFilter = "all" | PayoutAccountState;

/** What is in the address, so a refresh, a shared link and the back button all land where the person was. */
function fromAddress() {
  if (typeof window === "undefined") return { institution: "all", kind: "all", state: "Live" as StateFilter, q: "", page: 1 };
  const p = new URLSearchParams(window.location.search);
  const state = p.get("state");
  return {
    institution: p.get("institution") || "all",
    kind: KINDS.some((k) => k.value === p.get("kind")) ? (p.get("kind") as string) : "all",
    state: (["all", "Live", "SharesInstitution", "Pending", "Rejected"].includes(state ?? "") ? state : "Live") as StateFilter,
    q: p.get("q") ?? "",
    page: Math.max(1, Number(p.get("page")) || 1),
  };
}

export default function PayoutAccountsPage() {
  const { isSuperAdmin, role } = useAuth();
  const allowed = isSuperAdmin || role === "Billing";

  const initial = useMemo(fromAddress, []);
  const [institution, setInstitution] = useState(initial.institution);
  const [kind, setKind] = useState(initial.kind);
  const [state, setState] = useState<StateFilter>(initial.state);
  const [typed, setTyped] = useState(initial.q);
  const [q, setQ] = useState(initial.q);
  const [page, setPage] = useState(initial.page);

  // Searching waits for a pause in typing, so the list does not reload on every letter.
  useEffect(() => { const t = setTimeout(() => { setQ(typed.trim()); setPage(1); }, 300); return () => clearTimeout(t); }, [typed]);

  useEffect(() => {
    const url = new URL(window.location.href);
    const set = (k: string, v: string | number, empty: string | number) => (v === empty ? url.searchParams.delete(k) : url.searchParams.set(k, String(v)));
    set("institution", institution, "all"); set("kind", kind, "all"); set("state", state, "Live"); set("q", q, ""); set("page", page, 1);
    window.history.replaceState(null, "", url);
  }, [institution, kind, state, q, page]);

  const institutions = useQuery({ queryKey: ["institutions", { page: 1, pageSize: 200 }], queryFn: () => getInstitutions({ page: 1, pageSize: 200 }), enabled: allowed, staleTime: 5 * 60_000 });
  const institutionOptions = useMemo(
    () => [{ value: "all", label: "All institutions" }, ...(institutions.data?.results ?? []).map((i) => ({ value: i.id, label: i.name }))],
    [institutions.data],
  );

  const accounts = useQuery({
    queryKey: ["payout-accounts", { institution, kind, state, q, page }],
    queryFn: () => getPayoutAccounts({
      institutionId: institution === "all" ? undefined : institution, kind: kind === "all" ? undefined : kind,
      state: state === "all" ? undefined : state, search: q || undefined, page, pageSize: PAGE_SIZE,
    }),
    enabled: allowed,
    placeholderData: (previous) => previous,
  });

  const counts = accounts.data?.counts;
  const tabs: { value: StateFilter; label: string; count?: number }[] = [
    { value: "Live", label: "Live accounts", count: counts?.live },
    { value: "Pending", label: "Awaiting approval", count: counts?.pending },
    { value: "SharesInstitution", label: "Uses the institution's", count: counts?.sharesInstitution },
    { value: "Rejected", label: "Rejected", count: counts?.rejected },
    { value: "all", label: "All", count: counts ? counts.live + counts.pending + counts.sharesInstitution + counts.rejected : undefined },
  ];
  const filtered = institution !== "all" || kind !== "all" || q !== "" || state !== "Live";
  const reset = () => { setInstitution("all"); setKind("all"); setState("Live"); setTyped(""); setQ(""); setPage(1); };

  if (!allowed) {
    return (
      <div className="p-8 lg:p-12">
        <h1 className="text-2xl font-bold">Unauthorized</h1>
        <p className="mt-2 text-muted-foreground">Only platform staff who handle payments can see payout accounts.</p>
      </div>
    );
  }

  return (
    <div className="max-w-[1200px] p-5 sm:p-7">
      <PageHeading
        title="Payout accounts"
        description="Every account an institution, year group or group has given us to be paid into, as approved: the bank, the account holder, when and by whom."
      />

      {/* Who and what: the institution (typed to find), the kind of account, and a free search. */}
      <div className="mb-4 grid gap-3 md:grid-cols-[minmax(0,320px)_minmax(0,1fr)]">
        <SearchableSelect
          value={institution} onValueChange={(v) => { setInstitution(v); setPage(1); }} options={institutionOptions}
          placeholder="All institutions" searchPlaceholder="Find an institution" className="w-full"
        />
        <div className="relative">
          <Search size={15} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
          <Input aria-label="Search payout accounts" placeholder="Search by name, bank, account holder or subaccount code" className="pl-10"
            value={typed} onChange={(e) => setTyped(e.target.value)} />
        </div>
      </div>

      <div className="mb-4 flex flex-wrap gap-2" role="radiogroup" aria-label="Kind of account">
        {KINDS.map((k) => (
          <button key={k.value} type="button" role="radio" aria-checked={kind === k.value} onClick={() => { setKind(k.value); setPage(1); }}
            className={cn("min-h-10 rounded-full px-4 text-[13px] font-medium ring-1 transition",
              kind === k.value ? "bg-foreground text-background ring-foreground" : "text-muted-foreground ring-border hover:bg-muted/60 hover:text-foreground")}>
            {k.label}
          </button>
        ))}
      </div>

      <Card className="overflow-hidden">
        {/* The state of an account is the main way to slice the list, so its counts are the tabs. */}
        <div role="tablist" aria-label="State of the account" className="flex gap-1 overflow-x-auto border-b border-border px-3 pt-2 [scrollbar-width:none]">
          {tabs.map((t) => (
            <button key={t.value} type="button" role="tab" aria-selected={state === t.value} onClick={() => { setState(t.value); setPage(1); }}
              className={cn("flex min-h-11 shrink-0 items-center gap-2 border-b-2 px-3 text-[13.5px] font-medium transition",
                state === t.value ? "border-primary text-foreground" : "border-transparent text-muted-foreground hover:text-foreground")}>
              {t.label}
              {t.count !== undefined && (
                <span className={cn("rounded-full px-2 py-0.5 text-[11.5px] tabular-nums",
                  t.value === "Pending" && t.count > 0 ? "bg-amber-500/15 text-amber-700 dark:text-amber-400" : "bg-muted text-muted-foreground")}>{t.count}</span>
              )}
            </button>
          ))}
        </div>

        {accounts.isError ? (
          <LoadError className="py-12" onRetry={() => accounts.refetch()} />
        ) : accounts.isLoading ? (
          <div className="divide-y divide-border" aria-busy="true" aria-label="Loading accounts">
            {[0, 1, 2, 3].map((i) => <div key={i} className="h-[104px] animate-pulse bg-muted/40" />)}
          </div>
        ) : (accounts.data?.results.length ?? 0) === 0 ? (
          <EmptyState
            icon={<Landmark size={26} />}
            title={filtered ? "No accounts match" : "No payout accounts yet"}
            description={filtered
              ? "Try another institution, kind or state, or clear the search."
              : "Once an institution, year group or group is approved to be paid into its own account, it is kept here."}
            action={filtered ? <button type="button" onClick={reset} className="text-[13.5px] font-semibold text-primary underline underline-offset-4">Clear filters</button> : undefined}
          />
        ) : (
          <ul className={cn("divide-y divide-border transition-opacity", accounts.isFetching && "opacity-60")}>
            {accounts.data!.results.map((a) => <AccountRow key={`${a.kind}-${a.id}`} account={a} />)}
          </ul>
        )}

        {accounts.data && accounts.data.totalPages > 1 && (
          <div className="flex flex-col items-center gap-2 border-t border-border px-5 py-4">
            <Pagination page={accounts.data.page} totalPages={accounts.data.totalPages} onPageChange={setPage} />
            <p className="text-[12px] text-muted-foreground">{accounts.data.totalCount.toLocaleString()} accounts</p>
          </div>
        )}
      </Card>

      <p className="mt-3 text-[12.5px] leading-relaxed text-muted-foreground">
        Account numbers show only their last four digits. Showing a full number is logged against your name in the audit log.
      </p>
    </div>
  );
}

const STATE_BADGE: Record<PayoutAccountState, { label: string; variant: "success" | "warning" | "destructive" | "neutral" }> = {
  Live: { label: "Live", variant: "success" },
  SharesInstitution: { label: "Uses the institution's account", variant: "neutral" },
  Pending: { label: "Awaiting approval", variant: "warning" },
  Rejected: { label: "Rejected", variant: "destructive" },
};

function AccountRow({ account: a }: { account: PayoutAccount }) {
  const badge = STATE_BADGE[a.state];
  const hasBank = !!(a.bankName || a.accountName || a.accountNumberMasked);
  return (
    <li className="grid gap-4 px-5 py-4 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1.5fr)_minmax(0,1fr)] lg:gap-6">
      <div className="min-w-0">
        <p className="truncate text-[15px] font-semibold leading-tight">{a.name}</p>
        <p className="mt-1 truncate text-[13px] text-muted-foreground">
          {KIND_LABEL[a.kind]}{a.kind !== "Institution" && <> · {a.institutionName}</>}
        </p>
        <div className="mt-2 flex flex-wrap gap-1.5">
          <Badge variant={badge.variant} size="sm">{badge.label}</Badge>
          {a.changeAwaitingApproval && <Badge variant="warning" size="sm">New details awaiting approval</Badge>}
        </div>
      </div>

      <div className="min-w-0">
        {hasBank ? (
          <>
            <p className="text-[14px] font-medium leading-tight">{a.bankName ?? "Bank not recorded"}</p>
            {a.accountName && <p className="mt-0.5 text-[13px] text-muted-foreground">{a.accountName}</p>}
            {a.accountNumberMasked && <AccountNumber account={a} />}
            {a.subaccountCode && <CopyLine label="Paystack" value={a.subaccountCode} />}
            {a.showingSubmitted && <p className="mt-2 text-[12.5px] text-amber-700 dark:text-amber-400">These are the details submitted. They are not live until approved.</p>}
          </>
        ) : (
          <p className="text-[13.5px] text-muted-foreground">
            {a.state === "SharesInstitution" ? "Payments for this go into the institution's own account." : "No account details on record."}
          </p>
        )}
      </div>

      <div className="min-w-0 text-[13px]">
        {a.decidedAt ? (
          <>
            <p className="font-medium">{a.state === "Rejected" ? "Rejected" : "Approved"} {formatDate(a.decidedAt)}</p>
            {a.decidedBy && <p className="mt-0.5 text-muted-foreground">by {a.decidedBy}</p>}
          </>
        ) : a.state === "Live" || a.state === "SharesInstitution" ? (
          <p className="text-muted-foreground">Approved before we began recording when and by whom.</p>
        ) : a.state === "Pending" ? (
          <p className="text-muted-foreground">Waiting for a decision.</p>
        ) : null}
        {a.decisionNote && <p className="mt-2 rounded-md bg-destructive/10 px-2.5 py-1.5 text-[12.5px] leading-snug">{a.decisionNote}</p>}
      </div>
    </li>
  );
}

/** The masked number, and the way to see it whole: on purpose, logged, and hidden again by itself. */
function AccountNumber({ account }: { account: PayoutAccount }) {
  const [full, setFull] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [copied, setCopied] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);
  useEffect(() => () => clearTimeout(timer.current), []);

  const hide = useCallback(() => { clearTimeout(timer.current); setFull(null); setCopied(false); }, []);
  const show = async () => {
    setLoading(true);
    try {
      setFull(await revealPayoutAccountNumber(account.kind, account.id));
      timer.current = setTimeout(hide, REVEAL_SECONDS * 1000);
    } catch (e) { toast.error(handleApiError(e)); } finally { setLoading(false); }
  };
  const copy = async () => {
    if (!full) return;
    try { await navigator.clipboard.writeText(full); setCopied(true); toast.success("Account number copied"); setTimeout(() => setCopied(false), 2000); }
    catch { toast.error("Could not copy. Select the number and copy it."); }
  };

  return (
    <div className="mt-2 flex flex-wrap items-center gap-2">
      <span className="font-mono text-[14px] tabular-nums tracking-wide" aria-live="polite">{full ?? account.accountNumberMasked}</span>
      {full ? (
        <>
          <button type="button" onClick={copy} aria-label="Copy account number"
            className="inline-flex min-h-9 items-center gap-1.5 rounded-md px-2 text-[12.5px] font-medium text-primary hover:bg-primary/10">
            {copied ? <Check size={13} /> : <Copy size={13} />}{copied ? "Copied" : "Copy"}
          </button>
          <button type="button" onClick={hide} aria-label="Hide account number"
            className="inline-flex min-h-9 items-center gap-1.5 rounded-md px-2 text-[12.5px] text-muted-foreground hover:bg-muted"><EyeOff size={13} />Hide</button>
        </>
      ) : (
        <button type="button" onClick={show} disabled={loading} aria-label={`Show the full account number of ${account.name}`}
          className="inline-flex min-h-9 items-center gap-1.5 rounded-md px-2 text-[12.5px] font-medium text-primary hover:bg-primary/10 disabled:opacity-60">
          <Eye size={13} />{loading ? "Showing…" : "Show"}
        </button>
      )}
    </div>
  );
}

function CopyLine({ label, value }: { label: string; value: string }) {
  const [copied, setCopied] = useState(false);
  const copy = async () => {
    try { await navigator.clipboard.writeText(value); setCopied(true); setTimeout(() => setCopied(false), 2000); } catch { toast.error("Could not copy"); }
  };
  return (
    <button type="button" onClick={copy} title="Copy" aria-label={`Copy the ${label} code ${value}`}
      className="mt-1 inline-flex min-h-8 max-w-full items-center gap-1.5 rounded-md text-[12px] text-muted-foreground hover:text-foreground">
      <span>{label}</span>
      <span className="truncate font-mono">{value}</span>
      {copied ? <Check size={12} className="shrink-0" /> : <Copy size={12} className="shrink-0" />}
    </button>
  );
}
