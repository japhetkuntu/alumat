"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import {
  Briefcase, MapPin, Clock, ExternalLink, ArrowLeft,
  Building2, Calendar, Globe, Eye, XCircle, Pencil,
  CheckCircle2, AlertCircle, ChevronRight,
} from "lucide-react";
import Link from "next/link";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { FormSelect } from "@/components/ui/select";
import { ImageUpload } from "@/components/ui/image-upload";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { formatDate } from "@/lib/utils";
import { getJob, updateJob, closeJob, deleteJob, type UpdateJobBody } from "@/lib/admin-api";
import { handleApiError } from "@/lib/api-client";
import { EmptyState } from "@/components/ui/empty-state";
import { useState } from "react";
import type { Job } from "@/types";

const typeColors: Record<string, "info" | "secondary" | "warning" | "success"> = {
  "Full-time": "info",
  "Part-time": "secondary",
  Contract: "warning",
  Internship: "success",
};

const statusConfig: Record<string, { variant: "success" | "secondary" | "destructive" | "warning" | "info"; label: string; icon: React.ReactNode }> = {
  Active: { variant: "success", label: "Active", icon: <CheckCircle2 size={14} /> },
  Closed: { variant: "secondary", label: "Closed", icon: <XCircle size={14} /> },
  Draft: { variant: "warning", label: "Draft", icon: <AlertCircle size={14} /> },
};

interface FormState {
  title: string;
  company: string;
  location: string;
  type: string;
  description: string;
  applyUrl: string;
  deadline: string;
  status: string;
  bannerImage: File | null;
  existingBannerUrl: string;
}

function toFormState(j: Job): FormState {
  return {
    title: j.title,
    company: j.company,
    location: j.location,
    type: j.type,
    description: j.description ?? "",
    applyUrl: j.applyUrl ?? "",
    deadline: j.deadline ? j.deadline.split("T")[0] : "",
    status: j.status,
    bannerImage: null,
    existingBannerUrl: j.bannerImageUrl ?? "",
  };
}

export default function AdminJobDetailPage() {
  const { id } = useParams() as { id: string };
  const router = useRouter();
  const qc = useQueryClient();

  const [isEditing, setIsEditing] = useState(false);
  const [form, setForm] = useState<FormState | null>(null);
  const [closeTarget, setCloseTarget] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState(false);

  const { data: job, isLoading } = useQuery({
    queryKey: ["admin-job", id],
    queryFn: () => getJob(id),
  });

  const updateMut = useMutation({
    mutationFn: (f: FormState) =>
      updateJob(id, {
        title: f.title,
        company: f.company,
        location: f.location,
        type: f.type,
        description: f.description || undefined,
        applyUrl: f.applyUrl || undefined,
        deadline: f.deadline || undefined,
        status: f.status,
        bannerImage: f.bannerImage || undefined,
      } as UpdateJobBody),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-job", id] });
      qc.invalidateQueries({ queryKey: ["admin-jobs"] });
      setIsEditing(false);
      toast.success("Job updated");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const closeMut = useMutation({
    mutationFn: () => closeJob(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-job", id] });
      qc.invalidateQueries({ queryKey: ["admin-jobs"] });
      setCloseTarget(false);
      toast.success("Job closed");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const deleteMut = useMutation({
    mutationFn: () => deleteJob(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-jobs"] });
      toast.success("Job deleted");
      router.push("/jobs");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const startEdit = () => {
    if (job) {
      setForm(toFormState(job));
      setIsEditing(true);
    }
  };

  const f = (k: keyof FormState, v: string | File | null) =>
    setForm((prev) => prev ? { ...prev, [k]: v } : prev);

  if (isLoading) {
    return (
      <div className="p-8 lg:p-12 max-w-5xl mx-auto space-y-8 animate-pulse">
        <div className="h-8 w-32 bg-muted rounded-lg" />
        <div className="h-56 w-full bg-muted rounded-2xl" />
        <div className="space-y-4">
          <div className="h-10 w-2/3 bg-muted rounded-lg" />
          <div className="h-4 w-full bg-muted rounded-lg" />
          <div className="h-4 w-5/6 bg-muted rounded-lg" />
        </div>
      </div>
    );
  }

  if (!job) return (
    <div className="p-6 lg:p-12 max-w-6xl mx-auto">
      <Button variant="ghost" size="sm" className="mb-6 font-bold" onClick={() => router.push("/jobs")}>
        <ArrowLeft size={16} className="mr-2" /> Back to Jobs
      </Button>
      <EmptyState icon={<Briefcase size={48} />} title="Job not found" description="This listing may have been removed or the link is incorrect." />
    </div>
  );

  const sc = statusConfig[job.status] ?? statusConfig.Active;

  return (
    <div className="p-6 lg:p-12 max-w-6xl mx-auto space-y-10 pb-24">
      {/* Breadcrumb */}
      <nav className="flex items-center justify-between animate-in fade-in slide-in-from-top-4 duration-500">
        <div className="flex items-center gap-1.5 text-sm">
          <Button
            variant="ghost"
            size="sm"
            className="h-8 px-2 rounded-lg hover:bg-black/5 dark:hover:bg-white/5 font-bold group"
            onClick={() => router.push("/jobs")}
          >
            <ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />
            Jobs
          </Button>
          <ChevronRight size={14} className="text-muted-foreground/50" />
          <span className="text-[13px] font-semibold text-foreground/70 truncate max-w-[200px] sm:max-w-xs">{job.title}</span>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant={sc.variant} className="flex items-center gap-1.5 h-7 px-3 font-black uppercase tracking-widest text-[10px]">
            {sc.icon}
            {sc.label}
          </Badge>
        </div>
      </nav>

      {/* Edit Form */}
      {isEditing && form ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Edit Job Posting</CardTitle>
          </CardHeader>
          <CardContent>
            <form
              className="space-y-4"
              onSubmit={(e) => {
                e.preventDefault();
                if (form) updateMut.mutate(form);
              }}
            >
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Job Title</Label>
                  <Input value={form.title} onChange={(e) => f("title", e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Company</Label>
                  <Input value={form.company} onChange={(e) => f("company", e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Location</Label>
                  <Input value={form.location} onChange={(e) => f("location", e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Job Type</Label>
                  <FormSelect
                    value={form.type}
                    onValueChange={(v) => f("type", v)}
                    options={[
                      { value: "Full-time", label: "Full-time" },
                      { value: "Part-time", label: "Part-time" },
                      { value: "Contract", label: "Contract" },
                      { value: "Internship", label: "Internship" },
                    ]}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Deadline</Label>
                  <Input type="date" value={form.deadline} onChange={(e) => f("deadline", e.target.value)} />
                </div>
                <div className="space-y-2">
                  <Label>Apply URL</Label>
                  <Input type="url" value={form.applyUrl} onChange={(e) => f("applyUrl", e.target.value)} />
                </div>
                <div className="space-y-2">
                  <Label>Status</Label>
                  <FormSelect
                    value={form.status}
                    onValueChange={(v) => f("status", v)}
                    options={[
                      { value: "Active", label: "Active" },
                      { value: "Closed", label: "Closed" },
                      { value: "Draft", label: "Draft" },
                    ]}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Banner Image</Label>
                  <ImageUpload
                    file={form.bannerImage}
                    existingUrl={form.existingBannerUrl}
                    onChange={(file) => setForm((prev) => prev ? { ...prev, bannerImage: file } : prev)}
                    onClearExisting={() => setForm((prev) => prev ? { ...prev, existingBannerUrl: "" } : prev)}
                    label="Upload banner image"
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Description</Label>
                <Textarea rows={5} value={form.description} onChange={(e) => f("description", e.target.value)} />
              </div>
              <div className="flex gap-3">
                <Button type="submit" size="sm" isLoading={updateMut.isPending} loadingText="Saving">
                  Save Changes
                </Button>
                <Button type="button" size="sm" variant="outline" onClick={() => setIsEditing(false)}>
                  Cancel
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-10">
          {/* Main */}
          <div className="lg:col-span-8 space-y-10">
            {/* Banner */}
            {job.bannerImageUrl ? (
              <div className="rounded-[2rem] overflow-hidden shadow-2xl border-4 border-white dark:border-white/5 ring-1 ring-black/5 ring-offset-4 ring-offset-background aspect-video relative group animate-in fade-in zoom-in-95 duration-700">
                <img
                  src={job.bannerImageUrl}
                  alt={job.title}
                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-1000"
                />
                <div className="absolute inset-0 bg-gradient-to-t from-black/20 to-transparent" />
              </div>
            ) : (
              <div className="rounded-[2rem] bg-gradient-to-br from-primary/10 to-muted/20 aspect-video flex items-center justify-center border-4 border-white dark:border-white/5 ring-1 ring-black/5 animate-in fade-in duration-700">
                <Briefcase size={64} className="text-primary/20" />
              </div>
            )}

            {/* Title & Meta */}
            <header className="space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
              <div className="flex items-center gap-2 flex-wrap">
                <Badge variant={typeColors[job.type] ?? "secondary"} className="font-bold uppercase tracking-widest text-[10px] px-3 h-6">
                  {job.type}
                </Badge>
              </div>
              <h1 className="text-4xl sm:text-5xl font-black tracking-tight text-foreground leading-[1.1]">
                {job.title}
              </h1>
              <div className="flex flex-wrap items-center gap-6 text-sm font-bold text-muted-foreground">
                <div className="flex items-center gap-2">
                  <Building2 size={18} className="text-primary" />
                  {job.company}
                </div>
                <div className="flex items-center gap-2">
                  <MapPin size={18} className="text-primary" />
                  {job.location}
                </div>
                {job.deadline && (
                  <div className="flex items-center gap-2">
                    <Clock size={18} className="text-primary" />
                    Deadline: {formatDate(job.deadline)}
                  </div>
                )}
                <div className="flex items-center gap-2">
                  <Calendar size={18} className="text-primary" />
                  Posted {formatDate(job.createdAt)}
                </div>
              </div>
            </header>

            {/* Description */}
            <section className="space-y-6 animate-in fade-in slide-in-from-bottom-6 duration-700 delay-300">
              <h2 className="text-2xl font-black tracking-tight border-b border-border/40 pb-4">
                Job Description
              </h2>
              <div className="whitespace-pre-wrap font-medium text-muted-foreground leading-relaxed text-lg">
                {job.description || "No description provided for this job posting."}
              </div>
            </section>
          </div>

          {/* Sidebar */}
          <aside className="lg:col-span-4 animate-in fade-in slide-in-from-right-8 duration-1000 delay-300">
            <Card className="sticky top-24 overflow-hidden border-border/40 shadow-2xl shadow-primary/5 bg-card/80 backdrop-blur-xl">
              <div className={`absolute top-0 left-0 right-0 h-2 ${job.status === "Active" ? "bg-green-500" : job.status === "Draft" ? "bg-yellow-500" : "bg-muted-foreground"}`} />
              <CardContent className="p-8 space-y-8 pt-10">
                {/* Status */}
                <div>
                  <p className="text-[11px] font-bold text-muted-foreground uppercase tracking-[0.2em] mb-3">Status</p>
                  <div className="flex items-center gap-3 p-4 rounded-2xl bg-muted/20 border border-border/40">
                    <div className={`w-10 h-10 rounded-xl flex items-center justify-center shrink-0 ${job.status === "Active" ? "bg-green-500/10 text-green-600" : job.status === "Draft" ? "bg-yellow-500/10 text-yellow-600" : "bg-muted text-muted-foreground"}`}>
                      {sc.icon}
                    </div>
                    <div>
                      <p className="text-sm font-black">{sc.label}</p>
                      <p className="text-[10px] text-muted-foreground font-medium">
                        {job.status === "Active" ? "Visible to members" : job.status === "Draft" ? "Not yet published" : "No longer accepting applications"}
                      </p>
                    </div>
                  </div>
                </div>

                {/* Apply */}
                {job.applyUrl && (
                  <div>
                    <p className="text-[11px] font-bold text-muted-foreground uppercase tracking-[0.2em] mb-3">Application Link</p>
                    <a href={job.applyUrl} target="_blank" rel="noopener noreferrer">
                      <Button className="w-full h-14 rounded-2xl font-black text-base shadow-2xl shadow-primary/20 hover:shadow-primary/40 transition-all hover:scale-[1.02] active:scale-[0.98]">
                        <Globe size={20} className="mr-3" />
                        View Application
                        <ExternalLink size={14} className="ml-auto opacity-60" />
                      </Button>
                    </a>
                  </div>
                )}

                {/* Actions */}
                <div className="space-y-3 pt-4 border-t border-border/40">
                  <p className="text-[11px] font-bold text-muted-foreground uppercase tracking-[0.2em]">Actions</p>
                  <Button
                    variant="outline"
                    className="w-full justify-start h-11 rounded-xl font-bold"
                    onClick={startEdit}
                  >
                    <Pencil size={16} className="mr-2" />
                    Edit Posting
                  </Button>
                  {job.status === "Active" && (
                    <Button
                      variant="outline"
                      className="w-full justify-start h-11 rounded-xl font-bold border-orange-200 text-orange-600 hover:bg-orange-50 dark:border-orange-800 dark:text-orange-400 dark:hover:bg-orange-950"
                      onClick={() => setCloseTarget(true)}
                    >
                      <XCircle size={16} className="mr-2" />
                      Close Job
                    </Button>
                  )}
                  <Button
                    variant="outline"
                    className="w-full justify-start h-11 rounded-xl font-bold border-destructive/20 text-destructive hover:bg-destructive hover:text-white transition-all"
                    onClick={() => setDeleteTarget(true)}
                  >
                    <Eye size={16} className="mr-2" />
                    Delete Posting
                  </Button>
                </div>

                {/* Meta */}
                <div className="space-y-3 pt-4 border-t border-border/40">
                  <div className="flex items-center gap-3 p-3 rounded-xl bg-muted/20 border border-border/40">
                    <Building2 size={16} className="text-primary/60 shrink-0" />
                    <div>
                      <p className="text-[10px] font-bold text-muted-foreground uppercase tracking-widest">Company</p>
                      <p className="text-sm font-black">{job.company}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 p-3 rounded-xl bg-muted/20 border border-border/40">
                    <MapPin size={16} className="text-primary/60 shrink-0" />
                    <div>
                      <p className="text-[10px] font-bold text-muted-foreground uppercase tracking-widest">Location</p>
                      <p className="text-sm font-black">{job.location}</p>
                    </div>
                  </div>
                  {job.deadline && (
                    <div className="flex items-center gap-3 p-3 rounded-xl bg-muted/20 border border-border/40">
                      <Clock size={16} className="text-primary/60 shrink-0" />
                      <div>
                        <p className="text-[10px] font-bold text-muted-foreground uppercase tracking-widest">Application Deadline</p>
                        <p className="text-sm font-black">{formatDate(job.deadline)}</p>
                      </div>
                    </div>
                  )}
                </div>
              </CardContent>
            </Card>
          </aside>
        </div>
      )}

      <ConfirmModal
        open={closeTarget}
        title="Close Job Posting"
        message={`Close "${job.title}" at ${job.company}? Members will no longer see this posting.`}
        confirmLabel="Close Job"
        variant="destructive"
        isLoading={closeMut.isPending}
        onConfirm={() => closeMut.mutate()}
        onCancel={() => setCloseTarget(false)}
      />

      <ConfirmModal
        open={deleteTarget}
        title="Delete Job Posting"
        message={`Permanently delete "${job.title}" at ${job.company}? This cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        isLoading={deleteMut.isPending}
        onConfirm={() => deleteMut.mutate()}
        onCancel={() => setDeleteTarget(false)}
      />
    </div>
  );
}
