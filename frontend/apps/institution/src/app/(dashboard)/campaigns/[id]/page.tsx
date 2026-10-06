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
import { Card, CardContent, CardHeader, CardTitle, Checkbox } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { formatCurrency, formatDate, cn } from "@alumni/ui";
import { useFeatureEnabled } from "@/hooks/use-institution-features";
import { PledgesPanel } from "@/components/institution/pledges-panel";
import { PublicPagePanel } from "@/components/institution/public-page-panel";
import {
  getCampaign, getCampaignPaystackSummary, getContributions, confirmContribution, rejectContribution, markCampaignPaystackDisbursed, updateCampaign, paymentMethodLabel,
  getCampaignUpdates, createCampaignUpdate, deleteCampaignUpdate, getInstitutionProfile,
} from "@/lib/institution-api";
import { buildMemberPortalShareUrl } from "@/lib/member-portal-share";
import { EmptyState } from "@alumni/ui";
import { ShareLinkButton } from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton, TableSkeleton } from "@alumni/ui";
import { YouTubeEmbed, YouTubePreview } from "@alumni/ui";
import { ImageUpload } from "@alumni/ui";
import { ZoomableImage } from "@alumni/ui";
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
  const { data: institution } = useQuery({
    queryKey: ["institution-profile"],
    queryFn: getInstitutionProfile,
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
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-contributions", id] });
      // Confirming changes this campaign's collected amount and paid-member
      // count, both shown above in the progress summary on this same page.
      qc.invalidateQueries({ queryKey: ["admin-campaign", id] });
      qc.invalidateQueries({ queryKey: ["admin-campaigns"] });
      setConfirmTarget(null);
      toast.success("Contribution confirmed!");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const [confirmCampaignDisburseOpen, setConfirmCampaignDisburseOpen] = useState(false);

  const rejectMut = useMutation({
    mutationFn: (cid: string) => rejectContribution(cid),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-contributions", id] });
      qc.invalidateQueries({ queryKey: ["admin-campaign", id] });
      qc.invalidateQueries({ queryKey: ["admin-campaigns"] });
      setRejectTarget(null);
      toast.success("Contribution rejected.");
    },
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
  const shareUrl = buildMemberPortalShareUrl(campaign.isMembershipCampaign ? `/contributions/${id}` : `/payment-campaign/${id}`, institution?.memberPortalUrl);
  const daysUntilDeadline = Math.ceil((new Date(campaign.deadline).getTime() - Date.now()) / (1000 * 60 * 60 * 24));
  // A quiet nudge, not an alert: only for an active, not-yet-met goal within reach of its deadline.
  const showDeadlineReminder = campaign.status === "Active" && pct < 100 && daysUntilDeadline >= 0 && daysUntilDeadline <= 14;
  const reminderBroadcastHref = `/broadcast?title=${encodeURIComponent(campaign.title)}&message=${encodeURIComponent(
    campaign.isMembershipCampaign
      ? `Reminder: "${campaign.title}" dues are due by ${formatDate(campaign.deadline)}. Please make your payment if you haven't already.`
      : `Reminder: "${campaign.title}" closes on ${formatDate(campaign.deadline)}. We're at ${pct}% of the target — every contribution helps.`,
  )}`;

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
        <div className="flex items-center gap-2">
          <ShareLinkButton
            url={shareUrl}
            title={campaign.title}
            variant="outline"
            size="sm"
            onSuccess={(result) => {
              toast.success(result === "shared" ? "Share sheet opened" : "Member portal link copied");
              if (!institution?.memberPortalUrl) {
                toast.warning("Member portal URL is not configured — this shared the current admin URL instead, which members can't open. Set it up in institution settings.");
              }
            }}
            onError={(message) => {
              if (!institution?.memberPortalUrl) {
                toast.warning("Member portal URL is not configured, so this uses the current origin as a fallback.");
              }
              toast.error(message);
            }}
          />
          {campaign.status === "Active" && (
            <Button variant="outline" onClick={() => setEditing(!editing)}>
              <Pencil size={14} />{editing ? "Cancel edit" : "Edit"}
            </Button>
          )}
        </div>
      </div>

      {editing && <CampaignEditForm campaign={campaign} isSuperAdmin={isSuperAdmin} saving={updateMut.isPending} onSave={(body) => updateMut.mutate(body)} onCancel={() => setEditing(false)} />}

      {/* Summary: the headline figure first, everything else quietly beneath it */}
      <Card className="overflow-hidden">
        {campaign.bannerImageUrl && (
          <button
            onClick={() => setLightboxOpen(true)}
            className="group relative block w-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
            title="Click to expand"
          >
            <img src={campaign.bannerImageUrl} alt={campaign.title} className="h-44 w-full object-cover sm:h-56 cursor-zoom-in" loading="lazy" />
            <span className="absolute right-3 top-3 flex h-8 w-8 items-center justify-center bg-black/45 text-white opacity-0 transition-opacity group-hover:opacity-100"><Expand size={15} /></span>
          </button>
        )}
        <CardContent className="space-y-7 p-5 sm:p-7">
          <div>
            <p className="text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">{campaign.isMembershipCampaign ? "Dues collected" : "Collected"}</p>
            <div className="mt-1 flex flex-wrap items-baseline gap-x-3">
              <span className="text-4xl font-bold tabular-nums text-success">{formatCurrency(campaign.collectedAmount)}</span>
              <span className="text-sm text-muted-foreground tabular-nums">of {formatCurrency(campaign.targetAmount)} · {pct}%</span>
            </div>
            <Progress value={pct} className="mt-4 h-1.5" />
            {campaign.description && <p className="mt-5 max-w-2xl text-[14px] leading-relaxed text-muted-foreground whitespace-pre-line">{campaign.description}</p>}
          </div>

          <dl className="grid grid-cols-2 gap-x-6 gap-y-5 border-t border-border/60 pt-6 sm:grid-cols-4">
            <Stat label="Paid members" value={String(campaign.paidCount)} />
            <Stat label="Closes" value={formatDate(campaign.deadline)} hint={campaign.status === "Active" && daysUntilDeadline >= 0 ? (daysUntilDeadline === 0 ? "Today" : `${daysUntilDeadline} day${daysUntilDeadline === 1 ? "" : "s"} left`) : undefined} />
            <Stat label="Successful payments" value={paystackSummary ? String(paystackSummary.confirmedCount) : "–"} />
            <Stat label="Online, collected" value={paystackSummary ? formatCurrency(paystackSummary.totalPaidToPaystack) : "–"} />
          </dl>

          {paystackSummary && (
            <dl className="grid grid-cols-2 gap-x-6 gap-y-5 border-t border-border/60 pt-6 sm:grid-cols-4">
              <Stat label="Disbursed" value={formatCurrency(paystackSummary.totalDisbursed)} hint={`${paystackSummary.disbursedCount} payment${paystackSummary.disbursedCount === 1 ? "" : "s"}`} />
              <Stat label="Outstanding" value={formatCurrency(paystackSummary.totalOutstanding)} hint="Yours to receive" />
            </dl>
          )}
          {!loadingPaystackSummary && !paystackSummary && <p className="text-sm text-muted-foreground">No online payment summary available.</p>}

          {user?.role === "SuperAdmin" && campaign.status === "Closed" && paystackSummary && paystackSummary.totalOutstanding > 0 && (
            <Button variant="destructive" onClick={() => setConfirmCampaignDisburseOpen(true)} isLoading={campaignDisburseMut.isPending}>
              Mark online payments as disbursed
            </Button>
          )}

          {isSuperAdmin && showDeadlineReminder && (
            <div className="flex flex-wrap items-center justify-between gap-3 border-l-2 border-primary bg-muted/40 py-3 pl-4 pr-3">
              <p className="text-[13px] text-foreground">
                {daysUntilDeadline === 0 ? "Deadline is today" : `${daysUntilDeadline} day${daysUntilDeadline === 1 ? "" : "s"} left`}, {campaign.paidCount > 0 ? "some members still haven't paid." : "no one has paid yet."}
              </p>
              <Link href={reminderBroadcastHref}>
                <Button size="sm" variant="outline" className="font-semibold">Send a reminder</Button>
              </Link>
            </div>
          )}

          {campaign.youtubeVideoUrl && (
            <div className="max-w-2xl border-t border-border/60 pt-6"><YouTubeEmbed url={campaign.youtubeVideoUrl} /></div>
          )}

          <p className="text-[12.5px] text-muted-foreground">Members can give any amount, and give again over time. Online payments reach your institution in full.</p>
        </CardContent>
      </Card>


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

      <Card>
        <CardContent className="p-0">
          <div className="px-4 py-3 border-b border-border/50">
            <h2 className="text-base font-semibold">Contributions ({contribs?.totalCount ?? 0})</h2>
          </div>
          <Table stackOnMobile className="min-w-[760px] sm:min-w-[920px]">
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
                <TableRow><TableCell colSpan={6}><EmptyState className="py-8" title="Payments for this fundraiser appear here" description="When a member pays, you see who gave, how much and when. Share the fundraiser link to get things moving." /></TableCell></TableRow>
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

      {/* Pledges — promises to give, shown apart from real money. Renders nothing until someone has pledged. */}
      {!campaign.isMembershipCampaign && <PledgesPanel campaignId={id} canManage />}

      {/* Updates — close the loop on what the money did, postable any time */}
      {!campaign.isMembershipCampaign && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2"><Megaphone size={16} className="text-primary" />Updates</CardTitle>
            <p className="text-[12.5px] text-muted-foreground">Post progress here so givers see what their money did, not just at the end.</p>
          </CardHeader>
          <CardContent className="space-y-4">
            <CampaignUpdatesSection campaignId={id} />
          </CardContent>
        </Card>
      )}

      {/* Public page — shareable thank-you page listing givers' names, never amounts. Institution-wide fundraisers only. */}
      {!campaign.isMembershipCampaign && !campaign.communityId && (
        <PublicPagePanel
          key={JSON.stringify(campaign.publicPage ?? null)}
          campaign={campaign}
          pageUrl={buildMemberPortalShareUrl(`/fundraiser/${id}`, institution?.memberPortalUrl)}
        />
      )}


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
  const [allowPledges, setAllowPledges] = useState(campaign.allowPledges ?? false);
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
      allowPledges: isMembershipCampaign ? false : allowPledges,
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
    <Card className="animate-in fade-in slide-in-from-top-4 duration-500">
      <CardHeader className="border-b border-border/60 pb-4">
        <CardTitle className="text-base">{isMembershipCampaign ? "Edit membership dues" : "Edit fundraiser"}</CardTitle>
      </CardHeader>
      <CardContent className="pt-2">
        <form onSubmit={handleSubmit}>
          {isMembershipCampaign ? (
            <>
              <FormSection title="Details" hint="What members see when they open these dues.">
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <div className="space-y-2">
                    <Label>Title</Label>
                    <Input placeholder="e.g. Dues 2026" value={title} onChange={(e) => setTitle(e.target.value)} required />
                  </div>
                  <div className="space-y-2">
                    <Label>Dues year</Label>
                    <Input type="number" value={membershipYear} onChange={(e) => { const y = Number(e.target.value); setMembershipYear(y); setTitle(`Dues ${y}`); }} required />
                  </div>
                </div>
                <p className="text-[12.5px] text-muted-foreground -mt-2">
                  {membershipYear === currentYear
                    ? "Current year: members must pay this to stay active."
                    : membershipYear > currentYear
                      ? "Future year: optional early payment, does not affect active status."
                      : "Past year: for members who haven't paid for previous years."}
                </p>
                <div className="space-y-2">
                  <Label>Description <span className="font-normal text-muted-foreground">(optional)</span></Label>
                  <Textarea placeholder="Describe the purpose of this dues period..." rows={2} value={description} onChange={(e) => setDescription(e.target.value)} />
                </div>
              </FormSection>

              <FormSection title="Amounts and deadline">
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                  <div className="space-y-2">
                    <Label>Employed members (GHS)</Label>
                    <Input type="number" min={1} step="0.01" placeholder="e.g. 100" value={amountPerMember} onChange={(e) => setAmountPerMember(e.target.value)} required />
                  </div>
                  <div className="space-y-2">
                    <Label>Pensioners (GHS)</Label>
                    <Input type="number" min={1} step="0.01" placeholder="e.g. 50" value={pensionerAmountPerMember} onChange={(e) => setPensionerAmountPerMember(e.target.value)} />
                  </div>
                  <div className="space-y-2">
                    <Label>Deadline</Label>
                    <Input type="date" value={deadline} onChange={(e) => setDeadline(e.target.value)} required />
                  </div>
                </div>
                <p className="text-[12.5px] text-muted-foreground -mt-2">Leave the pensioner amount empty to charge everyone the same.</p>
              </FormSection>

              <FormSection title="Banner">
                <ImageUpload file={bannerImage} existingUrl={existingBannerUrl} onChange={setBannerImage} onClearExisting={() => setExistingBannerUrl("")} label="Upload banner image" />
              </FormSection>

              {manualPaymentsEnabled && (
                <FormSection title="Ways to pay">
                  <OptionRow id="edit-manual-pay-membership" checked={allowManualPayments} onChange={setAllowManualPayments} title="Bank and mobile money transfers" hint="Members can pay offline and you confirm each payment." />
                </FormSection>
              )}
            </>
          ) : (
            <>
              <FormSection title="Details" hint="What members and visitors read first.">
                <div className="space-y-2">
                  <Label>Title</Label>
                  <Input value={title} onChange={(e) => setTitle(e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Description</Label>
                  <Textarea rows={4} value={description} onChange={(e) => setDescription(e.target.value)} />
                </div>
              </FormSection>

              <FormSection title="Goal and timing">
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                  <div className="space-y-2">
                    <Label>Target (GHS)</Label>
                    <Input type="number" value={targetAmount} onChange={(e) => setTargetAmount(e.target.value)} required />
                  </div>
                  <div className="space-y-2">
                    <Label>Minimum gift (GHS)</Label>
                    <Input type="number" value={amountPerMember} onChange={(e) => setAmountPerMember(e.target.value)} required />
                  </div>
                  <div className="space-y-2">
                    <Label>Deadline</Label>
                    <Input type="date" value={deadline} onChange={(e) => setDeadline(e.target.value)} required />
                  </div>
                </div>
              </FormSection>

              <FormSection title="Who can see it" hint="Limit this fundraiser to certain year groups.">
                {isSuperAdmin ? (
                  <>
                    <OptionRow id="edit-all-years" checked={yearGroupsAll} onChange={setYearGroupsAll} title="Everyone" hint="Members of every year group see this fundraiser." />
                    {!yearGroupsAll && <YearGroupPicker value={yearGroups} onChange={setYearGroups} />}
                  </>
                ) : (
                  <p className="text-[13px] text-muted-foreground">Only super admins can choose year groups.</p>
                )}
              </FormSection>

              <FormSection title="Media">
                <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
                  <div className="space-y-2">
                    <Label>Banner image <span className="font-normal text-muted-foreground">(optional)</span></Label>
                    <ImageUpload file={bannerImage} existingUrl={existingBannerUrl} onChange={setBannerImage} onClearExisting={() => setExistingBannerUrl("")} label="Upload banner image" />
                  </div>
                  <div className="space-y-2">
                    <Label>YouTube video <span className="font-normal text-muted-foreground">(optional)</span></Label>
                    <Input type="url" placeholder="https://youtube.com/..." value={youtubeVideoUrl} onChange={(e) => setYoutubeVideoUrl(e.target.value)} />
                    {youtubeVideoUrl && <YouTubePreview url={youtubeVideoUrl} />}
                  </div>
                </div>
              </FormSection>

              <FormSection title="Ways to give">
                <div className="divide-y divide-border/50 -my-3">
                  <OptionRow id="edit-allow-pledges" checked={allowPledges} onChange={setAllowPledges} title="Allow pledges" hint="Members can promise to give by a date. Pledges are not payments." />
                  {manualPaymentsEnabled && (
                    <OptionRow id="edit-manual-pay-regular" checked={allowManualPayments} onChange={setAllowManualPayments} title="Bank and mobile money transfers" hint="Members can pay offline and you confirm each payment." />
                  )}
                  {isSuperAdmin && (
                    <OptionRow id="edit-membership" checked={isMembershipCampaign} onChange={setIsMembershipCampaign} title="Treat as membership dues" hint="Moves this to Dues, where payment keeps members active." />
                  )}
                </div>
                {isSuperAdmin && isMembershipCampaign && (
                  <div className="flex items-center gap-3">
                    <Label className="min-w-max">Dues year</Label>
                    <Input type="number" value={membershipYear} onChange={(e) => setMembershipYear(Number(e.target.value))} className="w-32" />
                  </div>
                )}
              </FormSection>
            </>
          )}

          {manualPaymentsEnabled && allowManualPayments && (
            <FormSection title="Where transfers go" hint="Shown to members who choose to pay by transfer.">
              <div className="grid grid-cols-1 gap-8 lg:grid-cols-2">
                <div className="space-y-3">
                  <h4 className="text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">Bank account</h4>
                  <div className="space-y-1.5"><Label>Account number</Label><Input value={bankAccountNumber} onChange={(e) => setBankAccountNumber(e.target.value)} /></div>
                  <div className="space-y-1.5"><Label>Account name</Label><Input value={bankAccountName} onChange={(e) => setBankAccountName(e.target.value)} /></div>
                  <div className="grid grid-cols-2 gap-3">
                    <div className="space-y-1.5"><Label>Bank</Label><Input value={bankName} onChange={(e) => setBankName(e.target.value)} /></div>
                    <div className="space-y-1.5"><Label>Branch</Label><Input value={bankBranch} onChange={(e) => setBankBranch(e.target.value)} /></div>
                  </div>
                </div>
                <div className="space-y-3">
                  <h4 className="text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">Mobile money</h4>
                  <div className="space-y-1.5"><Label>Number</Label><Input type="tel" value={mobileMoneyNumber} onChange={(e) => setMobileMoneyNumber(e.target.value)} /></div>
                  <div className="space-y-1.5"><Label>Account name</Label><Input value={mobileMoneyName} onChange={(e) => setMobileMoneyName(e.target.value)} /></div>
                  <div className="space-y-1.5"><Label>Provider</Label><Input placeholder="MTN, Telecel or AT" value={mobileMoneyProvider} onChange={(e) => setMobileMoneyProvider(e.target.value)} /></div>
                </div>
              </div>
            </FormSection>
          )}

          <div className="flex justify-end gap-3 border-t border-border/60 pt-5">
            <Button type="button" variant="outline" onClick={onCancel}>Cancel</Button>
            <Button type="submit" isLoading={saving} loadingText="Saving">Save changes</Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

/** A settings group: a short label on the left, its fields on the right (stacked on phones). */
function FormSection({ title, hint, children }: { title: string; hint?: string; children: React.ReactNode }) {
  return (
    <section className="grid gap-4 border-b border-border/60 py-6 md:grid-cols-[190px_1fr] md:gap-10">
      <div>
        <h3 className="text-[13.5px] font-semibold">{title}</h3>
        {hint && <p className="mt-1 text-[12.5px] leading-snug text-muted-foreground">{hint}</p>}
      </div>
      <div className="min-w-0 space-y-4">{children}</div>
    </section>
  );
}

function OptionRow({ id, checked, onChange, title, hint }: { id: string; checked: boolean; onChange: (v: boolean) => void; title: string; hint?: string }) {
  return (
    <label htmlFor={id} className="flex cursor-pointer items-start gap-3 py-3">
      <Checkbox id={id} checked={checked} onCheckedChange={(v) => onChange(v === true)} className="mt-0.5" />
      <span>
        <span className="block text-sm font-medium">{title}</span>
        {hint && <span className="block text-[12.5px] leading-snug text-muted-foreground">{hint}</span>}
      </span>
    </label>
  );
}

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div>
      <dt className="text-[12px] text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-lg font-semibold tabular-nums">{value}</dd>
      {hint && <p className="text-[12px] text-muted-foreground">{hint}</p>}
    </div>
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
        <EmptyState className="py-8" title="Updates keep supporters engaged" description="Post a short note on progress or how the money is being used. Members who gave will see it." />
      ) : (
        <div className="space-y-3">
          {updates.map((u) => (
            <div key={u.id} className="rounded-xl border border-border overflow-hidden">
              {u.imageUrl && <ZoomableImage src={u.imageUrl} alt="" className="w-full object-cover" style={{ maxHeight: 220 }} />}
              <div className="p-3.5 flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="text-[13.5px] whitespace-pre-wrap leading-relaxed">{u.body}</p>
                  <p className="text-[12px] text-muted-foreground mt-1.5">
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
