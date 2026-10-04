"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { cn, formatDate, formatDateTime } from "../lib/utils";
import { Badge } from "./badge";
import { Button } from "./button";
import { Card, CardContent, CardHeader, CardTitle } from "./card";
import { Download, Loader2 } from "./icons";
import { Input } from "./input";
import { Label } from "./label";
import { LoadError } from "./load-error";
import { FormSelect } from "./select";
import { Skeleton } from "./skeleton";

/* ─────────────────────────────────────────────────────────────────────────
   REPORT CENTER — the whole Reports screen, shared by the institution and
   platform portals. A report is never built while the page waits: asking
   for one queues it, the worker prepares it, and it appears under "Your
   reports" (and in the person's notifications and email) when it is ready.
   Each portal passes in its own API calls; everything else is the same.
   ───────────────────────────────────────────────────────────────────────── */

export interface ReportDefinitionItem {
  key: string;
  title: string;
  /** What the report answers, in the reader's words. */
  question: string;
  /** Filter names this report takes: from, to, status, yearFrom, yearTo, profession, location, year. */
  parameters: string[];
  statusOptions: string[];
}

export type ReportJobStatus = "Queued" | "Running" | "Ready" | "Failed" | "Expired";

export interface ReportJobItem {
  id: string;
  reportType: string;
  title: string;
  format: "xlsx" | "csv";
  parameters: Record<string, string>;
  status: ReportJobStatus;
  requestedAt: string;
  completedAt?: string | null;
  expiresAt?: string | null;
  rowCount: number;
  fileName?: string | null;
  fileSizeBytes: number;
  failureReason?: string | null;
}

export interface ReportCenterApi {
  getCatalog: () => Promise<ReportDefinitionItem[]>;
  /** The signed-in person's own reports, newest first. */
  getJobs: () => Promise<ReportJobItem[]>;
  request: (body: { reportType: string; format: string; parameters: Record<string, string> }) => Promise<ReportJobItem>;
  /** Fetches the finished file and hands it to the browser to save. */
  download: (job: ReportJobItem) => Promise<void>;
}

interface ReportCenterProps {
  api: ReportCenterApi;
  /** Turns a failed request into the sentence to show — each portal has its own error helper. */
  errorMessage: (error: unknown) => string;
  /** Community-type institutions don't collect graduation years, so the year filters are left out. */
  hideYearFilters?: boolean;
}

const ANY = "any";
const FORMATS = [
  { value: "xlsx", label: "Excel (.xlsx)" },
  { value: "csv", label: "CSV (.csv)" },
];

const FILTER_LABELS: Record<string, string> = {
  from: "From",
  to: "To",
  status: "Status",
  yearFrom: "Graduation year from",
  yearTo: "Graduation year to",
  profession: "Job title contains",
  location: "Location contains",
  year: "Dues year",
};

const FILTER_PLACEHOLDERS: Record<string, string> = {
  yearFrom: "e.g. 1990",
  yearTo: "e.g. 2005",
  profession: "e.g. Nurse",
  location: "e.g. Kumasi",
  year: String(new Date().getFullYear()),
};

/** The status filter of the dues report is "Paid" or "Owing" — a standing, not a payment status. */
function filterLabel(key: string, reportType: string): string {
  return key === "status" && reportType === "dues-standing" ? "Standing" : FILTER_LABELS[key] ?? key;
}

function inProgress(job: ReportJobItem) {
  return job.status === "Queued" || job.status === "Running";
}

function fileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** The filters a report was run with, as one readable line: "From 2026-01-01 · Status Paid". */
function filterSummary(job: ReportJobItem): string {
  return Object.entries(job.parameters)
    .map(([key, value]) => `${filterLabel(key, job.reportType)} ${value}`)
    .join(" · ");
}

function StatusBadge({ status }: { status: ReportJobStatus }) {
  switch (status) {
    case "Ready": return <Badge variant="success">Ready</Badge>;
    case "Failed": return <Badge variant="destructive">Couldn’t be prepared</Badge>;
    case "Expired": return <Badge variant="neutral">Expired</Badge>;
    default:
      return (
        <Badge variant="info" className="gap-1.5">
          <Loader2 size={11} className="animate-spin" />
          Preparing
        </Badge>
      );
  }
}

function JobRow({ job, onDownload, downloading }: { job: ReportJobItem; onDownload: () => void; downloading: boolean }) {
  const filters = filterSummary(job);
  return (
    <li className="flex flex-col gap-3 px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:gap-6 sm:px-5">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1">
          <p className="text-[14.5px] font-semibold text-foreground">{job.title}</p>
          <StatusBadge status={job.status} />
        </div>
        <p className="mt-1 text-[13px] text-muted-foreground">
          Requested {formatDateTime(job.requestedAt)} · {job.format === "csv" ? "CSV" : "Excel"}
          {filters ? ` · ${filters}` : " · No filters"}
        </p>
        {job.status === "Ready" && (
          <p className="mt-0.5 text-[13px] text-muted-foreground">
            {job.rowCount.toLocaleString()} {job.rowCount === 1 ? "row" : "rows"} · {fileSize(job.fileSizeBytes)}
            {job.expiresAt ? ` · Available until ${formatDate(job.expiresAt)}` : ""}
          </p>
        )}
        {job.status === "Failed" && job.failureReason && (
          <p className="mt-0.5 text-[13px] text-destructive">{job.failureReason}</p>
        )}
        {job.status === "Expired" && (
          <p className="mt-0.5 text-[13px] text-muted-foreground">The file was deleted after 7 days. Request the report again for a fresh copy.</p>
        )}
        {inProgress(job) && (
          <p className="mt-0.5 text-[13px] text-muted-foreground">You can leave this page. We’ll notify you here and by email when it’s ready.</p>
        )}
      </div>
      {job.status === "Ready" && (
        <Button size="sm" className="shrink-0 gap-1.5 font-semibold" onClick={onDownload} disabled={downloading}>
          {downloading ? <Loader2 size={13} className="animate-spin" /> : <Download size={13} />}
          {downloading ? "Downloading…" : "Download"}
        </Button>
      )}
    </li>
  );
}

export function ReportCenter({ api, errorMessage, hideYearFilters = false }: ReportCenterProps) {
  const queryClient = useQueryClient();
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [format, setFormat] = useState("xlsx");
  const [filters, setFilters] = useState<Record<string, string>>({});
  const [notice, setNotice] = useState<{ kind: "success" | "error"; text: string } | null>(null);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const catalog = useQuery({ queryKey: ["report-catalog"], queryFn: api.getCatalog, staleTime: 5 * 60_000 });
  const jobs = useQuery({
    queryKey: ["report-jobs"],
    queryFn: api.getJobs,
    // Only while something is being prepared; an idle page asks nothing.
    refetchInterval: (query) => (query.state.data?.some(inProgress) ? 4000 : false),
  });

  const definitions = catalog.data ?? [];
  const selected = definitions.find((d) => d.key === selectedKey) ?? definitions[0] ?? null;
  const visibleFilters = (selected?.parameters ?? []).filter((p) => !(hideYearFilters && (p === "yearFrom" || p === "yearTo")));

  const request = useMutation({
    mutationFn: () => {
      // Only the filters this report takes, and only the ones filled in.
      const parameters = Object.fromEntries(
        visibleFilters.map((key) => [key, (filters[key] ?? "").trim()]).filter(([, value]) => value && value !== ANY),
      );
      return api.request({ reportType: selected!.key, format, parameters });
    },
    onSuccess: (job) => {
      setNotice({ kind: "success", text: `We’re preparing your ${job.title.toLowerCase()} report. It will appear below, and we’ll notify you when it’s ready.` });
      queryClient.setQueryData<ReportJobItem[]>(["report-jobs"], (current) => [job, ...(current ?? [])]);
      void queryClient.invalidateQueries({ queryKey: ["report-jobs"] });
    },
    onError: (error) => setNotice({ kind: "error", text: errorMessage(error) }),
  });

  async function download(job: ReportJobItem) {
    setDownloadingId(job.id);
    try {
      await api.download(job);
    } catch (error) {
      setNotice({ kind: "error", text: await downloadErrorText(error, errorMessage) });
      // Most likely it expired while the page sat open; the list will now say so.
      void queryClient.invalidateQueries({ queryKey: ["report-jobs"] });
    } finally {
      setDownloadingId(null);
    }
  }

  function setFilter(key: string, value: string) {
    setFilters((current) => ({ ...current, [key]: value }));
  }

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Request a report</CardTitle>
          <p className="mt-1 text-[13px] text-muted-foreground">
            Choose what you want to know. We prepare the file in the background, so a large report never keeps you waiting on this page.
          </p>
        </CardHeader>
        <CardContent>
          {catalog.isLoading ? (
            <div className="space-y-3" aria-busy="true">
              {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-14 w-full" />)}
            </div>
          ) : catalog.isError ? (
            <LoadError title="The list of reports couldn’t load" onRetry={() => void catalog.refetch()} />
          ) : definitions.length === 0 ? (
            <p className="text-[14px] text-muted-foreground">There are no reports available to your account.</p>
          ) : (
            <div className="grid gap-6 lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
              <div role="radiogroup" aria-label="Report" className="self-start divide-y divide-border border border-border">
                {definitions.map((d) => {
                  const isSelected = d.key === selected?.key;
                  return (
                    <button
                      key={d.key}
                      type="button"
                      role="radio"
                      aria-checked={isSelected}
                      onClick={() => { setSelectedKey(d.key); setNotice(null); }}
                      className={cn(
                        "block w-full px-4 py-3 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring",
                        isSelected ? "bg-primary/5" : "hover:bg-muted/50",
                      )}
                    >
                      <span className={cn("block text-[14px] font-semibold", isSelected ? "text-primary" : "text-foreground")}>{d.title}</span>
                      <span className="mt-0.5 block text-[13px] leading-snug text-muted-foreground">{d.question}</span>
                    </button>
                  );
                })}
              </div>

              {selected && (
                <form
                  className="space-y-4"
                  onSubmit={(e) => { e.preventDefault(); setNotice(null); request.mutate(); }}
                >
                  <div>
                    <p className="text-[15px] font-semibold text-foreground">{selected.title}</p>
                    <p className="mt-0.5 text-[13px] text-muted-foreground">
                      {visibleFilters.length > 0 ? "Narrow it down if you need to. Leave a filter empty to include everything." : "This report has no filters; it covers everything you have access to."}
                    </p>
                  </div>

                  {visibleFilters.length > 0 && (
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      {visibleFilters.map((key) => {
                        const id = `report-filter-${key}`;
                        const label = filterLabel(key, selected.key);
                        return (
                          <div key={key} className="space-y-1.5">
                            <Label htmlFor={id} className="text-[12.5px] font-normal text-muted-foreground">{label}</Label>
                            {key === "status" ? (
                              <FormSelect
                                className="sm:w-full"
                                value={filters[key] || ANY}
                                onValueChange={(value) => setFilter(key, value)}
                                options={[{ value: ANY, label: "Any" }, ...selected.statusOptions.map((s) => ({ value: s, label: s }))]}
                              />
                            ) : (
                              <Input
                                id={id}
                                type={key === "from" || key === "to" ? "date" : key.toLowerCase().includes("year") ? "number" : "text"}
                                value={filters[key] ?? ""}
                                onChange={(e) => setFilter(key, e.target.value)}
                                placeholder={FILTER_PLACEHOLDERS[key]}
                              />
                            )}
                          </div>
                        );
                      })}
                    </div>
                  )}

                  <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
                    <div className="space-y-1.5">
                      <Label className="text-[12.5px] font-normal text-muted-foreground">File format</Label>
                      <FormSelect value={format} onValueChange={setFormat} options={FORMATS} />
                    </div>
                    <Button type="submit" className="gap-1.5 font-semibold" disabled={request.isPending}>
                      {request.isPending && <Loader2 size={14} className="animate-spin" />}
                      {request.isPending ? "Sending request…" : "Prepare report"}
                    </Button>
                  </div>

                  {notice && (
                    <p role="status" className={cn("text-[13.5px] leading-relaxed", notice.kind === "error" ? "text-destructive" : "text-foreground")}>
                      {notice.text}
                    </p>
                  )}
                </form>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Your reports</CardTitle>
          <p className="mt-1 text-[13px] text-muted-foreground">
            Only you can see and download these. Each file is kept for 7 days, then deleted.
          </p>
        </CardHeader>
        <CardContent className="p-0">
          {jobs.isLoading ? (
            <div className="space-y-3 p-5" aria-busy="true">
              {Array.from({ length: 2 }).map((_, i) => <Skeleton key={i} className="h-12 w-full" />)}
            </div>
          ) : jobs.isError && !jobs.data ? (
            <LoadError title="Your reports couldn’t load" onRetry={() => void jobs.refetch()} />
          ) : (jobs.data ?? []).length === 0 ? (
            <p className="px-5 pb-6 text-[14px] text-muted-foreground">
              You haven’t requested a report yet. Pick one above and it will show up here while it’s being prepared.
            </p>
          ) : (
            <ul className="divide-y divide-border border-t border-border">
              {(jobs.data ?? []).map((job) => (
                <JobRow key={job.id} job={job} onDownload={() => void download(job)} downloading={downloadingId === job.id} />
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

/** Saves a fetched file through the browser, named by the server's Content-Disposition when it gives one. */
/**
 * A download asks for a blob, so when it fails the server's JSON error arrives as a blob too and the
 * app's usual error reader finds no message in it. Read the blob's text and hand that back as a normal error.
 */
async function downloadErrorText(error: unknown, errorMessage: (error: unknown) => string): Promise<string> {
  const data = (error as { response?: { data?: unknown } })?.response?.data;
  if (data instanceof Blob) {
    try {
      const body = JSON.parse(await data.text()) as { message?: string };
      if (body.message) return body.message;
    } catch {
      // not JSON: fall through to the app's own reader
    }
  }
  return errorMessage(error);
}

export function saveBlob(blob: Blob, contentDisposition: string | undefined, fallbackName: string) {
  const fileName = contentDisposition?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i)?.[1] ?? fallbackName;
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = decodeURIComponent(fileName);
  link.click();
  URL.revokeObjectURL(link.href);
}
