"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Briefcase, MapPin, Clock, ExternalLink, Pencil, Archive } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { FormSelect } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { SearchModal } from "@/components/ui/search-modal";
import { formatDate } from "@/lib/utils";
import { getJobs, createJob, updateJob, deleteJob } from "@/lib/admin-api";
import { ImageUpload } from "@/components/ui/image-upload";
import { useAuth } from "@/hooks/use-auth";
import { YearGroupPicker } from "@/components/ui/year-group-picker";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import type { Job } from "@/types";

const typeColors: Record<string, string> = {
  "Full-time": "bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-200 dark:border-blue-800",
  FullTime: "bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-200 dark:border-blue-800",
  "Part-time": "bg-violet-500/10 text-violet-600 dark:text-violet-400 border-violet-200 dark:border-violet-800",
  PartTime: "bg-violet-500/10 text-violet-600 dark:text-violet-400 border-violet-200 dark:border-violet-800",
  Contract: "bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-200 dark:border-amber-800",
  Internship: "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800",
  Remote: "bg-cyan-500/10 text-cyan-600 dark:text-cyan-400 border-cyan-200 dark:border-cyan-800",
};

const statusColors: Record<string, string> = {
  Active: "bg-emerald-500/10 text-emerald-600 border-emerald-200",
  Closed: "bg-rose-500/10 text-rose-600 border-rose-200",
  Draft: "bg-slate-500/10 text-slate-600 border-slate-200",
};

interface FormState {
  title: string;
  company: string;
  location: string;
  type: string;
  description: string;
  applyUrl: string;
  deadline: string;
  yearGroups: number[];
  bannerImage: File | null;
  existingBannerUrl: string;
}
const emptyForm: FormState = { title: "", company: "", location: "", type: "Full-time", description: "", applyUrl: "", deadline: "", yearGroups: [], bannerImage: null, existingBannerUrl: "" };
const statusOptions = ["Active", "Closed", "Draft"];

function JobForm({ init, onSave, onCancel, saving, showStatus, title, isSuperAdmin }: {
  init: FormState & { status?: string }; onSave: (f: FormState & { status: string }) => void;
  onCancel: () => void; saving: boolean; showStatus: boolean; title: string; isSuperAdmin: boolean;
}) {
  const [form, setForm] = useState({ status: "Active", ...init });
  const f = (k: string, v: string) => setForm(prev => ({ ...prev, [k]: v }));
  return (
    <Card>
      <CardHeader><CardTitle className="text-base">{title}</CardTitle></CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); onSave(form); }}>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2"><Label>Job Title</Label>
              <Input placeholder="e.g. Senior Mining Engineer" value={form.title} onChange={(e) => f("title", e.target.value)} required /></div>
            <div className="space-y-2"><Label>Company</Label>
              <Input placeholder="e.g. Gold Fields Ghana" value={form.company} onChange={(e) => f("company", e.target.value)} required /></div>
            <div className="space-y-2"><Label>Location</Label>
              <Input placeholder="e.g. Tarkwa, Ghana" value={form.location} onChange={(e) => f("location", e.target.value)} required /></div>
            <div className="space-y-2"><Label>Job Type</Label>
              <FormSelect value={form.type} onValueChange={(v) => f("type", v)}
                options={[
                  { value: "Full-time", label: "Full-time" },
                  { value: "Part-time", label: "Part-time" },
                  { value: "Contract", label: "Contract" },
                  { value: "Internship", label: "Internship" },
                ]} /></div>
            <div className="space-y-2"><Label>Target year groups</Label>
              {isSuperAdmin ? (
                <>
                  <div className="flex items-center justify-between">
                    <Label></Label>
                    <label className="flex items-center gap-2 text-sm font-medium">
                      <input
                        type="checkbox"
                        checked={form.yearGroups.length === 0}
                        onChange={(e) => setForm((prev) => ({ ...prev, yearGroups: e.target.checked ? [] : prev.yearGroups }))}
                        className="h-4 w-4 rounded border border-muted-foreground"
                      />
                      All years
                    </label>
                  </div>
                  {form.yearGroups.length > 0 ? (
                    <YearGroupPicker value={form.yearGroups} onChange={(years) => setForm(prev => ({ ...prev, yearGroups: years }))} />
                  ) : (
                    <p className="text-xs text-muted-foreground">This job will be visible to members of all year groups.</p>
                  )}
                </>
              ) : (
                <p className="text-xs text-muted-foreground">Regular admins cannot choose year groups; this will use your year group automatically.</p>
              )}
            </div>
            <div className="space-y-2"><Label>Deadline (optional)</Label>
              <Input type="date" value={form.deadline} onChange={(e) => f("deadline", e.target.value)} /></div>
            <div className="space-y-2"><Label>Apply URL (optional)</Label>
              <Input type="url" placeholder="https://..." value={form.applyUrl} onChange={(e) => f("applyUrl", e.target.value)} /></div>
            <div className="space-y-2"><Label>Banner Image (optional)</Label>
              <ImageUpload file={form.bannerImage} existingUrl={form.existingBannerUrl} onChange={(file) => setForm(prev => ({ ...prev, bannerImage: file }))} onClearExisting={() => setForm(prev => ({ ...prev, existingBannerUrl: "" }))} label="Upload banner image" /></div>
            {showStatus && (
              <div className="space-y-2"><Label>Status</Label>
                <FormSelect value={form.status} onValueChange={(v) => f("status", v)}
                  options={statusOptions.map(s => ({ value: s, label: s }))} /></div>
            )}
          </div>
          <div className="space-y-2"><Label>Job Description</Label>
            <Textarea placeholder="Describe the role..." rows={4} value={form.description} onChange={(e) => f("description", e.target.value)} /></div>
          <div className="flex gap-3">
            <Button type="submit" size="sm" isLoading={saving} loadingText="Saving">Save</Button>
            <Button type="button" size="sm" variant="outline" onClick={onCancel}>Cancel</Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

export default function AdminJobsPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";

  const [showCreate, setShowCreate] = useState(false);
  const [editJob, setEditJob] = useState<Job | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Job | null>(null);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [locationFilter, setLocationFilter] = useState("");
  const [postedAfter, setPostedAfter] = useState("");
  const [postedBefore, setPostedBefore] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["admin-jobs", search, statusFilter, typeFilter, locationFilter, postedAfter, postedBefore, page],
    queryFn: () => getJobs(
      page,
      pageSize,
      search || undefined,
      statusFilter || undefined,
      typeFilter || undefined,
      locationFilter || undefined,
      postedAfter || undefined,
      postedBefore || undefined,
    ),
    placeholderData: (prev) => prev,
  });

  const createMut = useMutation({
    mutationFn: (f: FormState) => createJob({
      title: f.title, company: f.company, location: f.location, type: f.type,
      description: f.description || undefined, applyUrl: f.applyUrl || undefined,
      deadline: f.deadline || undefined, yearGroups: isSuperAdmin ? (f.yearGroups.length > 0 ? f.yearGroups : undefined) : undefined,
      bannerImage: f.bannerImage || undefined,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-jobs"] }); setShowCreate(false); toast.success("Job posted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, f }: { id: string; f: FormState & { status: string } }) => updateJob(id, {
      title: f.title, company: f.company, location: f.location, type: f.type,
      description: f.description || undefined, applyUrl: f.applyUrl || undefined,
      deadline: f.deadline || undefined, status: f.status,
      yearGroups: isSuperAdmin ? (f.yearGroups.length > 0 ? f.yearGroups : undefined) : undefined,
      bannerImage: f.bannerImage || undefined,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-jobs"] }); setEditJob(null); toast.success("Job updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => deleteJob(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-jobs"] }); setDeleteTarget(null); toast.success("Job deleted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const jobs = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-8 lg:p-12 space-y-8 max-w-7xl mx-auto">
      {/* Header */}
      <header className="flex items-end justify-between gap-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div>
          <h1 className="text-3xl font-black tracking-tight">Job Board</h1>
          <p className="text-muted-foreground text-sm font-medium mt-1">Manage job postings across the alumni network</p>
        </div>
        <Button onClick={() => setShowCreate(!showCreate)} className="gap-1.5 font-bold">
          <Plus size={15} />Post Job
        </Button>
      </header>

      {/* Create / Edit forms */}
      {showCreate && (
        <JobForm title="Post a New Job" init={emptyForm} saving={createMut.isPending} showStatus={false} isSuperAdmin={isSuperAdmin}
          onSave={(f) => createMut.mutate(f)} onCancel={() => setShowCreate(false)} />
      )}
      {editJob && (
        <JobForm
          title={`Edit — ${editJob.title}`}
          init={{ title: editJob.title, company: editJob.company, location: editJob.location, type: editJob.type,
            description: editJob.description ?? "", applyUrl: editJob.applyUrl ?? "",
            deadline: editJob.deadline ? editJob.deadline.split("T")[0] : "", yearGroups: editJob.yearGroups ?? [], bannerImage: null,
            existingBannerUrl: editJob.bannerImageUrl ?? "",
            status: editJob.status }}
          saving={updateMut.isPending} showStatus isSuperAdmin={isSuperAdmin}
          onSave={(f) => updateMut.mutate({ id: editJob.id, f })}
          onCancel={() => setEditJob(null)}
        />
      )}

      {/* Filters */}
      <div className="space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
          <div className="flex-1 min-w-0">
            <SearchModal
              title="Search jobs"
              value={search}
              onChange={(value) => { setSearch(value); setPage(1); }}
              placeholder="Search by title, company, or location..."
            >
              {isLoading ? (
                <p className="text-sm text-muted-foreground">Loading results…</p>
              ) : jobs.length === 0 ? (
                <p className="text-sm text-muted-foreground">No results match your search.</p>
              ) : (
                <div className="space-y-2 max-h-72 overflow-y-auto">
                  {jobs.slice(0, 5).map((j) => (
                    <div key={j.id} className="flex items-center justify-between gap-3 rounded-lg border border-border/40 bg-muted/30 px-3 py-2">
                      <div className="min-w-0">
                        <p className="text-sm font-semibold truncate">{j.title}</p>
                        <p className="text-xs text-muted-foreground truncate">{j.company} · {j.location}</p>
                      </div>
                      <span className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">{j.status}</span>
                    </div>
                  ))}
                  {jobs.length > 5 && (
                    <p className="text-xs text-muted-foreground">Showing {Math.min(5, jobs.length)} of {jobs.length} results. Close to view the full list.</p>
                  )}
                </div>
              )}
            </SearchModal>
          </div>
          <Input placeholder="Filter by location" className="h-11" value={locationFilter} onChange={(e) => { setLocationFilter(e.target.value); setPage(1); }} />
          <Input type="date" className="h-11" value={postedAfter} onChange={(e) => { setPostedAfter(e.target.value); setPage(1); }} />
          <Input type="date" className="h-11" value={postedBefore} onChange={(e) => { setPostedBefore(e.target.value); setPage(1); }} />
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          {/* Status pills */}
          {["", "Active", "Draft", "Closed"].map((s) => (
            <button
              key={s}
              onClick={() => { setStatusFilter(s); setPage(1); }}
              className={`px-4 py-1.5 rounded-full text-[11px] font-black uppercase tracking-widest transition-all ${
                statusFilter === s ? "bg-primary text-primary-foreground shadow-md shadow-primary/20" : "bg-muted/50 text-muted-foreground hover:bg-muted"
              }`}
            >
              {s === "" ? "All" : s}
            </button>
          ))}
          <div className="w-px h-6 bg-border mx-1" />
          {/* Type pills */}
          {["", "Full-time", "Part-time", "Contract", "Internship"].map((t) => (
            <button
              key={t}
              onClick={() => { setTypeFilter(t); setPage(1); }}
              className={`px-3 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider transition-all border ${
                typeFilter === t ? "bg-foreground/10 text-foreground border-foreground/20" : "bg-transparent text-muted-foreground border-border/50 hover:border-foreground/20 hover:text-foreground"
              }`}
            >
              {t === "" ? "All Types" : t}
            </button>
          ))}
        </div>
      </div>

      {/* Job Cards Grid */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : jobs.length === 0 ? (
        <EmptyState icon={<Briefcase size={40} />} title="No jobs found" description="Post your first job listing to help alumni find opportunities." action={<Button onClick={() => setShowCreate(true)}><Plus size={14} />Post Job</Button>} />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5">
          {jobs.map((j, i) => (
            <Card
              key={j.id}
              className="group flex flex-col overflow-hidden border-border/40 hover:border-primary/30 transition-all duration-500 hover:shadow-xl hover:shadow-primary/5 animate-in fade-in slide-in-from-bottom-6 duration-700"
              style={{ animationDelay: `${i * 60}ms` }}
            >
              <CardContent className="flex-1 flex flex-col p-5 space-y-3">
                {/* Top: Icon + badges */}
                <div className="flex items-start justify-between gap-2">
                  <div className="h-10 w-10 rounded-xl bg-muted/60 flex items-center justify-center shrink-0 group-hover:bg-primary/10 transition-colors">
                    <Briefcase size={18} className="text-muted-foreground group-hover:text-primary transition-colors" />
                  </div>
                  <div className="flex gap-1.5 flex-wrap justify-end">
                    <span className={`px-2 py-0.5 rounded-full text-[9px] font-black uppercase tracking-widest border ${typeColors[j.type] ?? "bg-muted text-muted-foreground border-border"}`}>
                      {j.type}
                    </span>
                    <span className={`px-2 py-0.5 rounded-full text-[9px] font-black uppercase tracking-widest border ${statusColors[j.status] ?? "bg-muted text-muted-foreground border-border"}`}>
                      {j.status}
                    </span>
                  </div>
                </div>

                {/* Title & company */}
                <div className="flex-1 space-y-1 min-w-0">
                  <h3 className="font-bold text-[15px] leading-snug line-clamp-2 group-hover:text-primary transition-colors">{j.title}</h3>
                  <p className="text-sm font-medium text-muted-foreground">{j.company}</p>
                </div>

                {/* Meta */}
                <div className="flex items-center gap-3 text-[11px] text-muted-foreground flex-wrap">
                  <span className="flex items-center gap-1"><MapPin size={11} />{j.location}</span>
                  {j.deadline && <span className="flex items-center gap-1"><Clock size={11} />{formatDate(j.deadline)}</span>}
                </div>

                {j.description && (
                  <p className="text-[12px] text-muted-foreground line-clamp-2">{j.description}</p>
                )}

                {/* Actions */}
                <div className="flex items-center gap-2 pt-1 border-t border-border/40">
                  <Button size="sm" variant="outline" className="flex-1 h-9 text-[11px] font-bold gap-1" onClick={() => setEditJob(j)}>
                    <Pencil size={12} />Edit
                  </Button>
                  {j.applyUrl && (
                    <a href={j.applyUrl} target="_blank" rel="noopener noreferrer" className="flex-1">
                      <Button size="sm" variant="outline" className="w-full h-9 text-[11px] font-bold gap-1">
                        <ExternalLink size={12} />Preview
                      </Button>
                    </a>
                  )}
                  <Button size="sm" variant="ghost" className="h-9 px-2 text-destructive hover:bg-destructive/10" onClick={() => setDeleteTarget(j)} title="Delete">
                    <Archive size={13} />
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      <ConfirmModal
        open={!!deleteTarget}
        title="Delete Job"
        message={`Delete "${deleteTarget?.title}" at ${deleteTarget?.company}? This cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        isLoading={deleteMut.isPending}
        onConfirm={() => deleteTarget && deleteMut.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
