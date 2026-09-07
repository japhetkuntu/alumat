"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Eye, Pencil, Archive, ArchiveRestore } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import Link from "next/link";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { formatCurrency, formatDate, cn } from "@alumni/ui";
import { getCampaigns, createCampaign, updateCampaign, archiveCampaign, unarchiveCampaign, activateCampaign, getCommunities } from "@/lib/institution-api";
import { useAuth } from "@/hooks/use-auth";
import { useFeatureEnabled } from "@/hooks/use-institution-features";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import type { Campaign, CampaignStatus } from "@/types";
import { ImageUpload } from "@alumni/ui";
import { YouTubePreview } from "@alumni/ui";
import { AudienceScopePicker, inferAudienceMode, type AudienceMode } from "@alumni/ui";

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
  audienceMode: AudienceMode;
  yearGroups: number[];
  bannerImage: File | null;
  existingBannerUrl: string;
  youtubeVideoUrl: string;
  allowManualPayments: boolean;
  bankAccountNumber: string;
  bankAccountName: string;
  bankName: string;
  bankBranch: string;
  mobileMoneyNumber: string;
  mobileMoneyName: string;
  mobileMoneyProvider: string;
  communityId: string;
}
const emptyForm: FormState = {
  title: "", description: "", targetAmount: "", minContribution: "", deadline: "",
  audienceMode: "everyone", yearGroups: [],
  bannerImage: null, existingBannerUrl: "", youtubeVideoUrl: "",
  allowManualPayments: false,
  bankAccountNumber: "",
  bankAccountName: "",
  bankName: "",
  bankBranch: "",
  mobileMoneyNumber: "",
  mobileMoneyName: "",
  mobileMoneyProvider: "",
  communityId: "",
};

function CampaignForm({ init, onSave, onCancel, saving, title, isSuperAdmin }: { init: FormState; onSave: (f: FormState) => void; onCancel: () => void; saving: boolean; title: string; isSuperAdmin: boolean }) {
  const [form, setForm] = useState(init);
  const f = <K extends keyof FormState>(k: K, v: FormState[K]) => setForm(prev => ({ ...prev, [k]: v }));
  const { data: communities = [] } = useQuery({ queryKey: ["communities"], queryFn: getCommunities });
  const manualPaymentsEnabled = useFeatureEnabled("ManualPayments");
  return (
    <Card>
      <CardHeader><CardTitle className="text-base">{title}</CardTitle></CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); onSave(form); }}>
          <div className="space-y-2">
            <Label>Fundraiser title</Label>
            <Input placeholder="e.g. Alumni Development Fund 2026" value={form.title} onChange={(e) => f("title", e.target.value)} required />
          </div>
          <div className="space-y-2">
            <Label>Description</Label>
            <Textarea placeholder="Describe the purpose of this fundraiser..." rows={3} value={form.description} onChange={(e) => f("description", e.target.value)} />
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

          <AudienceScopePicker
            mode={form.audienceMode}
            onModeChange={(mode) => f("audienceMode", mode)}
            communityId={form.communityId}
            onCommunityChange={(v) => f("communityId", v)}
            communities={communities}
            yearGroups={form.yearGroups}
            onYearGroupsChange={(years) => f("yearGroups", years)}
            supportsCommunity
            restricted={!isSuperAdmin ? { reason: "Regular admins cannot choose an audience. This fundraiser will be restricted to your assigned year group or community." } : undefined}
          />

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

          {manualPaymentsEnabled && (
            <div className="space-y-2">
              <Label>Allow Manual Payments</Label>
              <div className="flex items-center gap-2">
                <input id="manual-pay" type="checkbox" checked={form.allowManualPayments} onChange={(e) => f("allowManualPayments", e.target.checked)} className="h-4 w-4" />
                <label htmlFor="manual-pay" className="text-sm">Allow bank/mobile money transfers</label>
              </div>
            </div>
          )}

          {manualPaymentsEnabled && form.allowManualPayments && (
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
      communityId: isSuperAdmin && f.audienceMode === "community" ? f.communityId || undefined : undefined,
      title: f.title, description: f.description,
      targetAmount: Number(f.targetAmount), amountPerMember: Number(f.minContribution),
      deadline: f.deadline,
      yearGroups: isSuperAdmin && f.audienceMode === "yearGroups" ? f.yearGroups : undefined,
      bannerImage: f.bannerImage || undefined,
      youtubeVideoUrl: f.youtubeVideoUrl || undefined,
      allowManualPayments: f.allowManualPayments,
      bankAccountNumber: f.bankAccountNumber || undefined,
      bankAccountName: f.bankAccountName || undefined,
      bankName: f.bankName || undefined,
      bankBranch: f.bankBranch || undefined,
      mobileMoneyNumber: f.mobileMoneyNumber || undefined,
      mobileMoneyName: f.mobileMoneyName || undefined,
      mobileMoneyProvider: f.mobileMoneyProvider || undefined,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-campaigns"] }); setShowCreate(false); toast.success("Fundraiser created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, f, status }: { id: string; f: FormState; status: string }) => updateCampaign(id, {
      communityId: isSuperAdmin && f.audienceMode === "community" ? f.communityId || undefined : undefined,
      title: f.title, description: f.description || undefined, deadline: f.deadline, status,
      targetAmount: Number(f.targetAmount), amountPerMember: Number(f.minContribution),
      yearGroups: isSuperAdmin && f.audienceMode === "yearGroups" ? f.yearGroups : undefined,
      bannerImage: f.bannerImage || undefined, youtubeVideoUrl: f.youtubeVideoUrl || undefined,
      allowManualPayments: f.allowManualPayments,
      bankAccountNumber: f.bankAccountNumber || undefined,
      bankAccountName: f.bankAccountName || undefined,
      bankName: f.bankName || undefined,
      bankBranch: f.bankBranch || undefined,
      mobileMoneyNumber: f.mobileMoneyNumber || undefined,
      mobileMoneyName: f.mobileMoneyName || undefined,
      mobileMoneyProvider: f.mobileMoneyProvider || undefined,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-campaigns"] }); setEditCampaign(null); toast.success("Fundraiser updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const closeMut = useMutation({
    mutationFn: (id: string) => updateCampaign(id, { status: "Closed" }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-campaigns"] }); setCloseTarget(null); toast.success("Fundraiser closed"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const archiveMut = useMutation({
    mutationFn: (id: string) => archiveCampaign(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-campaigns"] }); setArchiveTarget(null); toast.success("Fundraiser archived"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const unarchiveMut = useMutation({
    mutationFn: (id: string) => unarchiveCampaign(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-campaigns"] }); setUnarchiveTarget(null); toast.success("Fundraiser restored"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const activateMut = useMutation({
    mutationFn: (id: string) => activateCampaign(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-campaigns"] }); toast.success("Fundraiser activated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const campaigns = (data?.results ?? []).filter((c) => !c.isMembershipCampaign);
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-4">
      <header className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Fundraisers</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">Initiatives outside standard membership dues.</p>
        </div>
        <Button onClick={() => setShowCreate(!showCreate)}>
          <Plus size={16} />Create fundraiser
        </Button>
      </header>

      {showCreate && (
        <CampaignForm
          title="Create New Fundraiser"
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
            audienceMode: inferAudienceMode(editCampaign.communityId, editCampaign.yearGroups),
            yearGroups: editCampaign.yearGroups ?? [],
            bannerImage: null,
            existingBannerUrl: editCampaign.bannerImageUrl ?? "",
            youtubeVideoUrl: editCampaign.youtubeVideoUrl ?? "",
            allowManualPayments: editCampaign.allowManualPayments,
            bankAccountNumber: editCampaign.bankAccount?.accountNumber ?? "",
            bankAccountName: editCampaign.bankAccount?.accountName ?? "",
            bankName: editCampaign.bankAccount?.bankName ?? "",
            bankBranch: editCampaign.bankAccount?.branch ?? "",
            mobileMoneyNumber: editCampaign.mobileMoneyAccount?.mobileMoneyNumber ?? "",
            mobileMoneyName: editCampaign.mobileMoneyAccount?.name ?? "",
            mobileMoneyProvider: editCampaign.mobileMoneyAccount?.provider ?? "",
            communityId: editCampaign.communityId ?? "",
          }}
          saving={updateMut.isPending}
          isSuperAdmin={isSuperAdmin}
          onSave={(f) => updateMut.mutate({ id: editCampaign.id, f, status: editCampaign.status })}
          onCancel={() => setEditCampaign(null)}
        />
      )}

      {/* Status Filter — flat pill row matching the design */}
      <div className="flex items-center gap-2 flex-wrap">
        {["", "Active", "Draft", "Closed", "Completed", "Archived"].map((s) => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setPage(1); }}
            className={cn(
              "px-3 py-1.5 border text-[12.5px] font-semibold transition-colors",
              statusFilter === s ? "bg-primary/10 text-primary border-blue-300" : "bg-white text-foreground border-border hover:bg-muted"
            )}
          >
            {s === "" ? "All" : s}
          </button>
        ))}
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : campaigns.length === 0 ? (
        <EmptyState icon={<Plus size={40} />} title="No fundraisers yet" description="Create a fundraiser to start collecting funds." action={<Button onClick={() => setShowCreate(true)}><Plus size={14} />Create fundraiser</Button>} />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {campaigns.map((c) => {
            const targetMembers = c.isMembershipCampaign ? Math.max(1, Math.round(c.targetAmount / c.amountPerMember)) : null;
            const pct = c.isMembershipCampaign
              ? Math.round((c.paidCount / (targetMembers ?? 1)) * 100)
              : c.targetAmount > 0
                ? Math.round((c.collectedAmount / c.targetAmount) * 100)
                : 0;
            return (
              <Card key={c.id} className="flex flex-col">
                <CardContent className="p-[18px] flex flex-col flex-1 gap-3">
                  <div className="flex items-start justify-between gap-2">
                    <Badge variant={statusVariant[c.status]}>{c.status}</Badge>
                  </div>
                  <div>
                    <h3 className="font-semibold text-[14px] leading-snug line-clamp-2">{c.title}</h3>
                    <p className="text-[12.5px] text-muted-foreground mt-1">
                      {c.yearGroups && c.yearGroups.length > 0
                        ? (c.yearGroups.length === 1 ? `Class of ${c.yearGroups[0]}` : `Classes ${c.yearGroups.slice(0, 2).join(", ")}${c.yearGroups.length > 2 ? "…" : ""}`)
                        : "All members"}
                      {" · "}
                      {c.allowManualPayments ? "Online + manual payments" : "Online payments"}
                    </p>
                  </div>

                  <Progress value={Math.min(pct, 100)} className="h-2" />
                  <div className="flex items-center justify-between text-[13px] tabular-nums">
                    <b>{c.isMembershipCampaign ? `${c.paidCount} paid` : `${formatCurrency(c.collectedAmount)} collected`}</b>
                    <span className="text-muted-foreground">{c.isMembershipCampaign ? `${targetMembers} target` : `${formatCurrency(c.targetAmount)} target`}</span>
                  </div>

                  <p className="text-[12px] text-muted-foreground border-t border-border pt-3 mt-1">
                    Deadline {formatDate(c.deadline)} &middot; {c.paidCount} contributors
                  </p>

                  <div className="mt-auto flex flex-wrap gap-2">
                    <Link href={`/campaigns/${c.id}`} className="flex-1">
                      <Button variant="outline" size="sm" className="w-full">
                        <Eye size={13} />View details
                      </Button>
                    </Link>
                    {c.status === "Active" && (
                      <Button size="sm" variant="outline" onClick={() => setEditCampaign(c)}>
                        <Pencil size={13} />Edit
                      </Button>
                    )}
                    {c.status === "Active" && (
                      <Button size="sm" variant="outline" onClick={() => setCloseTarget(c)}>Close</Button>
                    )}
                    {c.status !== "Active" && c.status !== "Archived" && (
                      <Button size="sm" variant="outline" onClick={() => setArchiveTarget(c)}>
                        <Archive size={13} />Archive
                      </Button>
                    )}
                    {c.status === "Completed" && (
                      <Button size="sm" variant="outline" onClick={() => activateMut.mutate(c.id)}>
                        <Plus size={13} />Re-open
                      </Button>
                    )}
                    {c.status === "Archived" && (
                      <Button size="sm" variant="outline" onClick={() => setUnarchiveTarget(c)}>
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
        title="Close Fundraiser"
        message={`Close "${closeTarget?.title}"? This is irreversible: once closed, the fundraiser cannot be reopened. Please confirm that you want to finalize and disburse online payment contributions now.`}
        confirmLabel="Close Fundraiser"
        variant="destructive"
        isLoading={closeMut.isPending}
        onConfirm={() => closeTarget && closeMut.mutate(closeTarget.id)}
        onCancel={() => setCloseTarget(null)}
      />
      <ConfirmModal
        open={!!archiveTarget}
        title="Archive Fundraiser"
        message={`Archive "${archiveTarget?.title}"? It will be hidden from members but can be restored later.`}
        confirmLabel="Archive"
        variant="default"
        isLoading={archiveMut.isPending}
        onConfirm={() => archiveTarget && archiveMut.mutate(archiveTarget.id)}
        onCancel={() => setArchiveTarget(null)}
      />
      <ConfirmModal
        open={!!unarchiveTarget}
        title="Restore Fundraiser"
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
