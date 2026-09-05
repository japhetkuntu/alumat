"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Calendar, CreditCard, ChevronRight, Pencil } from "@alumni/ui";
import Link from "next/link";
import { toast } from "sonner";
import { getCampaigns, createCampaign, getMembers } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { formatCurrency, formatDate, cn } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { useFeatureEnabled } from "@/hooks/use-institution-features";
import { CardSkeleton } from "@alumni/ui";
import { ImageUpload } from "@alumni/ui";
import type { Campaign } from "@/types";

export default function AdminMembershipPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const manualPaymentsEnabled = useFeatureEnabled("ManualPayments");
  const qc = useQueryClient();
  const currentYear = new Date().getFullYear();
  const [showCreate, setShowCreate] = useState(false);

  // Form state for creating membership campaign
  const [form, setForm] = useState({
    title: `Dues ${currentYear}`,
    description: "",
    amountPerMember: "",
    pensionerAmountPerMember: "",
    deadline: "",
    membershipYear: currentYear,
    allowManualPayments: false,
    bankAccountNumber: "",
    bankAccountName: "",
    bankName: "",
    bankBranch: "",
    mobileMoneyNumber: "",
    mobileMoneyName: "",
    mobileMoneyProvider: "",
  });
  const [bannerImage, setBannerImage] = useState<File | null>(null);
  const f = <K extends keyof typeof form>(k: K, v: (typeof form)[K]) => setForm((prev) => ({ ...prev, [k]: v }));

  const { data: campaignsData, isLoading } = useQuery({
    queryKey: ["admin-campaigns", "", 1],
    queryFn: () => getCampaigns(1, 100),
  });

  const { data: membersData } = useQuery({
    queryKey: ["admin-members-count"],
    queryFn: () => getMembers({ page: 1, pageSize: 1, status: "Active" }),
  });

  const totalMembers = membersData?.totalCount ?? 0;

  const membershipCampaigns = (campaignsData?.results ?? [])
    .filter((c: Campaign) => c.isMembershipCampaign)
    .sort((a, b) => (b.membershipYear ?? 0) - (a.membershipYear ?? 0));

  const currentYearCampaigns = membershipCampaigns.filter((c) => c.membershipYear === currentYear);
  const futureYearCampaigns = membershipCampaigns.filter((c) => (c.membershipYear ?? 0) > currentYear);
  const pastYearCampaigns = membershipCampaigns.filter((c) => (c.membershipYear ?? 0) < currentYear);

  const currentYearPaidCount = currentYearCampaigns.reduce((sum, c) => sum + c.paidCount, 0);
  const currentYearActive = currentYearCampaigns.length > 0;
  const currentYearEligible = currentYearCampaigns[0]?.totalEligibleMembers ?? totalMembers;

  const createMut = useMutation({
    mutationFn: () => {
      const amount = Number(form.amountPerMember);
      const pensionerAmount = form.pensionerAmountPerMember ? Number(form.pensionerAmountPerMember) : undefined;
      // Target amount = amount × total members (for progress tracking)
      const targetAmount = amount * Math.max(totalMembers, 1);
      return createCampaign({
        title: form.title,
        description: form.description,
        targetAmount,
        amountPerMember: amount,
        pensionerAmountPerMember: pensionerAmount,
        deadline: form.deadline,
        isMembershipCampaign: true,
        membershipYear: form.membershipYear,
        bannerImage: bannerImage || undefined,
        allowManualPayments: form.allowManualPayments,
        bankAccountNumber: form.bankAccountNumber || undefined,
        bankAccountName: form.bankAccountName || undefined,
        bankName: form.bankName || undefined,
        bankBranch: form.bankBranch || undefined,
        mobileMoneyNumber: form.mobileMoneyNumber || undefined,
        mobileMoneyName: form.mobileMoneyName || undefined,
        mobileMoneyProvider: form.mobileMoneyProvider || undefined,
      });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-campaigns"] });
      setShowCreate(false);
      toast.success("Dues period created");
      setBannerImage(null);
      setForm((prev) => ({ ...prev, title: `Dues ${prev.membershipYear}`, description: "", amountPerMember: "", pensionerAmountPerMember: "", deadline: "" }));
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-6">
      {/* Header */}
      <header className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Dues</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5 max-w-2xl">
            Recurring dues cycles that determine active membership. Members must pay the <strong>current year&apos;s</strong> dues to remain active — future years are optional early payment.
          </p>
        </div>
        {isSuperAdmin && (
          <Button onClick={() => setShowCreate(!showCreate)}>
            <Plus size={16} />Create renewal period
          </Button>
        )}
      </header>

      {/* Summary Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        <div className="card p-4">
          <span className="text-[12px] text-muted-foreground">Eligible members</span>
          <b className="block text-[24px] mt-1 tabular-nums">{currentYearEligible}</b>
        </div>
        <div className="card p-4">
          <span className="text-[12px] text-muted-foreground">Paid this period</span>
          <b className="block text-[24px] mt-1 tabular-nums">{currentYearPaidCount}</b>
        </div>
        <div className="card p-4">
          <span className="text-[12px] text-muted-foreground">Unpaid this period</span>
          <b className="block text-[24px] mt-1 tabular-nums">{currentYearActive ? Math.max(0, currentYearEligible - currentYearPaidCount) : 0}</b>
          {!currentYearActive && (
            <small className="block text-[11px] text-muted-foreground mt-1" title={`No membership dues for ${currentYear} yet, so there's nothing to be unpaid on.`}>
              No {currentYear} dues yet
            </small>
          )}
        </div>
        <div className="card p-4">
          <span className="text-[12px] text-muted-foreground">Historical cycles</span>
          <b className="block text-[24px] mt-1 tabular-nums">{membershipCampaigns.length}</b>
        </div>
      </div>

      {/* Create Form */}
      {showCreate && isSuperAdmin && (
        <Card className="animate-in fade-in slide-in-from-top-4 duration-500 border-primary/30">
          <CardHeader>
            <CardTitle className="text-base">Create Dues Period</CardTitle>
          </CardHeader>
          <CardContent>
            <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); createMut.mutate(); }}>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Title</Label>
                  <Input placeholder="e.g. Dues 2026" value={form.title} onChange={(e) => f("title", e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Dues Year</Label>
                  <Input type="number" value={form.membershipYear} onChange={(e) => { const y = Number(e.target.value); f("membershipYear", y); f("title", `Dues ${y}`); }} required />
                  <p className="text-xs text-muted-foreground">
                    {form.membershipYear === currentYear
                      ? "Current year — members must pay this to stay active."
                      : form.membershipYear > currentYear
                        ? "Future year — optional early payment, does not affect active status."
                        : "Past year — for members who haven't paid for previous years."}
                  </p>
                </div>
              </div>
              <div className="space-y-2">
                <Label>Description (optional)</Label>
                <Textarea placeholder="Describe the purpose of this dues period..." rows={2} value={form.description} onChange={(e) => f("description", e.target.value)} />
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label>Amount for employed members (GHS)</Label>
                  <Input type="number" min={1} step="0.01" placeholder="e.g. 100" value={form.amountPerMember} onChange={(e) => f("amountPerMember", e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Amount for pensioners (GHS)</Label>
                  <Input type="number" min={1} step="0.01" placeholder="e.g. 50" value={form.pensionerAmountPerMember} onChange={(e) => f("pensionerAmountPerMember", e.target.value)} />
                  <p className="text-xs text-muted-foreground">Leave empty to use same amount as employed members.</p>
                </div>
                <div className="space-y-2">
                  <Label>Deadline</Label>
                  <Input type="date" value={form.deadline} onChange={(e) => f("deadline", e.target.value)} required />
                </div>
              </div>

              <div className="space-y-2">
                <Label>Banner image (optional)</Label>
                <ImageUpload file={bannerImage} existingUrl="" onChange={setBannerImage} onClearExisting={() => {}} label="Upload banner image" />
              </div>

              {manualPaymentsEnabled && (
                <div className="space-y-2">
                  <Label>Allow Manual Payments</Label>
                  <div className="flex items-center gap-2">
                    <input id="m-manual-pay" type="checkbox" checked={form.allowManualPayments} onChange={(e) => f("allowManualPayments", e.target.checked)} className="h-4 w-4" />
                    <label htmlFor="m-manual-pay" className="text-sm">Bank/Mobile money transfers</label>
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
                <Button type="submit" size="sm" isLoading={createMut.isPending} loadingText="Creating">Create</Button>
                <Button type="button" size="sm" variant="outline" onClick={() => setShowCreate(false)}>Cancel</Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}

      {/* Current Year Section */}
      <section className="space-y-3">
        <div className="flex items-center gap-2.5">
          <h2 className="text-[19px] font-bold m-0">Current period</h2>
          <Badge variant="success">Required for active status</Badge>
        </div>

        {isLoading ? (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6"><CardSkeleton /><CardSkeleton /></div>
        ) : currentYearCampaigns.length === 0 ? (
          <Card className="border-dashed border-2 border-border/60">
            <CardContent className="p-8 text-center space-y-3">
              <p className="text-muted-foreground font-medium">No membership dues for {currentYear} yet.</p>
              <p className="text-sm text-muted-foreground">Members will retain their previous active status until the current-year dues are created.</p>
              {isSuperAdmin && (
                <Button onClick={() => { setForm((prev) => ({ ...prev, membershipYear: currentYear, title: `Dues ${currentYear}` })); setShowCreate(true); }} variant="outline">
                  <Plus size={14} />Create {currentYear} Dues
                </Button>
              )}
            </CardContent>
          </Card>
        ) : (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            {currentYearCampaigns.map((c) => (
              <MembershipCampaignCard key={c.id} campaign={c} totalMembers={totalMembers} isCurrent />
            ))}
          </div>
        )}
      </section>

      {/* Future Years Section */}
      {(futureYearCampaigns.length > 0 || isSuperAdmin) && (
        <section className="space-y-3">
          <div className="flex items-center gap-2.5">
            <h2 className="text-[19px] font-bold m-0">Future periods</h2>
            <Badge variant="info">Optional early renewal</Badge>
          </div>

          <p className="text-[13px] text-muted-foreground max-w-2xl">
            Future membership dues allow members to pay ahead. However, <strong>not paying</strong> future dues does <strong>not</strong> affect a member&apos;s active status.
            Only the current year&apos;s dues determine membership activity.
          </p>

          {futureYearCampaigns.length === 0 ? (
            <Card className="border-dashed border-2 border-border/60">
              <CardContent className="p-6 text-center">
                <p className="text-muted-foreground font-medium">No future membership dues created.</p>
                {isSuperAdmin && (
                  <Button onClick={() => { setForm((prev) => ({ ...prev, membershipYear: currentYear + 1, title: `Dues ${currentYear + 1}` })); setShowCreate(true); }} variant="outline" className="mt-3">
                    <Plus size={14} />Create {currentYear + 1} Dues
                  </Button>
                )}
              </CardContent>
            </Card>
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
              {futureYearCampaigns.map((c) => (
                <MembershipCampaignCard key={c.id} campaign={c} totalMembers={totalMembers} isCurrent={false} />
              ))}
            </div>
          )}
        </section>
      )}

      {/* Past Years Section */}
      {pastYearCampaigns.length > 0 && (
        <section className="space-y-3">
          <h2 className="text-[19px] font-bold m-0 text-muted-foreground">Past periods</h2>
          <div className="grid grid-cols-1 lg:grid-cols-2 xl:grid-cols-3 gap-4">
            {pastYearCampaigns.map((c) => (
              <MembershipCampaignCard key={c.id} campaign={c} totalMembers={totalMembers} isCurrent={false} compact />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}

function MembershipCampaignCard({ campaign: c, totalMembers: fallbackTotal, isCurrent, compact }: { campaign: Campaign; totalMembers: number; isCurrent: boolean; compact?: boolean }) {
  const totalMembers = c.totalEligibleMembers ?? fallbackTotal;
  const paidPct = totalMembers > 0 ? Math.round((c.paidCount / totalMembers) * 100) : 0;
  const unpaid = Math.max(0, totalMembers - c.paidCount);

  if (compact) {
    return (
      <Card className="border-border/40 hover:border-primary/20 transition-all">
        <CardContent className="p-4 space-y-2">
          <div className="flex items-center justify-between gap-2">
            <h3 className="font-bold text-sm">{c.title}</h3>
            <Badge variant={c.status === "Active" ? "success" : "secondary"} className="text-[9px] font-bold uppercase">{c.status}</Badge>
          </div>
          <div className="flex items-center justify-between text-xs text-muted-foreground tabular-nums">
            <span>Year: {c.membershipYear ?? "?"}</span>
            <span>{c.paidCount}/{totalMembers} paid ({paidPct}%)</span>
          </div>
          <Progress value={paidPct} className="h-1.5" />
          <Link href={`/campaigns/${c.id}`}>
            <Button size="sm" variant="ghost" className="w-full text-xs font-bold h-8">View <ChevronRight size={12} /></Button>
          </Link>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardContent className="p-[18px] space-y-4">
        {/* Header */}
        <div className="flex items-start justify-between gap-3">
          <div className="space-y-1">
            <h3 className="font-bold text-lg leading-tight">{c.title}</h3>
            {c.description && <p className="text-sm text-muted-foreground line-clamp-2">{c.description}</p>}
          </div>
          <div className="flex flex-col items-end gap-1.5 shrink-0">
            <Badge variant={c.status === "Active" ? "success" : "secondary"} className="font-bold uppercase tracking-widest text-[9px]">{c.status}</Badge>
            <Badge variant={isCurrent ? "success" : "info"} className="font-bold text-[10px]">
              {c.membershipYear ?? "?"}
            </Badge>
          </div>
        </div>

        {/* Key Stats */}
        <div className="grid grid-cols-3 gap-4 py-4 border-y border-border/40">
          <div className="text-center">
            <p className="text-[9px] font-black uppercase tracking-widest text-muted-foreground/60 mb-1">Paid</p>
            <p className="text-2xl font-black text-success tabular-nums">{c.paidCount}</p>
          </div>
          <div className="text-center">
            <p className="text-[9px] font-black uppercase tracking-widest text-muted-foreground/60 mb-1">Unpaid</p>
            <p className="text-2xl font-black text-warning tabular-nums">{unpaid}</p>
          </div>
          <div className="text-center">
            <p className="text-[9px] font-black uppercase tracking-widest text-muted-foreground/60 mb-1">Total</p>
            <p className="text-2xl font-black tabular-nums">{totalMembers}</p>
          </div>
        </div>

        {/* Progress */}
        <div className="space-y-2">
          <div className="flex items-center justify-between text-xs font-bold tabular-nums">
            <span className="text-primary">{paidPct}% of members paid</span>
            <span className="text-muted-foreground">{c.paidCount}/{totalMembers}</span>
          </div>
          <Progress value={paidPct} className="h-2.5" />
        </div>

        {/* Meta */}
        <div className="flex flex-wrap gap-4 text-xs text-muted-foreground">
          <div className="flex items-center gap-1.5 tabular-nums">
            <CreditCard size={14} />
            <span className="font-bold">{formatCurrency(c.amountPerMember)}</span> employed
            {c.pensionerAmountPerMember != null && (
              <> · <span className="font-bold">{formatCurrency(c.pensionerAmountPerMember)}</span> pensioner</>
            )}
          </div>
          <div className="flex items-center gap-1.5">
            <Calendar size={14} />
            Deadline: <span className="font-bold">{formatDate(c.deadline)}</span>
          </div>
        </div>

        {isCurrent && (
          <div className="rounded-lg bg-success/10 border border-success/30 p-3 text-xs text-success font-medium">
            <strong>Active status:</strong> Members must pay these dues to be considered active for {c.membershipYear}.
          </div>
        )}

        {!isCurrent && (c.membershipYear ?? 0) > new Date().getFullYear() && (
          <div className="rounded-lg bg-info/10 border border-info/30 p-3 text-xs text-info font-medium">
            <strong>Optional:</strong> Members can pay early, but skipping this won&apos;t affect their current active status.
          </div>
        )}

        {/* Actions */}
        <div className="flex gap-2">
          <Link href={`/campaigns/${c.id}`} className="flex-1">
            <Button size="sm" variant="outline" className="w-full font-bold">
              <Pencil size={13} />Manage
            </Button>
          </Link>
        </div>
      </CardContent>
    </Card>
  );
}
