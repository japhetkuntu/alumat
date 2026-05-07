"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Users, Calendar, CreditCard, CheckCircle2, Clock, ChevronRight, Pencil } from "lucide-react";
import Link from "next/link";
import { toast } from "sonner";
import { getCampaigns, createCampaign, getMembers } from "@/lib/admin-api";
import { handleApiError } from "@/lib/api-client";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Progress } from "@/components/ui/progress";
import { formatCurrency, formatDate } from "@/lib/utils";
import { useAuth } from "@/hooks/use-auth";
import { EmptyState } from "@/components/ui/empty-state";
import { CardSkeleton } from "@/components/ui/skeleton";
import { ImageUpload } from "@/components/ui/image-upload";
import type { Campaign } from "@/types";

export default function AdminMembershipPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const qc = useQueryClient();
  const currentYear = new Date().getFullYear();
  const [showCreate, setShowCreate] = useState(false);

  // Form state for creating membership campaign
  const [form, setForm] = useState({
    title: `Membership Renewal ${currentYear}`,
    description: "",
    amountPerMember: "",
    pensionerAmountPerMember: "",
    deadline: "",
    membershipYear: currentYear,
    allowOnlinePayments: true,
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
        allowOnlinePayments: form.allowOnlinePayments,
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
      toast.success("Membership renewal campaign created");
      setBannerImage(null);
      setForm((prev) => ({ ...prev, title: `Membership Renewal ${prev.membershipYear}`, description: "", amountPerMember: "", pensionerAmountPerMember: "", deadline: "" }));
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <div className="p-8 lg:p-12 space-y-10 max-w-7xl mx-auto">
      {/* Header */}
      <header className="flex items-end justify-between gap-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div className="space-y-1">
          <p className="text-[11px] font-black uppercase tracking-[0.2em] text-primary">Financial</p>
          <h1 className="text-4xl font-black tracking-tight text-foreground">Membership Renewal</h1>
          <p className="text-muted-foreground font-medium">
            Manage membership campaigns by year. Members must pay <strong>current year</strong> campaigns to remain active.
            Future year campaigns are optional &mdash; paying early does not affect active status.
          </p>
        </div>
        {isSuperAdmin && (
          <Button onClick={() => setShowCreate(!showCreate)} className="shadow-lg shadow-primary/20 font-bold h-11 px-5">
            <Plus size={16} />New Membership Campaign
          </Button>
        )}
      </header>

      {/* Summary Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
        <Card className="border-border/40">
          <CardContent className="p-5">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center text-primary"><Users size={20} /></div>
              <div>
                <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Eligible Members</p>
                <p className="text-2xl font-black">{currentYearEligible}</p>
              </div>
            </div>
          </CardContent>
        </Card>
        <Card className="border-border/40">
          <CardContent className="p-5">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-green-500/10 flex items-center justify-center text-green-600"><CheckCircle2 size={20} /></div>
              <div>
                <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Paid ({currentYear})</p>
                <p className="text-2xl font-black">{currentYearPaidCount}</p>
              </div>
            </div>
          </CardContent>
        </Card>
        <Card className="border-border/40">
          <CardContent className="p-5">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-orange-500/10 flex items-center justify-center text-orange-600"><Clock size={20} /></div>
              <div>
                <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Unpaid ({currentYear})</p>
                <p className="text-2xl font-black">{currentYearActive ? Math.max(0, currentYearEligible - currentYearPaidCount) : "N/A"}</p>
              </div>
            </div>
          </CardContent>
        </Card>
        <Card className="border-border/40">
          <CardContent className="p-5">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-blue-500/10 flex items-center justify-center text-blue-600"><Calendar size={20} /></div>
              <div>
                <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Total Campaigns</p>
                <p className="text-2xl font-black">{membershipCampaigns.length}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Create Form */}
      {showCreate && isSuperAdmin && (
        <Card className="animate-in fade-in slide-in-from-top-4 duration-500 border-primary/30">
          <CardHeader>
            <CardTitle className="text-base">Create Membership Renewal Campaign</CardTitle>
          </CardHeader>
          <CardContent>
            <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); createMut.mutate(); }}>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Campaign title</Label>
                  <Input placeholder="e.g. Membership Renewal 2026" value={form.title} onChange={(e) => f("title", e.target.value)} required />
                </div>
                <div className="space-y-2">
                  <Label>Membership Year</Label>
                  <Input type="number" value={form.membershipYear} onChange={(e) => { const y = Number(e.target.value); f("membershipYear", y); f("title", `Membership Renewal ${y}`); }} required />
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
                <Textarea placeholder="Describe the purpose of this membership renewal..." rows={2} value={form.description} onChange={(e) => f("description", e.target.value)} />
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

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Allow Online Payments</Label>
                  <div className="flex items-center gap-2">
                    <input id="m-online-pay" type="checkbox" checked={form.allowOnlinePayments} onChange={(e) => f("allowOnlinePayments", e.target.checked)} className="h-4 w-4" />
                    <label htmlFor="m-online-pay" className="text-sm">Enable Paystack</label>
                  </div>
                </div>
                <div className="space-y-2">
                  <Label>Allow Manual Payments</Label>
                  <div className="flex items-center gap-2">
                    <input id="m-manual-pay" type="checkbox" checked={form.allowManualPayments} onChange={(e) => f("allowManualPayments", e.target.checked)} className="h-4 w-4" />
                    <label htmlFor="m-manual-pay" className="text-sm">Bank/Mobile money transfers</label>
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
                <Button type="submit" size="sm" isLoading={createMut.isPending} loadingText="Creating">Create Campaign</Button>
                <Button type="button" size="sm" variant="outline" onClick={() => setShowCreate(false)}>Cancel</Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}

      {/* Current Year Section */}
      <section className="space-y-4">
        <div className="flex items-center gap-3">
          <div className="w-2 h-8 bg-green-500 rounded-full" />
          <h2 className="text-2xl font-extrabold tracking-tight">Current Year ({currentYear})</h2>
          <Badge variant="success" className="font-bold text-[10px] uppercase tracking-widest">Required for active status</Badge>
        </div>

        {isLoading ? (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6"><CardSkeleton /><CardSkeleton /></div>
        ) : currentYearCampaigns.length === 0 ? (
          <Card className="border-dashed border-2 border-border/60">
            <CardContent className="p-8 text-center space-y-3">
              <p className="text-muted-foreground font-medium">No membership campaign for {currentYear} yet.</p>
              <p className="text-sm text-muted-foreground">Members will retain their previous active status until a current-year campaign is created.</p>
              {isSuperAdmin && (
                <Button onClick={() => { setForm((prev) => ({ ...prev, membershipYear: currentYear, title: `Membership Renewal ${currentYear}` })); setShowCreate(true); }} variant="outline">
                  <Plus size={14} />Create {currentYear} Campaign
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
        <section className="space-y-4">
          <div className="flex items-center gap-3">
            <div className="w-2 h-8 bg-blue-500 rounded-full" />
            <h2 className="text-2xl font-extrabold tracking-tight">Future Years</h2>
            <Badge variant="secondary" className="font-bold text-[10px] uppercase tracking-widest">Optional &mdash; early payment</Badge>
          </div>

          <p className="text-sm text-muted-foreground max-w-2xl">
            Future membership campaigns allow members to pay ahead. However, <strong>not paying</strong> a future campaign does <strong>not</strong> affect a member&apos;s active status.
            Only the current year&apos;s campaign determines membership activity.
          </p>

          {futureYearCampaigns.length === 0 ? (
            <Card className="border-dashed border-2 border-border/60">
              <CardContent className="p-6 text-center">
                <p className="text-muted-foreground font-medium">No future membership campaigns created.</p>
                {isSuperAdmin && (
                  <Button onClick={() => { setForm((prev) => ({ ...prev, membershipYear: currentYear + 1, title: `Membership Renewal ${currentYear + 1}` })); setShowCreate(true); }} variant="outline" className="mt-3">
                    <Plus size={14} />Create {currentYear + 1} Campaign
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
        <section className="space-y-4">
          <div className="flex items-center gap-3">
            <div className="w-2 h-8 bg-muted-foreground/30 rounded-full" />
            <h2 className="text-2xl font-extrabold tracking-tight text-muted-foreground">Past Years</h2>
          </div>
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
          <div className="flex items-center justify-between text-xs text-muted-foreground">
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
    <Card className={`overflow-hidden border-border/40 hover:shadow-xl transition-all ${isCurrent ? "ring-2 ring-green-500/20" : ""}`}>
      <div className={`h-1.5 ${isCurrent ? "bg-green-500" : "bg-blue-500"}`} />
      <CardContent className="p-6 space-y-5">
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
            <p className="text-2xl font-black text-green-600">{c.paidCount}</p>
          </div>
          <div className="text-center">
            <p className="text-[9px] font-black uppercase tracking-widest text-muted-foreground/60 mb-1">Unpaid</p>
            <p className="text-2xl font-black text-orange-500">{unpaid}</p>
          </div>
          <div className="text-center">
            <p className="text-[9px] font-black uppercase tracking-widest text-muted-foreground/60 mb-1">Total</p>
            <p className="text-2xl font-black">{totalMembers}</p>
          </div>
        </div>

        {/* Progress */}
        <div className="space-y-2">
          <div className="flex items-center justify-between text-xs font-bold">
            <span className="text-primary">{paidPct}% of members paid</span>
            <span className="text-muted-foreground">{c.paidCount}/{totalMembers}</span>
          </div>
          <Progress value={paidPct} className="h-2.5" />
        </div>

        {/* Meta */}
        <div className="flex flex-wrap gap-4 text-xs text-muted-foreground">
          <div className="flex items-center gap-1.5">
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
          <div className="rounded-lg bg-green-50 dark:bg-green-950/20 border border-green-200 dark:border-green-800 p-3 text-xs text-green-800 dark:text-green-300 font-medium">
            <strong>Active status:</strong> Members must pay this campaign to be considered active for {c.membershipYear}.
          </div>
        )}

        {!isCurrent && (c.membershipYear ?? 0) > new Date().getFullYear() && (
          <div className="rounded-lg bg-blue-50 dark:bg-blue-950/20 border border-blue-200 dark:border-blue-800 p-3 text-xs text-blue-800 dark:text-blue-300 font-medium">
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
