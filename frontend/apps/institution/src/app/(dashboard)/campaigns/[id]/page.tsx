"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, XCircle, X, Expand, Pencil, ChevronRight, Trash2, Megaphone } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { useState, useEffect } from "react";
import { useAuth } from "@/hooks/use-auth";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { formatCurrency, formatDate, cn } from "@alumni/ui";
import { useFeatureEnabled } from "@/hooks/use-institution-features";
import {
  getCampaign, getCampaignPaystackSummary, getContributions, confirmContribution, rejectContribution, markCampaignPaystackDisbursed, updateCampaign, paymentMethodLabel,
  getCampaignUpdates, createCampaignUpdate, deleteCampaignUpdate,
} from "@/lib/institution-api";
import { EmptyState } from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton, TableSkeleton } from "@alumni/ui";
import { YouTubeEmbed, YouTubePreview } from "@alumni/ui";
import { ImageUpload } from "@alumni/ui";
import { YearGroupPicker } from "@alumni/ui";
import type { ContributionStatus } from "@/types";

const contribStatusVariant: Record<ContributionStatus, "success" | "warning" | "destructive"> = {
  Successful: "success",
  Pending: "warning",
  Rejected: "destructive",
};

export default function CampaignDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [page, setPage] = useState(1);
  const [lightboxOpen, setLightboxOpen] = useState(false);
  const [confirmTarget, setConfirmTarget] = useState<string | null>(null);
  const [rejectTarget, setRejectTarget] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const pageSize = 20;
  const qc = useQueryClient();
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";

  useEffect(() => {
    if (!lightboxOpen) return;
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") setLightboxOpen(false);
    }
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [lightboxOpen]);

  const { data: campaign, isLoading: loadingCampaign } = useQuery({
    queryKey: ["admin-campaign", id],
    queryFn: () => getCampaign(id),
  });

  const { data: paystackSummary, isLoading: loadingPaystackSummary } = useQuery({
    queryKey: ["admin-campaign-paystack-summary", id],
    queryFn: () => getCampaignPaystackSummary(id),
    enabled: !!id,
  });

  const { data: contribs, isLoading: loadingContribs } = useQuery({
    queryKey: ["admin-contributions", id, page],
    queryFn: () => getContributions({ campaignId: id, page, pageSize }),
  });

  const confirmMut = useMutation({
    mutationFn: (cid: string) => confirmContribution(cid),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-contributions", id] }); setConfirmTarget(null); toast.success("Contribution confirmed!"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const [confirmCampaignDisburseOpen, setConfirmCampaignDisburseOpen] = useState(false);

  const rejectMut = useMutation({
    mutationFn: (cid: string) => rejectContribution(cid),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-contributions", id] }); setRejectTarget(null); toast.success("Contribution rejected."); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const campaignDisburseMut = useMutation({
    mutationFn: () => markCampaignPaystackDisbursed(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-campaign", id] });
      qc.invalidateQueries({ queryKey: ["admin-contributions", id] });
      qc.invalidateQueries({ queryKey: ["admin-campaign-paystack-summary", id] });
      setConfirmCampaignDisburseOpen(false);
      toast.success(`${campaign?.isMembershipCampaign ? "Membership dues" : "Fundraiser"} online payment contributions marked as disbursed.`);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: (body: Parameters<typeof updateCampaign>[1]) => updateCampaign(id, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-campaign", id] });
      qc.invalidateQueries({ queryKey: ["admin-campaigns"] });
      setEditing(false);
      toast.success(`${campaign?.isMembershipCampaign ? "Membership dues" : "Fundraiser"} updated`);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (loadingCampaign) return <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-6 page-enter"><CardSkeleton /><CardSkeleton /></div>;
  if (!campaign) return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto">
      <Link href="/campaigns">
        <Button size="sm" variant="ghost" className="mb-6"><ArrowLeft size={14} />Back</Button>
      </Link>
      <EmptyState icon={<XCircle size={48} />} title="Not found" description="This may have been removed or the link is incorrect." />
    </div>
  );

  const pct = campaign.targetAmount > 0 ? Math.round((campaign.collectedAmount / campaign.targetAmount) * 100) : 0;
  const contributions = contribs?.results ?? [];
  const totalPages = contribs?.totalPages ?? 1;
  const backLink = campaign.isMembershipCampaign ? "/membership" : "/campaigns";

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-4">
      <nav className="flex items-center gap-1.5 text-sm">
        <Link href={backLink}>
          <Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg font-bold group">
            <ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />
            {campaign.isMembershipCampaign ? "Dues" : "Fundraisers"}
          </Button>
        </Link>
        <ChevronRight size={14} className="text-muted-foreground/50" />
        <span className="text-[13px] font-semibold text-foreground/70 truncate max-w-[200px] sm:max-w-xs">{campaign.title}</span>
      </nav>
      <div className="flex items-start justify-between gap-3">
        <div>
          <h1 className="text-[26px] font-bold m-0 flex items-center gap-2">
            {campaign.title}
            <Badge variant={campaign.status === "Active" ? "success" : "secondary"}>{campaign.status}</Badge>
          </h1>
        </div>
        {campaign.status === "Active" && (
          <Button variant="outline" onClick={() => setEditing(!editing)}>
            <Pencil size={14} />{editing ? "Cancel edit" : "Edit"}
          </Button>
        )}
      </div>

      {editing && <CampaignEditForm campaign={campaign} isSuperAdmin={isSuperAdmin} saving={updateMut.isPending} onSave={(body) => updateMut.mutate(body)} onCancel={() => setEditing(false)} />}

      {/* Fundraiser/dues summary */}
      <Card>
        <CardContent className="p-[18px] space-y-4">
          <h2 className="text-[15px] font-semibold m-0">{campaign.isMembershipCampaign ? "Membership dues progress" : "Fundraiser progress"}</h2>
          {campaign.description && <p className="text-[13px] text-muted-foreground">{campaign.description}</p>}
          <Progress value={pct} className="h-2.5" />
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-sm">
            <div><p className="text-muted-foreground text-xs">Collected</p><p className="font-bold text-lg tabular-nums text-success">{formatCurrency(campaign.collectedAmount)}</p></div>
            <div><p className="text-muted-foreground text-xs">Target</p><p className="font-semibold tabular-nums">{formatCurrency(campaign.targetAmount)}</p></div>
            <div><p className="text-muted-foreground text-xs">Paid Members</p><p className="font-semibold tabular-nums">{campaign.paidCount}</p></div>
            <div><p className="text-muted-foreground text-xs">Deadline</p><p className="font-semibold">{formatDate(campaign.deadline)}</p></div>
          </div>
          {pct > 0 && (
            <p className="text-xs text-muted-foreground tabular-nums">{pct}% of target reached</p>
          )}
          <div className="rounded-[6px] p-3 text-[12.5px]" style={{ background: "var(--brand-accent-light)", color: "var(--brand-accent-dark)", border: "1px solid #FED7AA" }}>
            Members may contribute any positive amount (including less than the suggested base amount). They can also contribute additional payments over time to reach their target.
          </div>

          <div className="mt-1 rounded-[6px] border border-border p-4">
            <h3 className="text-[13.5px] font-semibold">Online payment overview</h3>
            <p className="text-xs text-muted-foreground mt-1">Online payments are processed securely — your institution receives the full amount members pay.</p>
            {loadingPaystackSummary ? (
              <p className="text-sm text-muted-foreground mt-2">Loading payment summary…</p>
            ) : paystackSummary ? (
              <>
                <div className="flex flex-wrap gap-4 mt-3 text-sm">
                  <div><p className="text-muted-foreground">Collected online</p><p className="font-semibold tabular-nums">{formatCurrency(paystackSummary.totalPaidToPaystack)}</p></div>
                </div>
                <div className="flex flex-wrap gap-4 mt-3 text-sm">
                  <div><p className="text-muted-foreground">Total disbursed</p><p className="font-semibold tabular-nums">{formatCurrency(paystackSummary.totalDisbursed)}</p></div>
                  <div><p className="text-muted-foreground">Outstanding</p><p className="font-semibold tabular-nums">{formatCurrency(paystackSummary.totalOutstanding)}</p></div>
                  <div><p className="text-muted-foreground">Successful contributions</p><p className="font-semibold tabular-nums">{paystackSummary.confirmedCount}</p></div>
                  <div><p className="text-muted-foreground">Disbursed count</p><p className="font-semibold tabular-nums">{paystackSummary.disbursedCount}</p></div>
                </div>
              </>
            ) : (
              <p className="text-sm text-muted-foreground mt-2">No online payment summary available.</p>
            )}

            {user?.role === "SuperAdmin" && campaign.status === "Closed" && paystackSummary && paystackSummary.totalOutstanding > 0 && (
              <div className="mt-3">
                <Button
                  variant="destructive"
                  onClick={() => setConfirmCampaignDisburseOpen(true)}
                  isLoading={campaignDisburseMut.isPending}
                >
                  Mark online payment contributions as disbursed
                </Button>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Media */}
      {(campaign.bannerImageUrl || campaign.youtubeVideoUrl) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{campaign.isMembershipCampaign ? "Membership Dues Media" : "Fundraiser Media"}</CardTitle>
          </CardHeader>
          <CardContent>
            <div className={campaign.bannerImageUrl && campaign.youtubeVideoUrl
              ? "grid grid-cols-1 md:grid-cols-2 gap-4 items-start"
              : "flex justify-center"}
            >
              {campaign.bannerImageUrl && (
                <div className="relative overflow-hidden rounded-xl bg-muted/30 group">
                  <button
                    onClick={() => setLightboxOpen(true)}
                    className="block w-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                    title="Click to expand"
                  >
                    <img
                      src={campaign.bannerImageUrl}
                      alt={campaign.title}
                      className="w-full h-56 object-cover transition-transform duration-500 group-hover:scale-[1.03] cursor-zoom-in"
                      loading="lazy"
                    />
                    <div className="absolute inset-0 bg-black/0 group-hover:bg-black/20 transition-colors flex items-center justify-center">
                      <Expand size={22} className="text-white drop-shadow-lg opacity-0 group-hover:opacity-100 transition-opacity" />
                    </div>
                  </button>
                </div>
              )}
              {campaign.youtubeVideoUrl && (
                <div className={!campaign.bannerImageUrl ? "max-w-2xl w-full" : ""}>
                  <YouTubeEmbed url={campaign.youtubeVideoUrl} />
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Lightbox */}
      {lightboxOpen && campaign.bannerImageUrl && (
        <div
          className="fixed inset-0 z-[9999] bg-black/85 backdrop-blur-sm flex items-center justify-center p-4"
          onClick={() => setLightboxOpen(false)}
          role="dialog"
          aria-modal="true"
          aria-label="Banner image"
        >
          <button
            className="absolute top-4 right-4 text-white/80 hover:text-white bg-black/30 rounded-full p-1.5 transition-colors"
            onClick={() => setLightboxOpen(false)}
            aria-label="Close lightbox"
          >
            <X size={22} aria-hidden="true" />
          </button>
          <img
            src={campaign.bannerImageUrl}
            alt={campaign.title}
            className="max-h-[90vh] max-w-[95vw] object-contain rounded-xl shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          />
        </div>
      )}

      {/* Updates — close the loop on what the money did, postable any time */}
      {!campaign.isMembershipCampaign && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2"><Megaphone size={16} className="text-primary" />Updates</CardTitle>
            <p className="text-[12.5px] text-muted-foreground">Post progress here so givers see what their money did — not just at the end.</p>
          </CardHeader>
          <CardContent className="space-y-4">
            <CampaignUpdatesSection campaignId={id} />
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-0">
          <div className="px-4 py-3 border-b border-border/50">
            <h2 className="text-base font-semibold">Contributions ({contribs?.totalCount ?? 0})</h2>
          </div>
          <Table className="min-w-[760px] sm:min-w-[920px]">
            <TableHeader>
              <TableRow>
                <TableHead>Member</TableHead>
                <TableHead>Amount</TableHead>
                <TableHead>Method</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Date</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loadingContribs ? (
                <TableSkeleton rows={5} cols={6} />
              ) : contributions.length === 0 ? (
                <TableRow><TableCell colSpan={6} className="text-center py-8 text-muted-foreground">No contributions yet</TableCell></TableRow>
              ) : contributions.map((c) => (
                <TableRow key={c.id}>
                  <TableCell className="text-sm">
                    {c.memberName ?? "Unknown"}
                    {c.memberEmail && <p className="text-xs text-muted-foreground">{c.memberEmail}</p>}
                  </TableCell>
                  <TableCell className="font-semibold tabular-nums">{formatCurrency(c.amount)}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">{paymentMethodLabel(c.paymentMethod)}</TableCell>
                  <TableCell><Badge variant={contribStatusVariant[c.status]}>{c.status}</Badge></TableCell>
                  <TableCell className="text-sm text-muted-foreground">{formatDate(c.createdAt)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      <ConfirmModal
        open={!!confirmTarget}
        title="Confirm Contribution"
        message="Mark this contribution as confirmed?"
        confirmLabel="Confirm"
        variant="default"
        isLoading={confirmMut.isPending}
        onConfirm={() => confirmTarget && confirmMut.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
      <ConfirmModal
        open={!!rejectTarget}
        title="Reject Contribution"
        message="Reject this contribution? This action will be visible to the member."
        confirmLabel="Reject"
        variant="destructive"
        isLoading={rejectMut.isPending}
        onConfirm={() => rejectTarget && rejectMut.mutate(rejectTarget)}
        onCancel={() => setRejectTarget(null)}
      />
      <ConfirmModal
        open={confirmCampaignDisburseOpen}
        title="Mark online payments as disbursed"
        message="This will mark all confirmed online payment contributions for this fundraiser as disbursed. Continue?"
        confirmLabel="Mark as disbursed"
        variant="destructive"
        isLoading={campaignDisburseMut.isPending}
        onConfirm={() => campaignDisburseMut.mutate()}
        onCancel={() => setConfirmCampaignDisburseOpen(false)}
      />
    </div>
  );
}

/* ─── Inline Edit Form ──────────────────────────────────────────────────────── */

import type { Campaign } from "@/types";
import type { UpdateCampaignBody } from "@/lib/institution-api";

function CampaignEditForm({ campaign, isSuperAdmin, saving, onSave, onCancel }: {
  campaign: Campaign; isSuperAdmin: boolean; saving: boolean;
  onSave: (body: UpdateCampaignBody) => void; onCancel: () => void;
}) {
  const manualPaymentsEnabled = useFeatureEnabled("ManualPayments");
  const [title, setTitle] = useState(campaign.title);
  const [description, setDescription] = useState(campaign.description ?? "");
  const [targetAmount, setTargetAmount] = useState(String(campaign.targetAmount));
  const [amountPerMember, setAmountPerMember] = useState(String(campaign.amountPerMember));
  const [pensionerAmountPerMember, setPensionerAmountPerMember] = useState(String(campaign.pensionerAmountPerMember ?? ""));
  const [deadline, setDeadline] = useState(campaign.deadline.split("T")[0]);
  const [yearGroupsAll, setYearGroupsAll] = useState(!campaign.yearGroups || campaign.yearGroups.length === 0);
  const [yearGroups, setYearGroups] = useState(campaign.yearGroups ?? []);
  const [bannerImage, setBannerImage] = useState<File | null>(null);
  const [existingBannerUrl, setExistingBannerUrl] = useState(campaign.bannerImageUrl ?? "");
  const [youtubeVideoUrl, setYoutubeVideoUrl] = useState(campaign.youtubeVideoUrl ?? "");
  const [allowManualPayments, setAllowManualPayments] = useState(campaign.allowManualPayments);
  const [isMembershipCampaign, setIsMembershipCampaign] = useState(campaign.isMembershipCampaign ?? false);
  const [membershipYear, setMembershipYear] = useState(campaign.membershipYear ?? new Date().getFullYear());
  const [bankAccountNumber, setBankAccountNumber] = useState(campaign.bankAccount?.accountNumber ?? "");
  const [bankAccountName, setBankAccountName] = useState(campaign.bankAccount?.accountName ?? "");
  const [bankName, setBankName] = useState(campaign.bankAccount?.bankName ?? "");
  const [bankBranch, setBankBranch] = useState(campaign.bankAccount?.branch ?? "");
  const [mobileMoneyNumber, setMobileMoneyNumber] = useState(campaign.mobileMoneyAccount?.mobileMoneyNumber ?? "");
  const [mobileMoneyName, setMobileMoneyName] = useState(campaign.mobileMoneyAccount?.name ?? "");
  const [mobileMoneyProvider, setMobileMoneyProvider] = useState(campaign.mobileMoneyAccount?.provider ?? "");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSave({
      title, description: description || undefined, deadline, status: campaign.status,
      targetAmount: Number(targetAmount), amountPerMember: Number(amountPerMember),
      pensionerAmountPerMember: isMembershipCampaign && pensionerAmountPerMember ? Number(pensionerAmountPerMember) : undefined,
      yearGroups: isSuperAdmin ? (yearGroupsAll ? undefined : yearGroups) : undefined,
      bannerImage: bannerImage || undefined, youtubeVideoUrl: youtubeVideoUrl || undefined,
      allowManualPayments,
      isMembershipCampaign, membershipYear,
      bankAccountNumber: bankAccountNumber || undefined,
      bankAccountName: bankAccountName || undefined,
      bankName: bankName || undefined,
      bankBranch: bankBranch || undefined,
      mobileMoneyNumber: mobileMoneyNumber || undefined,
      mobileMoneyName: mobileMoneyName || undefined,
      mobileMoneyProvider: mobileMoneyProvider || undefined,
    });
  };

  const currentYear = new Date().getFullYear();

  return (
    <Card className="animate-in fade-in slide-in-from-top-4 duration-500 border-primary/30">
      <CardHeader><CardTitle className="text-base">{isMembershipCampaign ? "Edit Membership Dues" : "Edit Fundraiser"}</CardTitle></CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {isMembershipCampaign ? (
            /* ── Membership dues layout (matches create form) ── */
            <>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Title</Label>
                  <Input placeholder="e.g. Dues 2026" value={title} onChange={(e) => setTitle(e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Dues Year</Label>
                  <Input type="number" value={membershipYear} onChange={(e) => { const y = Number(e.target.value); setMembershipYear(y); setTitle(`Dues ${y}`); }} required />
                  <p className="text-xs text-muted-foreground">
                    {membershipYear === currentYear
                      ? "Current year — members must pay this to stay active."
                      : membershipYear > currentYear
                        ? "Future year — optional early payment, does not affect active status."
                        : "Past year — for members who haven't paid for previous years."}
                  </p>
                </div>
              </div>
              <div className="space-y-2">
                <Label>Description (optional)</Label>
                <Textarea placeholder="Describe the purpose of this dues period..." rows={2} value={description} onChange={(e) => setDescription(e.target.value)} />
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label>Amount for employed members (GHS)</Label>
                  <Input type="number" min={1} step="0.01" placeholder="e.g. 100" value={amountPerMember} onChange={(e) => setAmountPerMember(e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Amount for pensioners (GHS)</Label>
                  <Input type="number" min={1} step="0.01" placeholder="e.g. 50" value={pensionerAmountPerMember} onChange={(e) => setPensionerAmountPerMember(e.target.value)} />
                  <p className="text-xs text-muted-foreground">Leave empty to use same amount as employed members.</p>
                </div>
                <div className="space-y-2">
                  <Label>Deadline</Label>
                  <Input type="date" value={deadline} onChange={(e) => setDeadline(e.target.value)} required />
                </div>
              </div>

              <div className="space-y-2">
                <Label>Banner image (optional)</Label>
                <ImageUpload file={bannerImage} existingUrl={existingBannerUrl} onChange={setBannerImage} onClearExisting={() => setExistingBannerUrl("")} label="Upload banner image" />
              </div>

              {manualPaymentsEnabled && (
                <div className="space-y-2">
                  <Label>Allow Manual Payments</Label>
                  <div className="flex items-center gap-2">
                    <input id="edit-manual-pay-membership" type="checkbox" checked={allowManualPayments} onChange={(e) => setAllowManualPayments(e.target.checked)} className="h-4 w-4" />
                    <label htmlFor="edit-manual-pay-membership" className="text-sm">Bank/Mobile money transfers</label>
                  </div>
                </div>
              )}
            </>
          ) : (
            /* ── Regular fundraiser layout ── */
            <>
              <div className="space-y-2">
                <Label>Fundraiser title</Label>
                <Input value={title} onChange={(e) => setTitle(e.target.value)} required />
              </div>
              <div className="space-y-2">
                <Label>Description</Label>
                <Textarea rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label>Target amount (GHS)</Label>
                  <Input type="number" value={targetAmount} onChange={(e) => setTargetAmount(e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Minimum Contribution (GHS)</Label>
                  <Input type="number" value={amountPerMember} onChange={(e) => setAmountPerMember(e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Deadline</Label>
                  <Input type="date" value={deadline} onChange={(e) => setDeadline(e.target.value)} required />
                </div>
              </div>

              <div className="space-y-2">
                {isSuperAdmin ? (
                  <>
                    <div className="flex items-center justify-between">
                      <Label>Target year groups</Label>
                      <label className="flex items-center gap-2 text-sm font-medium">
                        <input type="checkbox" checked={yearGroupsAll} onChange={(e) => setYearGroupsAll(e.target.checked)} className="h-4 w-4 rounded border border-muted-foreground" />
                        All years
                      </label>
                    </div>
                    {!yearGroupsAll ? (
                      <YearGroupPicker value={yearGroups} onChange={setYearGroups} />
                    ) : (
                      <p className="text-xs text-muted-foreground">This fundraiser will be visible to members of all year groups.</p>
                    )}
                  </>
                ) : (
                  <p className="text-xs text-muted-foreground">Regular admins cannot choose year groups.</p>
                )}
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Banner image (optional)</Label>
                  <ImageUpload file={bannerImage} existingUrl={existingBannerUrl} onChange={setBannerImage} onClearExisting={() => setExistingBannerUrl("")} label="Upload banner image" />
                </div>
                <div className="space-y-2">
                  <Label>YouTube video URL (optional)</Label>
                  <Input type="url" placeholder="https://youtube.com/..." value={youtubeVideoUrl} onChange={(e) => setYoutubeVideoUrl(e.target.value)} />
                  {youtubeVideoUrl && <YouTubePreview url={youtubeVideoUrl} />}
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {manualPaymentsEnabled && (
                  <div className="space-y-2">
                    <Label>Allow Manual Payments</Label>
                    <div className="flex items-center gap-2">
                      <input id="edit-manual-pay-regular" type="checkbox" checked={allowManualPayments} onChange={(e) => setAllowManualPayments(e.target.checked)} className="h-4 w-4" />
                      <label htmlFor="edit-manual-pay-regular" className="text-sm">Bank/mobile money transfers</label>
                    </div>
                  </div>
                )}
                <div className="space-y-2">
                  <Label>Membership dues</Label>
                  {isSuperAdmin ? (
                    <div className="space-y-2">
                      <div className="flex items-center gap-2">
                        <input id="edit-membership" type="checkbox" checked={isMembershipCampaign} onChange={(e) => setIsMembershipCampaign(e.target.checked)} className="h-4 w-4" />
                        <label htmlFor="edit-membership" className="text-sm">Mark as membership dues</label>
                      </div>
                      {isMembershipCampaign && (
                        <div className="flex items-center gap-2">
                          <Label className="min-w-max">Dues Year</Label>
                          <Input type="number" value={membershipYear} onChange={(e) => setMembershipYear(Number(e.target.value))} className="w-32" />
                        </div>
                      )}
                    </div>
                  ) : (
                    <p className="text-xs text-muted-foreground">Only super admins can modify this.</p>
                  )}
                </div>
              </div>
            </>
          )}

          {manualPaymentsEnabled && allowManualPayments && (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
              <div className="p-4 border border-border rounded-lg space-y-2">
                <h4 className="text-sm font-black uppercase tracking-wider">Bank Account</h4>
                <Input placeholder="Account number" value={bankAccountNumber} onChange={(e) => setBankAccountNumber(e.target.value)} />
                <Input placeholder="Account name" value={bankAccountName} onChange={(e) => setBankAccountName(e.target.value)} />
                <Input placeholder="Bank name" value={bankName} onChange={(e) => setBankName(e.target.value)} />
                <Input placeholder="Branch" value={bankBranch} onChange={(e) => setBankBranch(e.target.value)} />
              </div>
              <div className="p-4 border border-border rounded-lg space-y-2">
                <h4 className="text-sm font-black uppercase tracking-wider">Mobile Money</h4>
                <Input placeholder="Mobile money number" value={mobileMoneyNumber} onChange={(e) => setMobileMoneyNumber(e.target.value)} />
                <Input placeholder="Account name" value={mobileMoneyName} onChange={(e) => setMobileMoneyName(e.target.value)} />
                <Input placeholder="Provider (MTN, Telecel, AT)" value={mobileMoneyProvider} onChange={(e) => setMobileMoneyProvider(e.target.value)} />
              </div>
            </div>
          )}

          <div className="flex gap-3 pt-2">
            <Button type="submit" size="sm" isLoading={saving} loadingText="Saving">Save Changes</Button>
            <Button type="button" size="sm" variant="outline" onClick={onCancel}>Cancel</Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

function CampaignUpdatesSection({ campaignId }: { campaignId: string }) {
  const qc = useQueryClient();
  const [body, setBody] = useState("");
  const [image, setImage] = useState<File | null>(null);

  const { data: updates = [], isLoading } = useQuery({
    queryKey: ["admin-campaign-updates", campaignId],
    queryFn: () => getCampaignUpdates(campaignId),
  });

  const postMut = useMutation({
    mutationFn: () => createCampaignUpdate(campaignId, { body, image: image ?? undefined }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-campaign-updates", campaignId] });
      setBody("");
      setImage(null);
      toast.success("Update posted");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const deleteMut = useMutation({
    mutationFn: (updateId: string) => deleteCampaignUpdate(campaignId, updateId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-campaign-updates", campaignId] });
      toast.success("Update deleted");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <div className="space-y-4">
      <form
        className="space-y-3"
        onSubmit={(e) => { e.preventDefault(); postMut.mutate(); }}
      >
        <Textarea
          rows={3}
          placeholder="Foundation poured, roof's up, here's the finished classroom…"
          value={body}
          onChange={(e) => setBody(e.target.value)}
          required
        />
        <div className="flex items-center gap-3 flex-wrap">
          <ImageUpload file={image} existingUrl="" onChange={setImage} onClearExisting={() => setImage(null)} label="Add a photo (optional)" />
          <Button type="submit" size="sm" isLoading={postMut.isPending} loadingText="Posting" disabled={!body.trim()}>
            Post update
          </Button>
        </div>
      </form>

      {isLoading ? (
        <p className="text-[13px] text-muted-foreground">Loading updates…</p>
      ) : updates.length === 0 ? (
        <p className="text-[13px] text-muted-foreground">No updates posted yet.</p>
      ) : (
        <div className="space-y-3">
          {updates.map((u) => (
            <div key={u.id} className="rounded-xl border border-border overflow-hidden">
              {u.imageUrl && <img src={u.imageUrl} alt="" className="w-full object-cover" style={{ maxHeight: 220 }} />}
              <div className="p-3.5 flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="text-[13.5px] whitespace-pre-wrap leading-relaxed">{u.body}</p>
                  <p className="text-[11.5px] text-muted-foreground mt-1.5">
                    {u.postedByName ? `${u.postedByName} · ` : ""}{formatDate(u.createdAt)}
                  </p>
                </div>
                <Button
                  size="sm"
                  variant="ghost"
                  className="shrink-0 text-destructive gap-1.5"
                  onClick={() => deleteMut.mutate(u.id)}
                  isLoading={deleteMut.isPending}
                >
                  <Trash2 size={13} />
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
