"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, CheckCircle, XCircle, X, Expand, Pencil } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { useState } from "react";
import { useAuth } from "@/hooks/use-auth";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { formatCurrency, formatDate } from "@/lib/utils";
import { getCampaign, getCampaignPaystackSummary, getContributions, confirmContribution, rejectContribution, markCampaignPaystackDisbursed, updateCampaign } from "@/lib/admin-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton, TableSkeleton } from "@/components/ui/skeleton";
import { YouTubeEmbed, YouTubePreview } from "@/components/ui/youtube-embed";
import { ImageUpload } from "@/components/ui/image-upload";
import { YearGroupPicker } from "@/components/ui/year-group-picker";
import type { ContributionStatus } from "@/types";

const contribStatusVariant: Record<ContributionStatus, "success" | "warning" | "destructive"> = {
  Confirmed: "success",
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
      toast.success("Campaign paystack contributions marked as disbursed.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: (body: Parameters<typeof updateCampaign>[1]) => updateCampaign(id, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-campaign", id] });
      qc.invalidateQueries({ queryKey: ["admin-campaigns"] });
      setEditing(false);
      toast.success("Campaign updated");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (loadingCampaign) return <div className="p-6 lg:p-8 space-y-6 page-enter"><CardSkeleton /><CardSkeleton /></div>;
  if (!campaign) return <div className="p-6 text-muted-foreground">Campaign not found.</div>;

  const pct = campaign.targetAmount > 0 ? Math.round((campaign.collectedAmount / campaign.targetAmount) * 100) : 0;
  const contributions = contribs?.results ?? [];
  const totalPages = contribs?.totalPages ?? 1;
  const backLink = campaign.isMembershipCampaign ? "/membership" : "/campaigns";

  return (
    <div className="p-6 lg:p-8 space-y-6 page-enter">
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <Link href={backLink}>
            <Button size="sm" variant="ghost"><ArrowLeft size={14} />Back</Button>
          </Link>
          <div>
            <h1 className="text-[22px] font-bold tracking-tight">{campaign.title}</h1>
            <p className="text-muted-foreground text-[13px]">Campaign Details &amp; Contributions</p>
          </div>
        </div>
        {campaign.status === "Active" && (
          <Button size="sm" variant="outline" onClick={() => setEditing(!editing)} className="font-bold">
            <Pencil size={14} />{editing ? "Cancel Edit" : "Edit Campaign"}
          </Button>
        )}
      </div>

      {editing && <CampaignEditForm campaign={campaign} isSuperAdmin={isSuperAdmin} saving={updateMut.isPending} onSave={(body) => updateMut.mutate(body)} onCancel={() => setEditing(false)} />}

      {/* Campaign summary */}
      <Card>
        <CardContent className="p-5 space-y-4">
          <div className="flex items-start justify-between gap-4">
            <div className="flex-1">
              {campaign.description && <p className="text-sm text-muted-foreground mb-4">{campaign.description}</p>}
              <Progress value={pct} className="h-3 mb-3" />
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-sm">
                <div><p className="text-muted-foreground text-xs">Collected</p><p className="font-bold text-green-600 text-lg">{formatCurrency(campaign.collectedAmount)}</p></div>
                <div><p className="text-muted-foreground text-xs">Target</p><p className="font-semibold">{formatCurrency(campaign.targetAmount)}</p></div>
                <div><p className="text-muted-foreground text-xs">Paid Members</p><p className="font-semibold">{campaign.paidCount}</p></div>
                <div><p className="text-muted-foreground text-xs">Deadline</p><p className="font-semibold">{formatDate(campaign.deadline)}</p></div>
              </div>
            </div>
            <Badge variant={campaign.status === "Active" ? "success" : "secondary"}>{campaign.status}</Badge>
          </div>
          {pct > 0 && (
            <p className="text-xs text-muted-foreground">{pct}% of target reached</p>
          )}
          <div className="rounded-lg border border-amber-300/30 bg-amber-100/25 p-3 text-xs text-amber-950">
            Members may contribute any positive amount (including less than the campaign suggested base amount). They can also contribute additional payments over time to reach their target.
          </div>

          <div className="mt-4 rounded-lg border border-border/50 bg-muted/20 p-4">
            <h3 className="text-sm font-semibold">Paystack contribution overview</h3>
            {loadingPaystackSummary ? (
              <p className="text-sm text-muted-foreground">Loading paystack summary…</p>
            ) : paystackSummary ? (
              <div className="flex flex-wrap gap-4 mt-2 text-sm">
                <div><p className="text-muted-foreground">Total paid via Paystack</p><p className="font-semibold">{formatCurrency(paystackSummary.totalPaidToPaystack)}</p></div>
                <div><p className="text-muted-foreground">Total disbursed</p><p className="font-semibold">{formatCurrency(paystackSummary.totalDisbursed)}</p></div>
                <div><p className="text-muted-foreground">Outstanding</p><p className="font-semibold">{formatCurrency(paystackSummary.totalOutstanding)}</p></div>
                <div><p className="text-muted-foreground">Confirmed contributions</p><p className="font-semibold">{paystackSummary.confirmedCount}</p></div>
                <div><p className="text-muted-foreground">Disbursed count</p><p className="font-semibold">{paystackSummary.disbursedCount}</p></div>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">No Paystack summary available.</p>
            )}

            {user?.role === "SuperAdmin" && campaign.status === "Closed" && paystackSummary && paystackSummary.totalOutstanding > 0 && (
              <div className="mt-3">
                <Button
                  variant="destructive"
                  onClick={() => setConfirmCampaignDisburseOpen(true)}
                  isLoading={campaignDisburseMut.isPending}
                >
                  Mark Paystack contributions as disbursed
                </Button>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Campaign Media */}
      {(campaign.bannerImageUrl || campaign.youtubeVideoUrl) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Campaign Media</CardTitle>
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
        >
          <button
            className="absolute top-4 right-4 text-white/80 hover:text-white bg-black/30 rounded-full p-1.5 transition-colors"
            onClick={() => setLightboxOpen(false)}
          >
            <X size={22} />
          </button>
          <img
            src={campaign.bannerImageUrl}
            alt={campaign.title}
            className="max-h-[90vh] max-w-[95vw] object-contain rounded-xl shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          />
        </div>
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
                  <TableCell className="font-semibold">{formatCurrency(c.amount)}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">{c.paymentMethod}</TableCell>
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
        title="Mark Paystack as disbursed"
        message="This will mark all confirmed Paystack contributions for this campaign as disbursed. Continue?"
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
import type { UpdateCampaignBody } from "@/lib/admin-api";

function CampaignEditForm({ campaign, isSuperAdmin, saving, onSave, onCancel }: {
  campaign: Campaign; isSuperAdmin: boolean; saving: boolean;
  onSave: (body: UpdateCampaignBody) => void; onCancel: () => void;
}) {
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
  const [allowOnlinePayments, setAllowOnlinePayments] = useState(campaign.allowOnlinePayments);
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
      allowOnlinePayments, allowManualPayments,
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
      <CardHeader><CardTitle className="text-base">{isMembershipCampaign ? "Edit Membership Campaign" : "Edit Campaign"}</CardTitle></CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={handleSubmit}>
          {isMembershipCampaign ? (
            /* ── Membership campaign layout (matches create form) ── */
            <>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Campaign title</Label>
                  <Input placeholder="e.g. Membership Renewal 2026" value={title} onChange={(e) => setTitle(e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Membership Year</Label>
                  <Input type="number" value={membershipYear} onChange={(e) => { const y = Number(e.target.value); setMembershipYear(y); setTitle(`Membership Renewal ${y}`); }} required />
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
                <Textarea placeholder="Describe the purpose of this membership renewal..." rows={2} value={description} onChange={(e) => setDescription(e.target.value)} />
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

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Allow Online Payments</Label>
                  <div className="flex items-center gap-2">
                    <input id="edit-online-pay" type="checkbox" checked={allowOnlinePayments} onChange={(e) => setAllowOnlinePayments(e.target.checked)} className="h-4 w-4" />
                    <label htmlFor="edit-online-pay" className="text-sm">Enable Paystack</label>
                  </div>
                </div>
                <div className="space-y-2">
                  <Label>Allow Manual Payments</Label>
                  <div className="flex items-center gap-2">
                    <input id="edit-manual-pay" type="checkbox" checked={allowManualPayments} onChange={(e) => setAllowManualPayments(e.target.checked)} className="h-4 w-4" />
                    <label htmlFor="edit-manual-pay" className="text-sm">Bank/Mobile money transfers</label>
                  </div>
                </div>
              </div>
            </>
          ) : (
            /* ── Regular campaign layout ── */
            <>
              <div className="space-y-2">
                <Label>Campaign title</Label>
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
                      <p className="text-xs text-muted-foreground">This campaign will be visible to members of all year groups.</p>
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

              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label>Allow Online Payments</Label>
                  <div className="flex items-center gap-2">
                    <input id="edit-online-pay" type="checkbox" checked={allowOnlinePayments} onChange={(e) => setAllowOnlinePayments(e.target.checked)} className="h-4 w-4" />
                    <label htmlFor="edit-online-pay" className="text-sm">Enable Paystack</label>
                  </div>
                </div>
                <div className="space-y-2">
                  <Label>Allow Manual Payments</Label>
                  <div className="flex items-center gap-2">
                    <input id="edit-manual-pay" type="checkbox" checked={allowManualPayments} onChange={(e) => setAllowManualPayments(e.target.checked)} className="h-4 w-4" />
                    <label htmlFor="edit-manual-pay" className="text-sm">Bank/mobile money transfers</label>
                  </div>
                </div>
                <div className="space-y-2">
                  <Label>Membership Campaign</Label>
                  {isSuperAdmin ? (
                    <div className="space-y-2">
                      <div className="flex items-center gap-2">
                        <input id="edit-membership" type="checkbox" checked={isMembershipCampaign} onChange={(e) => setIsMembershipCampaign(e.target.checked)} className="h-4 w-4" />
                        <label htmlFor="edit-membership" className="text-sm">Mark as membership campaign</label>
                      </div>
                      {isMembershipCampaign && (
                        <div className="flex items-center gap-2">
                          <Label className="min-w-max">Membership Year</Label>
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

          {allowManualPayments && (
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
