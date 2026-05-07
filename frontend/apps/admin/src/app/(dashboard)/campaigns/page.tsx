"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Eye, Pencil, Archive, ArchiveRestore } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import Link from "next/link";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { formatCurrency, formatDate } from "@/lib/utils";
import { getCampaigns, createCampaign, updateCampaign, archiveCampaign, unarchiveCampaign, activateCampaign } from "@/lib/admin-api";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import type { Campaign, CampaignStatus } from "@/types";
import { ImageUpload } from "@/components/ui/image-upload";
import { YouTubePreview } from "@/components/ui/youtube-embed";
import { YearGroupPicker } from "@/components/ui/year-group-picker";

const statusVariant: Record<CampaignStatus, "success" | "info" | "secondary" | "warning"> = {
  Active: "success",
  Draft: "info",
  Closed: "secondary",
  Completed: "secondary",
  Archived: "warning",
};

interface FormState {
  title: string;
  description: string;
  targetAmount: string;
  minContribution: string;
  deadline: string;
  yearGroupsAll: boolean;
  yearGroups: number[];
  bannerImage: File | null;
  existingBannerUrl: string;
  youtubeVideoUrl: string;
  allowOnlinePayments: boolean;
  allowManualPayments: boolean;
  bankAccountNumber: string;
  bankAccountName: string;
  bankName: string;
  bankBranch: string;
  mobileMoneyNumber: string;
  mobileMoneyName: string;
  mobileMoneyProvider: string;
}
const emptyForm: FormState = {
  title: "", description: "", targetAmount: "", minContribution: "", deadline: "",
  yearGroupsAll: true, yearGroups: [],
  bannerImage: null, existingBannerUrl: "", youtubeVideoUrl: "",
  allowOnlinePayments: true,
  allowManualPayments: false,
  bankAccountNumber: "",
  bankAccountName: "",
  bankName: "",
  bankBranch: "",
  mobileMoneyNumber: "",
  mobileMoneyName: "",
  mobileMoneyProvider: "",
};

function CampaignForm({ init, onSave, onCancel, saving, title, isSuperAdmin }: { init: FormState; onSave: (f: FormState) => void; onCancel: () => void; saving: boolean; title: string; isSuperAdmin: boolean }) {
  const [form, setForm] = useState(init);
  const f = <K extends keyof FormState>(k: K, v: FormState[K]) => setForm(prev => ({ ...prev, [k]: v }));
  return (
    <Card>
      <CardHeader><CardTitle className="text-base">{title}</CardTitle></CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); onSave(form); }}>
          <div className="space-y-2">
            <Label>Campaign title</Label>
            <Input placeholder="e.g. Alumni Development Fund 2026" value={form.title} onChange={(e) => f("title", e.target.value)} required />
          </div>
          <div className="space-y-2">
            <Label>Description</Label>
            <Textarea placeholder="Describe the purpose of this campaign..." rows={3} value={form.description} onChange={(e) => f("description", e.target.value)} />
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label>Target amount (GHS)</Label>
              <Input type="number" placeholder="100000" value={form.targetAmount} onChange={(e) => f("targetAmount", e.target.value)} required />
            </div>
            <div className="space-y-2">
              <Label>Minimum Contribution (GHS)</Label>
              <Input type="number" placeholder="200" value={form.minContribution} onChange={(e) => f("minContribution", e.target.value)} required />
            </div>
            <div className="space-y-2">
              <Label>Deadline</Label>
              <Input type="date" value={form.deadline} onChange={(e) => f("deadline", e.target.value)} required />
            </div>
          </div>

          <div className="space-y-2">
            {isSuperAdmin ? (
              <>
                <div className="flex items-center justify-between">
                  <Label>Target year groups</Label>
                  <label className="flex items-center gap-2 text-sm font-medium">
                    <input
                      type="checkbox"
                      checked={form.yearGroupsAll}
                      onChange={(e) => setForm((prev) => ({ ...prev, yearGroupsAll: e.target.checked }))}
                      className="h-4 w-4 rounded border border-muted-foreground"
                    />
                    All years
                  </label>
                </div>
                {!form.yearGroupsAll ? (
                  <YearGroupPicker
                    value={form.yearGroups}
                    onChange={(years) => setForm((prev) => ({ ...prev, yearGroups: years }))}
                  />
                ) : (
                  <p className="text-xs text-muted-foreground">This campaign will be visible to members of all year groups.</p>
                )}
              </>
            ) : (
              <p className="text-xs text-muted-foreground">Regular admins cannot choose year groups. Campaigns will be restricted to your assigned year group.</p>
            )}
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Banner image (optional)</Label>
              <ImageUpload
                file={form.bannerImage}
                existingUrl={form.existingBannerUrl}
                onChange={(file) => setForm(prev => ({ ...prev, bannerImage: file }))}
                onClearExisting={() => setForm(prev => ({ ...prev, existingBannerUrl: "" }))}
                label="Upload banner image"
              />
            </div>
            <div className="space-y-2">
              <Label>YouTube video URL (optional)</Label>
              <Input type="url" placeholder="https://youtube.com/..." value={form.youtubeVideoUrl} onChange={(e) => f("youtubeVideoUrl", e.target.value)} />
              {form.youtubeVideoUrl && <YouTubePreview url={form.youtubeVideoUrl} />}
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Allow Online Payments</Label>
              <div className="flex items-center gap-2">
                <input id="online-pay" type="checkbox" checked={form.allowOnlinePayments} onChange={(e) => f("allowOnlinePayments", e.target.checked)} className="h-4 w-4" />
                <label htmlFor="online-pay" className="text-sm">Enable Paystack / online gateway</label>
              </div>
            </div>
            <div className="space-y-2">
              <Label>Allow Manual Payments</Label>
              <div className="flex items-center gap-2">
                <input id="manual-pay" type="checkbox" checked={form.allowManualPayments} onChange={(e) => f("allowManualPayments", e.target.checked)} className="h-4 w-4" />
                <label htmlFor="manual-pay" className="text-sm">Allow bank/mobile money transfers</label>
              </div>
            </div>
          </div>

          {form.allowManualPayments && (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
              <div className="p-4 border border-border rounded-lg space-y-2">
                <h4 className="text-sm font-black uppercase tracking-wider">Bank Account</h4>
                <Input placeholder="Account number" value={form.bankAccountNumber} onChange={(e) => f("bankAccountNumber", e.target.value)} />
                <Input placeholder="Account name" value={form.bankAccountName} onChange={(e) => f("bankAccountName", e.target.value)} />
                <Input placeholder="Bank name" value={form.bankName} onChange={(e) => f("bankName", e.target.value)} />
                <Input placeholder="Branch" value={form.bankBranch} onChange={(e) => f("bankBranch", e.target.value)} />
              </div>
              <div className="p-4 border border-border rounded-lg space-y-2">
                <h4 className="text-sm font-black uppercase tracking-wider">Mobile Money</h4>
                <Input placeholder="Mobile money number" value={form.mobileMoneyNumber} onChange={(e) => f("mobileMoneyNumber", e.target.value)} />
                <Input placeholder="Account name" value={form.mobileMoneyName} onChange={(e) => f("mobileMoneyName", e.target.value)} />
                <Input placeholder="Provider (MTN, Telecel, AT)" value={form.mobileMoneyProvider} onChange={(e) => f("mobileMoneyProvider", e.target.value)} />
              </div>
            </div>
          )}

          <div className="flex gap-3 pt-2">
            <Button type="submit" size="sm" isLoading={saving} loadingText="Saving">Save</Button>
            <Button type="button" size="sm" variant="outline" onClick={onCancel}>Cancel</Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

export default function AdminCampaignsPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";

  const [showCreate, setShowCreate] = useState(false);
  const [editCampaign, setEditCampaign] = useState<Campaign | null>(null);
  const [closeTarget, setCloseTarget] = useState<Campaign | null>(null);
  const [archiveTarget, setArchiveTarget] = useState<Campaign | null>(null);
  const [unarchiveTarget, setUnarchiveTarget] = useState<Campaign | null>(null);
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["admin-campaigns", statusFilter, page],
    queryFn: () => getCampaigns(page, pageSize, statusFilter || undefined),
    placeholderData: (prev) => prev,
  });

  const createMut = useMutation({
    mutationFn: (f: FormState) => createCampaign({
      title: f.title, description: f.description,
      targetAmount: Number(f.targetAmount), amountPerMember: Number(f.minContribution),
      deadline: f.deadline,
      yearGroups: isSuperAdmin ? (f.yearGroupsAll ? undefined : f.yearGroups) : undefined,
      bannerImage: f.bannerImage || undefined,
      youtubeVideoUrl: f.youtubeVideoUrl || undefined,
      allowOnlinePayments: f.allowOnlinePayments,
      allowManualPayments: f.allowManualPayments,
      bankAccountNumber: f.bankAccountNumber || undefined,
      bankAccountName: f.bankAccountName || undefined,
      bankName: f.bankName || undefined,
      bankBranch: f.bankBranch || undefined,
      mobileMoneyNumber: f.mobileMoneyNumber || undefined,
      mobileMoneyName: f.mobileMoneyName || undefined,
      mobileMoneyProvider: f.mobileMoneyProvider || undefined,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-campaigns"] }); setShowCreate(false); toast.success("Campaign created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, f, status }: { id: string; f: FormState; status: string }) => updateCampaign(id, {
      title: f.title, description: f.description || undefined, deadline: f.deadline, status,
      targetAmount: Number(f.targetAmount), amountPerMember: Number(f.minContribution),
      yearGroups: isSuperAdmin ? (f.yearGroupsAll ? undefined : f.yearGroups) : undefined,
      bannerImage: f.bannerImage || undefined, youtubeVideoUrl: f.youtubeVideoUrl || undefined,
      allowOnlinePayments: f.allowOnlinePayments,
      allowManualPayments: f.allowManualPayments,
      bankAccountNumber: f.bankAccountNumber || undefined,
      bankAccountName: f.bankAccountName || undefined,
      bankName: f.bankName || undefined,
      bankBranch: f.bankBranch || undefined,
      mobileMoneyNumber: f.mobileMoneyNumber || undefined,
      mobileMoneyName: f.mobileMoneyName || undefined,
      mobileMoneyProvider: f.mobileMoneyProvider || undefined,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-campaigns"] }); setEditCampaign(null); toast.success("Campaign updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const closeMut = useMutation({
    mutationFn: (id: string) => updateCampaign(id, { status: "Closed" }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-campaigns"] }); setCloseTarget(null); toast.success("Campaign closed"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const archiveMut = useMutation({
    mutationFn: (id: string) => archiveCampaign(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-campaigns"] }); setArchiveTarget(null); toast.success("Campaign archived"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const unarchiveMut = useMutation({
    mutationFn: (id: string) => unarchiveCampaign(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-campaigns"] }); setUnarchiveTarget(null); toast.success("Campaign restored"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const activateMut = useMutation({
    mutationFn: (id: string) => activateCampaign(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-campaigns"] }); toast.success("Campaign activated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const campaigns = (data?.results ?? []).filter((c) => !c.isMembershipCampaign);
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-8 lg:p-12 space-y-10 max-w-7xl mx-auto">
      <header className="flex items-end justify-between gap-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div className="space-y-1">
          <p className="text-[11px] font-black uppercase tracking-[0.2em] text-primary">Financial</p>
          <h1 className="text-4xl font-black tracking-tight text-foreground">Campaigns</h1>
          <p className="text-muted-foreground font-medium">Manage contribution campaigns and track fundraising progress</p>
        </div>
        <Button onClick={() => setShowCreate(!showCreate)} className="shadow-lg shadow-primary/20 font-bold h-11 px-5">
          <Plus size={16} />New Campaign
        </Button>
      </header>

      {showCreate && (
        <CampaignForm
          title="Create New Campaign"
          init={emptyForm}
          saving={createMut.isPending}
          isSuperAdmin={isSuperAdmin}
          onSave={(f) => createMut.mutate(f)}
          onCancel={() => setShowCreate(false)}
        />
      )}

      {editCampaign && (
        <CampaignForm
          title={`Edit — ${editCampaign.title}`}
          init={{
            title: editCampaign.title,
            description: editCampaign.description ?? "",
            targetAmount: String(editCampaign.targetAmount),
            minContribution: String(editCampaign.amountPerMember),
            deadline: editCampaign.deadline.split("T")[0],
            yearGroupsAll: !editCampaign.yearGroups || editCampaign.yearGroups.length === 0,
            yearGroups: editCampaign.yearGroups ?? [],
            bannerImage: null,
            existingBannerUrl: editCampaign.bannerImageUrl ?? "",
            youtubeVideoUrl: editCampaign.youtubeVideoUrl ?? "",
            allowOnlinePayments: editCampaign.allowOnlinePayments,
            allowManualPayments: editCampaign.allowManualPayments,
            bankAccountNumber: editCampaign.bankAccount?.accountNumber ?? "",
            bankAccountName: editCampaign.bankAccount?.accountName ?? "",
            bankName: editCampaign.bankAccount?.bankName ?? "",
            bankBranch: editCampaign.bankAccount?.branch ?? "",
            mobileMoneyNumber: editCampaign.mobileMoneyAccount?.mobileMoneyNumber ?? "",
            mobileMoneyName: editCampaign.mobileMoneyAccount?.name ?? "",
            mobileMoneyProvider: editCampaign.mobileMoneyAccount?.provider ?? "",
          }}
          saving={updateMut.isPending}
          isSuperAdmin={isSuperAdmin}
          onSave={(f) => updateMut.mutate({ id: editCampaign.id, f, status: editCampaign.status })}
          onCancel={() => setEditCampaign(null)}
        />
      )}

      {/* Status Filter */}
      <div className="flex items-center gap-2 flex-wrap">
        {["", "Active", "Draft", "Closed", "Completed", "Archived"].map((s) => (
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
      </div>


      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
          {Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : campaigns.length === 0 ? (
        <EmptyState icon={<Plus size={40} />} title="No campaigns yet" description="Create a contribution campaign to start collecting funds." action={<Button onClick={() => setShowCreate(true)}><Plus size={14} />New Campaign</Button>} />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
          {campaigns.map((c, i) => {
            const targetMembers = c.isMembershipCampaign ? Math.max(1, Math.round(c.targetAmount / c.amountPerMember)) : null;
            const pct = c.isMembershipCampaign
              ? Math.round((c.paidCount / (targetMembers ?? 1)) * 100)
              : c.targetAmount > 0
                ? Math.round((c.collectedAmount / c.targetAmount) * 100)
                : 0;
            return (
              <Card
                key={c.id}
                className="group relative overflow-hidden flex flex-col border-border/40 hover:border-primary/30 hover:shadow-2xl hover:shadow-primary/5 transition-all duration-500 animate-in fade-in slide-in-from-bottom-6 duration-700"
                style={{ animationDelay: `${i * 100}ms` }}
              >
                {/* Image / Gradient Banner */}
                {c.bannerImageUrl ? (
                  <div className="relative h-44 overflow-hidden">
                    <img
                      src={c.bannerImageUrl}
                      alt={c.title}
                      className="w-full h-full object-cover transition-transform duration-1000 group-hover:scale-105"
                      loading="lazy"
                    />
                    <div className="absolute inset-0 bg-gradient-to-t from-black/50 via-transparent to-transparent" />
                    <div className="absolute top-3 left-3">
                      <Badge variant={statusVariant[c.status]} className="font-bold uppercase tracking-widest text-[9px] backdrop-blur-sm border-white/20">
                        {c.status}
                      </Badge>
                    </div>
                    <div className="absolute bottom-3 right-3 bg-black/30 backdrop-blur-md px-2.5 py-1 rounded-full border border-white/10">
                      <span className="text-white text-[11px] font-black">{pct}%</span>
                    </div>
                  </div>
                ) : (
                  <div className="relative h-32 bg-gradient-to-br from-primary/15 via-primary/5 to-muted/30 flex items-center justify-center overflow-hidden">
                    <span className="text-[80px] font-black text-primary/10 select-none leading-none">
                      {c.title.charAt(0)}
                    </span>
                    <div className="absolute top-3 left-3">
                      <Badge variant={statusVariant[c.status]} className="font-bold uppercase tracking-widest text-[9px]">
                        {c.status}
                      </Badge>
                    </div>
                  </div>
                )}

                <CardContent className="p-5 flex flex-col flex-1 space-y-4">
                  {/* Title + Description */}
                  <div>
                    <h3 className="font-bold text-[14px] leading-snug line-clamp-2 group-hover:text-primary transition-colors mb-1">
                      {c.title}
                    </h3>
                    <div className="flex flex-wrap gap-2 mb-2">
                      {c.yearGroups && c.yearGroups.length > 0 ? (
                        <Badge variant="secondary" className="text-[9px] font-bold uppercase tracking-widest">
                          {c.yearGroups.length === 1
                            ? `Class of ${c.yearGroups[0]}`
                            : `Classes of ${c.yearGroups.slice(0, 2).join(", ")}${c.yearGroups.length > 2 ? "…" : ""}`}
                        </Badge>
                      ) : (
                        <Badge variant="outline" className="text-[9px] font-bold uppercase tracking-widest">
                          All years
                        </Badge>
                      )}
                      {c.isMembershipCampaign && (
                        <Badge variant="outline" className="text-[9px] font-bold uppercase tracking-widest border-green-500 text-green-700">
                          Membership Campaign
                        </Badge>
                      )}
                    </div>
                    {c.isMembershipCampaign && (
                      <div className="mt-2 text-xs text-primary font-semibold">
                        Membership year: {c.membershipYear ?? new Date().getFullYear()}
                      </div>
                    )}
                    {c.description && (
                      <p className="text-[12px] text-muted-foreground line-clamp-2 leading-relaxed">{c.description}</p>
                    )}
                  </div>

                  {/* Progress Bar */}
                  <div className="space-y-1.5 min-w-0">
                    {c.isMembershipCampaign ? (
                      <div className="flex items-start justify-between gap-2 text-[10px] font-bold uppercase tracking-wider min-w-0">
                        <span className="text-primary min-w-0 break-words">Paid members: {c.paidCount}</span>
                        <span className="text-muted-foreground/60 shrink-0 whitespace-nowrap text-right">Target members: {Math.max(1, Math.round(c.targetAmount / c.amountPerMember))}</span>
                      </div>
                    ) : (
                      <div className="flex items-start justify-between gap-2 text-[10px] font-bold uppercase tracking-wider min-w-0">
                        <span className="text-primary min-w-0 break-words">{formatCurrency(c.collectedAmount)} raised</span>
                        <span className="text-muted-foreground/60 shrink-0 whitespace-nowrap text-right">Goal: {formatCurrency(c.targetAmount)}</span>
                      </div>
                    )}
                    <div className="h-1.5 w-full bg-primary/10 rounded-full overflow-hidden">
                      <div
                        className="h-full bg-primary rounded-full relative transition-all duration-1000"
                        style={{ width: `${Math.min(pct, 100)}%` }}
                      >
                        <div className="absolute inset-0 bg-[linear-gradient(45deg,rgba(255,255,255,0.2)_25%,transparent_25%,transparent_50%,rgba(255,255,255,0.2)_50%,rgba(255,255,255,0.2)_75%,transparent_75%,transparent)] bg-[length:16px_16px] animate-shimmer" />
                      </div>
                    </div>
                  </div>

                  {/* Stats Grid */}
                  <div className="grid grid-cols-2 gap-3 py-3 border-y border-border/40">
                    <div>
                      <p className="text-[9px] font-black uppercase tracking-widest text-muted-foreground/50 mb-0.5">Members Paid</p>
                      <p className="text-sm font-black text-foreground">{c.paidCount}</p>
                    </div>
                    <div>
                      <p className="text-[9px] font-black uppercase tracking-widest text-muted-foreground/50 mb-0.5">Deadline</p>
                      <p className="text-sm font-black text-foreground">{formatDate(c.deadline)}</p>
                    </div>
                  </div>

                  {/* Actions */}
                  <div className="mt-auto flex flex-wrap gap-2 min-w-0">
                    <Link href={`/campaigns/${c.id}`} className="basis-full">
                      <Button size="sm" variant="outline" className="w-full h-9 font-bold border-border/60 hover:border-primary/30 hover:text-primary hover:bg-primary/5 transition-all">
                        <Eye size={13} />View Details
                      </Button>
                    </Link>
                    {c.status === "Active" && (
                      <Button size="sm" variant="ghost" className="h-9 w-9 p-0 rounded-xl hover:bg-primary/10 hover:text-primary" onClick={() => setEditCampaign(c)}>
                        <Pencil size={14} />
                      </Button>
                    )}
                    {c.status === "Active" && (
                      <Button size="sm" variant="ghost" className="h-9 px-3 font-bold text-muted-foreground hover:bg-muted hover:text-foreground" onClick={() => setCloseTarget(c)}>
                        Close
                      </Button>
                    )}
                    {c.status !== "Active" && c.status !== "Archived" && (
                      <Button size="sm" variant="ghost" className="h-9 px-3 font-bold text-amber-600 hover:text-amber-700 hover:bg-amber-50 dark:hover:bg-amber-500/10" onClick={() => setArchiveTarget(c)}>
                        <Archive size={13} />Archive
                      </Button>
                    )}
                    {c.status === "Completed" && (
                      <Button size="sm" variant="ghost" className="h-9 px-3 font-bold text-primary hover:bg-primary/10 hover:text-primary" onClick={() => activateMut.mutate(c.id)}>
                        <Plus size={13} />Re-open
                      </Button>
                    )}
                    {c.status === "Archived" && (
                      <Button size="sm" variant="ghost" className="h-9 px-3 font-bold text-primary hover:bg-primary/10 hover:text-primary" onClick={() => setUnarchiveTarget(c)}>
                        <ArchiveRestore size={13} />Restore
                      </Button>
                    )}
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      <ConfirmModal
        open={!!closeTarget}
        title="Close Campaign"
        message={`Close "${closeTarget?.title}"? This is irreversible: once closed, the campaign cannot be reopened. Please confirm that you want to finalize and disburse Paystack contributions now.`}
        confirmLabel="Close Campaign"
        variant="destructive"
        isLoading={closeMut.isPending}
        onConfirm={() => closeTarget && closeMut.mutate(closeTarget.id)}
        onCancel={() => setCloseTarget(null)}
      />
      <ConfirmModal
        open={!!archiveTarget}
        title="Archive Campaign"
        message={`Archive "${archiveTarget?.title}"? It will be hidden from members but can be restored later.`}
        confirmLabel="Archive"
        variant="default"
        isLoading={archiveMut.isPending}
        onConfirm={() => archiveTarget && archiveMut.mutate(archiveTarget.id)}
        onCancel={() => setArchiveTarget(null)}
      />
      <ConfirmModal
        open={!!unarchiveTarget}
        title="Restore Campaign"
        message={`Restore "${unarchiveTarget?.title}" from archive? It will be set back to Closed.`}
        confirmLabel="Restore"
        variant="default"
        isLoading={unarchiveMut.isPending}
        onConfirm={() => unarchiveTarget && unarchiveMut.mutate(unarchiveTarget.id)}
        onCancel={() => setUnarchiveTarget(null)}
      />
    </div>
  );
}
