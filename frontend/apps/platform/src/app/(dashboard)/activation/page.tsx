"use client";

import { Suspense, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { TargetsTab } from "@/components/platform/work/targets-tab";
import { MyTasksTab } from "@/components/platform/work/my-tasks-tab";
import { TaskDialog, type TaskPrefill } from "@/components/platform/work/task-dialog";
import {
  Badge,
  Button,
  Card,
  CardContent,
  ChipRow,
  SegmentedControl,
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  Input,
  Label,
  LoadError,
  Progress,
  Skeleton,
  Table,
  TableBody,
  TableCell,
  TableEmpty,
  TableHead,
  TableHeader,
  TableRow,
  Check,
  Plus,
  Trash2,
  X,
  formatDate,
} from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";
import { activationStatus, daysUntil, scorecardToCsv, trialLabel } from "@/lib/activation";
import { downloadCsv } from "@/lib/csv";
import {
  getActivationFunnel,
  getActivationScorecard,
  updateActivationTarget,
  type ActivationScorecardItem,
  type MilestoneProgress,
} from "@/lib/platform-api";

type Filter = "all" | "overdue" | "stalled" | "progress" | "activated";

const FILTERS: { value: Filter; label: string }[] = [
  { value: "all", label: "All" },
  { value: "overdue", label: "Overdue" },
  { value: "stalled", label: "Stalled" },
  { value: "progress", label: "In progress" },
  { value: "activated", label: "Activated" },
];

const CRITERIA_COLUMNS = [
  { key: "branding", label: "Branding" },
  { key: "payouts", label: "Payouts" },
  { key: "members", label: "Members" },
  { key: "payments", label: "Payments" },
  { key: "staff", label: "Staff weekly" },
] as const;

interface MilestoneDraft {
  date: string;
  liveTarget: string;
  activatedTarget: string;
}

function matchesFilter(item: ActivationScorecardItem, filter: Filter) {
  const status = activationStatus(item);
  if (filter === "activated") return status === "Activated";
  if (filter === "overdue") return status === "Overdue";
  if (filter === "stalled") return status === "Stalled";
  if (filter === "progress") return status === "In progress";
  return true;
}

function StatusBadge({ item }: { item: ActivationScorecardItem }) {
  const status = activationStatus(item);
  const variant = status === "Activated" ? "success" : status === "Overdue" ? "destructive" : status === "Stalled" ? "warning" : "neutral";
  return <Badge variant={variant}>{status}</Badge>;
}

function MilestoneRow({ m }: { m: MilestoneProgress }) {
  const parts = [
    m.liveTarget != null && `${m.liveActual} of ${m.liveTarget} live`,
    m.activatedTarget != null && `${m.activatedActual} of ${m.activatedTarget} activated`,
  ].filter(Boolean);
  const variant = m.status === "met" ? "success" : m.status === "missed" ? "destructive" : "neutral";
  const label = m.status === "met" ? "Met" : m.status === "missed" ? "Missed" : `${daysUntil(m.date)} days left`;
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 px-5 py-3 border-b border-border last:border-0">
      <div className="min-w-0">
        <div className="text-[13px] font-semibold">{formatDate(m.date)}</div>
        <div className="text-[12.5px] text-muted-foreground tabular-nums">{parts.join(" · ")}</div>
      </div>
      <Badge variant={variant}>{label}</Badge>
    </div>
  );
}

function ScorecardTab() {
  const [taskFor, setTaskFor] = useState<TaskPrefill | null>(null);
  const { isSuperAdmin } = useAuth();
  const queryClient = useQueryClient();
  const [filter, setFilter] = useState<Filter>("all");
  const [targetOpen, setTargetOpen] = useState(false);
  const [targetCount, setTargetCount] = useState("");
  const [targetDate, setTargetDate] = useState("");
  const [milestones, setMilestones] = useState<MilestoneDraft[]>([]);

  const scorecard = useQuery({ queryKey: ["activation-scorecard"], queryFn: getActivationScorecard });
  const funnel = useQuery({ queryKey: ["activation-funnel"], queryFn: () => getActivationFunnel(9) });

  const targetMutation = useMutation({
    mutationFn: updateActivationTarget,
    onSuccess: (data) => {
      queryClient.setQueryData(["activation-scorecard"], data);
      toast.success("Target updated");
      setTargetOpen(false);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function openTargetDialog() {
    setTargetCount(scorecard.data?.targetCount?.toString() ?? "");
    setTargetDate(scorecard.data?.targetDate?.slice(0, 10) ?? "");
    setMilestones(
      (scorecard.data?.milestones ?? []).map((m) => ({
        date: m.date.slice(0, 10),
        liveTarget: m.liveTarget?.toString() ?? "",
        activatedTarget: m.activatedTarget?.toString() ?? "",
      })),
    );
    setTargetOpen(true);
  }

  const milestoneDraftsValid = milestones.every((m) => m.date && (m.liveTarget || m.activatedTarget));

  function saveTarget() {
    targetMutation.mutate({
      targetCount: Number(targetCount),
      targetDate: targetDate || null,
      milestones: milestones.map((m) => ({
        date: m.date,
        liveTarget: m.liveTarget ? Number(m.liveTarget) : null,
        activatedTarget: m.activatedTarget ? Number(m.activatedTarget) : null,
      })),
    });
  }

  const data = scorecard.data;
  const items = (data?.items ?? []).filter((i) => matchesFilter(i, filter));
  const stages = funnel.data?.stages ?? [];
  const weeks = [...(funnel.data?.weeks ?? [])].reverse();

  return (
    <div className="p-4 sm:p-7 max-w-[1500px]">
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-3 mb-6">
        <div>
          <h1 className="text-[24px] font-bold">Activation</h1>
          <p className="text-muted-foreground text-[13px] mt-1">
            Which institutions are actually using the platform, and how the onboarding pipeline is moving.
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            disabled={!data?.items.length}
            onClick={() => downloadCsv(`activation-${new Date().toISOString().slice(0, 10)}.csv`, scorecardToCsv(data?.items ?? []))}
          >
            Export CSV
          </Button>
          {isSuperAdmin && (
            <Button variant="outline" onClick={openTargetDialog}>
              {data?.targetCount ? "Edit target" : "Set a target"}
            </Button>
          )}
        </div>
      </div>

      {/* Goal line and milestones — only when a target is set; progress counts activated institutions, not sign-ups. */}
      {data?.targetCount ? (
        <div className={`grid grid-cols-1 ${data.milestones.length ? "lg:grid-cols-[1.4fr_1fr]" : ""} gap-4 mb-4`}>
          <Card>
            <CardContent className="p-5">
              <div className="flex flex-wrap items-baseline justify-between gap-2 mb-3">
                <p className="text-[15px] font-semibold">
                  <span className="tabular-nums text-[22px] font-bold mr-1.5">{data.activatedCount}</span>
                  of {data.targetCount} institutions activated
                </p>
                {data.targetDate && (
                  <p className="text-[12.5px] text-muted-foreground">
                    Target {formatDate(data.targetDate)}
                    {daysUntil(data.targetDate) >= 0
                      ? ` · ${daysUntil(data.targetDate)} days left`
                      : ` · ${Math.abs(daysUntil(data.targetDate))} days past`}
                  </p>
                )}
              </div>
              <Progress value={Math.min(100, Math.round((data.activatedCount / data.targetCount) * 100))} />
              <p className="text-[12.5px] text-muted-foreground mt-2.5">
                {data.liveCount} live · {data.overdueCount} overdue (past 30 days) · {data.stalledCount} stalled (live over 14 days)
              </p>
            </CardContent>
          </Card>
          {data.milestones.length > 0 && (
            <Card>
              <div className="px-5 py-4 border-b border-border">
                <p className="text-[14px] font-semibold">Milestones</p>
              </div>
              {data.milestones.map((m) => <MilestoneRow key={m.date} m={m} />)}
            </Card>
          )}
        </div>
      ) : null}

      {/* Funnel strip */}
      <Card className="mb-4">
        <div className="px-5 py-4 border-b border-border">
          <p className="text-[14px] font-semibold">Onboarding funnel</p>
          <p className="text-[12px] text-muted-foreground mt-0.5">All time. Percentages are conversion from the stage before.</p>
        </div>
        {funnel.isError ? (
          <LoadError className="py-8" onRetry={() => funnel.refetch()} />
        ) : (
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6">
            {(funnel.isLoading ? Array.from({ length: 6 }, () => null) : stages).map((stage, i) => {
              const prev = i > 0 ? stages[i - 1]?.count : undefined;
              const rate = stage && prev ? Math.round((stage.count / prev) * 100) : null;
              return (
                <div key={stage?.key ?? i} className="px-5 py-4 border-b lg:border-b-0 border-r border-border last:border-r-0">
                  {stage ? (
                    <>
                      <p className="text-[12px] text-muted-foreground">{stage.label}</p>
                      <p className="text-[24px] font-bold tabular-nums leading-tight mt-1">{stage.count}</p>
                      <p className="text-[12px] text-muted-foreground tabular-nums mt-0.5">{rate !== null ? `${rate}%` : " "}</p>
                    </>
                  ) : (
                    <Skeleton className="h-14 w-full" />
                  )}
                </div>
              );
            })}
          </div>
        )}
      </Card>

      {/* Scorecard */}
      <Card className="mb-4">
        <div className="px-5 py-4 border-b border-border flex flex-col xl:flex-row xl:items-center justify-between gap-3">
          <div>
            <p className="text-[14px] font-semibold">
              Scorecard <span className="text-muted-foreground font-normal">{items.length} {items.length === 1 ? "institution" : "institutions"}</span>
            </p>
            <p className="text-[12px] text-muted-foreground mt-0.5">
              Activated means all five within 30 days of going live: branded portal, payouts approved, the member threshold (100 unless set per institution) with 30% signed in, a live campaign with an online payment, and 2+ staff active three weeks running.
            </p>
          </div>
          <ChipRow label="Filter by activation status" activeKey={filter}>
            {FILTERS.map((f) => (
              <button
                key={f.value}
                aria-pressed={filter === f.value}
                onClick={() => setFilter(f.value)}
                className={`text-[12.5px] font-medium px-3 py-1.5 border transition-colors ${
                  filter === f.value
                    ? "bg-primary/10 text-primary border-primary/30"
                    : "bg-background text-muted-foreground border-border hover:bg-muted"
                }`}
              >
                {f.label}
              </button>
            ))}
          </ChipRow>
        </div>
        <Table stackOnMobile>
          <TableHeader>
            <TableRow>
              <TableHead>Institution</TableHead>
              {CRITERIA_COLUMNS.map((c) => (
                <TableHead key={c.key}>{c.label}</TableHead>
              ))}
              <TableHead>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {scorecard.isError && (
              <tr><td colSpan={7}><LoadError className="py-10" onRetry={() => scorecard.refetch()} /></td></tr>
            )}
            {scorecard.isLoading &&
              Array.from({ length: 4 }, (_, i) => (
                <tr key={i}><td colSpan={7} className="px-5 py-3"><Skeleton className="h-9 w-full" /></td></tr>
              ))}
            {!scorecard.isLoading && !scorecard.isError && items.length === 0 && (
              <TableEmpty colSpan={7} title={filter === "all" ? "No live institutions yet" : "Nothing in this group"} />
            )}
            {items.map((item) => {
              const trial = trialLabel(item.trialEndsAt, item.isActivated);
              return (
                <TableRow key={item.institutionId}>
                  <TableCell className="align-top">
                    <div className="min-w-[240px] max-w-[340px]">
                      <Link href={`/institutions/${item.institutionId}`} className="font-semibold hover:underline">
                        {item.name}
                      </Link>
                      <p className="text-[12px] text-muted-foreground">
                        {item.isActivated && item.activatedAt
                          ? `Activated ${formatDate(item.activatedAt)}`
                          : `Day ${item.daysLive} of 30 · ${item.metCount} of 5 met`}
                        {trial && <> · {trial}</>}
                        {!item.setupNudgesEnabled && <> · reminders off</>}
                      </p>
                      {item.nextStep && (
                        <p className="text-[12.5px] leading-snug mt-2">
                          <span className="font-semibold">Next: </span>{item.nextStep}
                          {!item.isActivated && (
                            <button
                              type="button"
                              onClick={() => setTaskFor({ title: `${item.name}: ${item.nextStep}`, institutionId: item.institutionId })}
                              className="ml-2 text-[12px] font-semibold text-primary underline underline-offset-2 hover:no-underline"
                            >
                              Create task
                            </button>
                          )}
                        </p>
                      )}
                    </div>
                  </TableCell>
                  {CRITERIA_COLUMNS.map((col) => {
                    const c = item.criteria.find((x) => x.key === col.key);
                    if (!c) return <TableCell key={col.key}>—</TableCell>;
                    return (
                      <TableCell key={col.key} className="align-top">
                        <div className="flex items-start gap-1.5 min-w-[120px]">
                          {c.met ? (
                            <Check size={13} className="mt-[3px] shrink-0" style={{ color: "var(--success)" }} aria-label="Met" />
                          ) : (
                            <X size={13} className="mt-[3px] shrink-0 text-muted-foreground" aria-label="Not met" />
                          )}
                          <span className={`text-[12.5px] leading-snug ${c.met ? "" : "text-muted-foreground"}`}>{c.detail}</span>
                        </div>
                      </TableCell>
                    );
                  })}
                  <TableCell className="align-top"><StatusBadge item={item} /></TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </Card>

      {/* Week by week */}
      <Card>
        <div className="px-5 py-4 border-b border-border">
          <p className="text-[14px] font-semibold">Week by week</p>
          <p className="text-[12px] text-muted-foreground mt-0.5">How many leads and institutions first reached each stage that week (weeks start Monday).</p>
        </div>
        <Table stackOnMobile>
          <TableHeader>
            <TableRow>
              <TableHead>Week of</TableHead>
              <TableHead>New leads</TableHead>
              <TableHead>Contacted</TableHead>
              <TableHead>Demos</TableHead>
              <TableHead>Trials</TableHead>
              <TableHead>Went live</TableHead>
              <TableHead>Activated</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {funnel.isLoading &&
              Array.from({ length: 3 }, (_, i) => (
                <tr key={i}><td colSpan={7} className="px-5 py-3"><Skeleton className="h-6 w-full" /></td></tr>
              ))}
            {weeks.map((w, i) => (
              <TableRow key={w.weekStart}>
                <TableCell>
                  {formatDate(w.weekStart)}
                  {i === 0 && <span className="text-[12px] text-muted-foreground ml-1.5">this week</span>}
                </TableCell>
                {[w.leads, w.contacted, w.demoBooked, w.trial, w.live, w.activated].map((n, j) => (
                  <TableCell key={j} className={`tabular-nums ${n === 0 ? "text-muted-foreground" : "font-medium"}`}>{n}</TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>

      <Dialog open={targetOpen} onOpenChange={setTargetOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Activation target</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4 mt-2">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
              <div className="grid gap-1.5">
                <Label htmlFor="target-count">Institutions to activate</Label>
                <Input id="target-count" type="number" min={1} value={targetCount} onChange={(e) => setTargetCount(e.target.value)} placeholder="20" />
              </div>
              <div className="grid gap-1.5">
                <Label htmlFor="target-date">By (optional)</Label>
                <Input id="target-date" type="date" value={targetDate} onChange={(e) => setTargetDate(e.target.value)} />
              </div>
            </div>

            <div className="grid gap-2">
              <div className="flex items-center justify-between">
                <Label>Milestones (optional)</Label>
                <Button size="sm" variant="outline" onClick={() => setMilestones([...milestones, { date: "", liveTarget: "", activatedTarget: "" }])}>
                  <Plus size={12} className="mr-1" />Add milestone
                </Button>
              </div>
              {milestones.length === 0 && (
                <p className="text-[12.5px] text-muted-foreground">Checkpoints on the way, e.g. 5 live by 16 Oct, then 12 live and 5 activated by 30 Oct.</p>
              )}
              {milestones.map((m, i) => (
                <div key={i} className="grid grid-cols-[1.3fr_1fr_1fr_auto] gap-2 items-end">
                  <div className="grid gap-1">
                    {i === 0 && <span className="text-[11.5px] text-muted-foreground">Date</span>}
                    <Input type="date" aria-label="Milestone date" value={m.date} onChange={(e) => setMilestones(milestones.map((x, j) => (j === i ? { ...x, date: e.target.value } : x)))} />
                  </div>
                  <div className="grid gap-1">
                    {i === 0 && <span className="text-[11.5px] text-muted-foreground">Live</span>}
                    <Input type="number" min={0} aria-label="Live target" value={m.liveTarget} onChange={(e) => setMilestones(milestones.map((x, j) => (j === i ? { ...x, liveTarget: e.target.value } : x)))} />
                  </div>
                  <div className="grid gap-1">
                    {i === 0 && <span className="text-[11.5px] text-muted-foreground">Activated</span>}
                    <Input type="number" min={0} aria-label="Activated target" value={m.activatedTarget} onChange={(e) => setMilestones(milestones.map((x, j) => (j === i ? { ...x, activatedTarget: e.target.value } : x)))} />
                  </div>
                  <Button size="sm" variant="ghost" aria-label="Remove milestone" onClick={() => setMilestones(milestones.filter((_, j) => j !== i))}>
                    <Trash2 size={13} />
                  </Button>
                </div>
              ))}
            </div>
          </div>
          <DialogFooter>
            {data?.targetCount ? (
              <Button
                variant="outline"
                className="sm:mr-auto"
                disabled={targetMutation.isPending}
                onClick={() => targetMutation.mutate({ targetCount: null, targetDate: null, milestones: [] })}
              >
                Clear target
              </Button>
            ) : null}
            <Button variant="outline" onClick={() => setTargetOpen(false)}>Cancel</Button>
            <Button
              disabled={!targetCount || Number(targetCount) < 1 || !milestoneDraftsValid || targetMutation.isPending}
              onClick={saveTarget}
            >
              {targetMutation.isPending ? "Saving…" : "Save target"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      {taskFor && <TaskDialog prefill={taskFor} onClose={() => setTaskFor(null)} onSaved={() => { setTaskFor(null); toast.success("Task created. Find it under Targets or My tasks."); }} />}
    </div>
  );
}

const TABS = [
  { value: "scorecard", label: "Scorecard" },
  { value: "targets", label: "Targets" },
  { value: "tasks", label: "My tasks" },
] as const;
type Tab = (typeof TABS)[number]["value"];

function ActivationTabs() {
  const router = useRouter();
  const params = useSearchParams();
  const raw = params.get("tab");
  const tab: Tab = TABS.some((t) => t.value === raw) ? (raw as Tab) : "scorecard";

  return (
    <div>
      <div className="px-4 sm:px-7 pt-4 sm:pt-7">
        <SegmentedControl
          label="Activation sections"
          options={TABS.map((t) => ({ value: t.value, label: t.label }))}
          value={tab}
          onChange={(v) => router.replace(v === "scorecard" ? "/activation" : `/activation?tab=${v}`)}
        />
      </div>
      {tab === "scorecard" && <ScorecardTab />}
      {tab === "targets" && <TargetsTab />}
      {tab === "tasks" && <MyTasksTab />}
    </div>
  );
}

export default function ActivationPage() {
  return (
    <Suspense fallback={null}>
      <ActivationTabs />
    </Suspense>
  );
}
