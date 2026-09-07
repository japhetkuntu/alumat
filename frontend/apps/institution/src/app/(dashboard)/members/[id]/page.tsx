"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, CheckCircle, XCircle, ShieldBan, ShieldCheck, MailCheck, MailX, MapPin, Building2, Briefcase, GraduationCap, Calendar, Link as LinkIcon, Smartphone, CreditCard, ChevronRight, Check, Share2 } from "@alumni/ui";
import { useState } from "react";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { formatDate, cn } from "@alumni/ui";
import { getMember, approveMember, rejectMember, banMember, unbanMember, activateMembership, getCampaigns, getInstitutionProfile } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { toast } from "sonner";
import type { MemberStatus } from "@/types";

const statusVariant: Record<MemberStatus, "success" | "warning" | "destructive" | "secondary"> = {
  Active: "success",
  Pending: "warning",
  Suspended: "destructive",
  Banned: "destructive",
  Blocked: "secondary",
};

type ModalState =
  | { type: "approve" }
  | { type: "reject" }
  | { type: "ban" }
  | { type: "unban" }
  | { type: "activate-membership" }
  | null;

export default function MemberDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [modal, setModal] = useState<ModalState>(null);
  const [reasonText, setReasonText] = useState("");
  const [selectedYears, setSelectedYears] = useState<number[]>([]);
  const [copied, setCopied] = useState(false);

  const copyProfileLink = async () => {
    await navigator.clipboard.writeText(window.location.href);
    setCopied(true);
    toast.success("Profile link copied to clipboard");
    setTimeout(() => setCopied(false), 2500);
  };
  const qc = useQueryClient();

  const { data: member, isLoading } = useQuery({
    queryKey: ["admin-member", id],
    queryFn: () => getMember(id),
  });

  const { data: institution } = useQuery({
    queryKey: ["institution-profile"],
    queryFn: getInstitutionProfile,
  });
  const duesRequired = institution?.memberActivePolicy !== "ApprovedOnly";

  const invalidate = () => qc.invalidateQueries({ queryKey: ["admin-member", id] });

  const approveMut = useMutation({
    mutationFn: () => approveMember(id),
    onSuccess: () => { invalidate(); setModal(null); toast.success("Member approved"); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const rejectMut = useMutation({
    mutationFn: () => rejectMember(id, reasonText || undefined),
    onSuccess: () => { invalidate(); setModal(null); setReasonText(""); toast.success("Registration rejected"); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const banMut = useMutation({
    mutationFn: () => banMember(id, reasonText || undefined),
    onSuccess: () => { invalidate(); setModal(null); setReasonText(""); toast.success("Member banned"); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const unbanMut = useMutation({
    mutationFn: () => unbanMember(id),
    onSuccess: () => { invalidate(); setModal(null); toast.success("Member unbanned"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const { data: campaignsData } = useQuery({
    queryKey: ["admin-campaigns-membership"],
    queryFn: () => getCampaigns(1, 100),
  });
  const membershipCampaigns = (campaignsData?.results ?? []).filter((c) => c.isMembershipCampaign && c.membershipYear);

  const activateMut = useMutation({
    mutationFn: () => activateMembership(id, selectedYears),
    onSuccess: () => { invalidate(); setModal(null); setSelectedYears([]); toast.success("Membership activated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (isLoading) return <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-6 page-enter"><CardSkeleton /><CardSkeleton /></div>;
  if (!member) return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto">
      <Link href="/members" className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground mb-6">
        <ArrowLeft size={14} /> Back to Members
      </Link>
      <EmptyState icon={<XCircle size={48} />} title="Member not found" description="This member profile doesn't exist or may have been removed." />
    </div>
  );

  const fullName = `${member.firstName} ${member.lastName}`;

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-6 page-enter">
      {/* Breadcrumb */}
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <nav className="flex items-center gap-1.5 text-sm">
          <Link href="/members">
            <Button size="sm" variant="ghost" className="h-8 px-2 text-muted-foreground hover:text-foreground font-medium">
              Members
            </Button>
          </Link>
          <ChevronRight size={14} className="text-muted-foreground shrink-0" />
          <span className="font-semibold text-foreground truncate max-w-[180px]">{fullName}</span>
        </nav>
        <Button
          size="sm"
          variant="outline"
          className={cn(
            "gap-2 h-8 px-3 rounded-lg transition-all duration-300 font-medium",
            copied && "border-success/40 bg-success/10 text-success hover:bg-success/10"
          )}
          onClick={copyProfileLink}
        >
          {copied ? <><Check size={13} /> Link copied!</> : <><Share2 size={13} /> Share profile</>}
        </Button>
      </div>

      {/* Profile header */}
      <Card>
        <CardContent className="p-6">
          <div className="flex items-start gap-5">
            <UserAvatar src={member.profilePictureUrl} name={fullName} size="xl" />
            <div className="flex-1">
              <div className="flex items-center gap-3 flex-wrap">
                <h2 className="text-xl font-bold">{fullName}</h2>
                <Badge variant={statusVariant[member.status as MemberStatus]}>{member.status}</Badge>
                {member.isEmailVerified
                  ? <span className="flex items-center gap-1 text-xs text-success"><MailCheck size={12} />Verified</span>
                  : <span className="flex items-center gap-1 text-xs text-muted-foreground"><MailX size={12} />Unverified</span>}
              </div>
              <p className="text-sm text-muted-foreground mt-1">{member.email}</p>
              {member.memberNumber && (
                <p className="text-sm font-mono text-primary mt-1">{member.memberNumber}</p>
              )}
            </div>
            <div className="flex gap-2 shrink-0 flex-wrap">
              {member.status === "Pending" && (
                <>
                  <Button size="sm" className="gap-1" onClick={() => setModal({ type: "approve" })}>
                    <CheckCircle size={13} />Approve
                  </Button>
                  <Button size="sm" variant="destructive" className="gap-1" onClick={() => { setReasonText(""); setModal({ type: "reject" }); }}>
                    <XCircle size={13} />Reject
                  </Button>
                </>
              )}
              {(member.status === "Active" || member.status === "Suspended") && (
                <Button size="sm" variant="destructive" className="gap-1" onClick={() => { setReasonText(""); setModal({ type: "ban" }); }}>
                  <ShieldBan size={13} />Ban
                </Button>
              )}
              {member.status === "Banned" && (
                <Button size="sm" className="gap-1" onClick={() => setModal({ type: "unban" })}>
                  <ShieldCheck size={13} />Unban
                </Button>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Details grid — mirrors the mockup's Personal / Professional / Membership 3-column layout */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card>
          <CardHeader><CardTitle className="text-base">Personal information</CardTitle></CardHeader>
          <CardContent className="space-y-3">
            <InfoRow icon={<GraduationCap size={14} />} label="Graduation Year" value={String(member.graduationYear)} />
            <InfoRow icon={<Calendar size={14} />} label="Joined" value={formatDate(member.createdAt)} />
            {member.phone && <InfoRow icon={<Smartphone size={14} />} label="Phone" value={member.phone} />}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">Professional information</CardTitle></CardHeader>
          <CardContent className="space-y-3">
            {member.company ? <InfoRow icon={<Building2 size={14} />} label="Company" value={member.company} /> : <p className="text-sm text-muted-foreground">Not provided</p>}
            {member.jobTitle && <InfoRow icon={<Briefcase size={14} />} label="Job Title" value={member.jobTitle} />}
            {member.location && <InfoRow icon={<MapPin size={14} />} label="Location" value={member.location} />}
            {member.linkedInUrl && <InfoRow icon={<LinkIcon size={14} />} label="LinkedIn" value={member.linkedInUrl} isLink />}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">Membership</CardTitle></CardHeader>
          <CardContent className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-[12px] text-muted-foreground">Membership state</span>
              <Badge variant={member.isMembershipActive ? "success" : "warning"}>
                {member.isMembershipActive ? "Active" : "Inactive"}
              </Badge>
            </div>
            {!duesRequired && (
              <p className="text-[11px] text-muted-foreground">
                This institution&apos;s active-member policy doesn&apos;t require dues — approved members are active regardless of payment.
              </p>
            )}
            {duesRequired && member.membershipYearsPaid != null && member.membershipYearsPaid > 0 && (
              <div className="flex items-center justify-between">
                <span className="text-[12px] text-muted-foreground">Years paid</span>
                <span className="text-sm font-semibold">{member.membershipYearsPaid}</span>
              </div>
            )}
            {duesRequired && member.status === "Active" && membershipCampaigns.length > 0 && (
              <>
                <Button size="sm" className="w-full" onClick={() => { setSelectedYears([]); setModal({ type: "activate-membership" }); }}>
                  <CreditCard size={13} />Activate membership
                </Button>
                <p className="text-[11px] text-muted-foreground">Use for offline payments or migration backfill.</p>
              </>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Bio */}
      {member.bio && (
        <Card>
          <CardHeader><CardTitle className="text-base">Bio</CardTitle></CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground whitespace-pre-wrap">{member.bio}</p>
          </CardContent>
        </Card>
      )}

      {/* History and notes */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card className="md:col-span-2">
          <CardHeader><CardTitle className="text-base">History and notes</CardTitle></CardHeader>
          <CardContent className="space-y-3">
            <div className="border-l-2 border-border pl-3">
              <p className="text-sm font-semibold">Account created</p>
              <p className="text-[12px] text-muted-foreground">System &middot; {formatDate(member.createdAt)} &middot; Registration submitted</p>
            </div>
            {member.isEmailVerified && (
              <div className="border-l-2 border-border pl-3">
                <p className="text-sm font-semibold">Email verified</p>
                <p className="text-[12px] text-muted-foreground">System &middot; {formatDate(member.createdAt)}</p>
              </div>
            )}
            {member.status === "Active" && member.memberNumber && (
              <div className="border-l-2 border-border pl-3">
                <p className="text-sm font-semibold">Profile approved</p>
                <p className="text-[12px] text-muted-foreground">Assigned member number {member.memberNumber}</p>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">Staff notes</CardTitle></CardHeader>
          <CardContent className="space-y-2">
            {(member.rejectionCount ?? 0) > 0 && (
              <p className="text-sm"><span className="font-medium text-warning">Rejections:</span> {member.rejectionCount}/3</p>
            )}
            {member.banReason && (
              <p className="text-sm"><span className="font-medium text-destructive">Ban reason:</span> {member.banReason}</p>
            )}
            {!(member.rejectionCount ?? 0) && !member.banReason && (
              <p className="text-[13px] text-muted-foreground">No moderation flags on this account.</p>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Modals */}
      <ConfirmModal
        open={modal?.type === "approve"}
        title="Approve Member"
        message={`Approve ${fullName}? They will be granted active access and assigned a member number.`}
        confirmLabel="Approve"
        variant="default"
        isLoading={approveMut.isPending}
        onConfirm={() => approveMut.mutate()}
        onCancel={() => setModal(null)}
      />
      <ConfirmModal
        open={modal?.type === "reject"}
        title="Reject Registration"
        message={`Reject ${fullName}? After 3 rejections their account will be permanently blocked.`}
        confirmLabel="Reject"
        variant="destructive"
        isLoading={rejectMut.isPending}
        onConfirm={() => rejectMut.mutate()}
        onCancel={() => { setModal(null); setReasonText(""); }}
      >
        <Textarea placeholder="Reason for rejection (optional)" rows={2} value={reasonText} onChange={(e) => setReasonText(e.target.value)} />
      </ConfirmModal>
      <ConfirmModal
        open={modal?.type === "ban"}
        title="Ban Member"
        message={`Ban ${fullName}? They will be prevented from logging in.`}
        confirmLabel="Ban Member"
        variant="destructive"
        isLoading={banMut.isPending}
        onConfirm={() => banMut.mutate()}
        onCancel={() => { setModal(null); setReasonText(""); }}
      >
        <Textarea placeholder="Reason for ban (optional)" rows={2} value={reasonText} onChange={(e) => setReasonText(e.target.value)} />
      </ConfirmModal>
      <ConfirmModal
        open={modal?.type === "unban"}
        title="Unban Member"
        message={`Restore access for ${fullName}? Their status will be set to Active.`}
        confirmLabel="Unban"
        variant="default"
        isLoading={unbanMut.isPending}
        onConfirm={() => unbanMut.mutate()}
        onCancel={() => setModal(null)}
      />
      <ConfirmModal
        open={modal?.type === "activate-membership"}
        title="Activate Membership"
        message={`Select membership years to mark as paid for ${fullName}.`}
        confirmLabel={`Activate ${selectedYears.length} year(s)`}
        variant="default"
        isLoading={activateMut.isPending}
        onConfirm={() => activateMut.mutate()}
        onCancel={() => { setModal(null); setSelectedYears([]); }}
      >
        <div className="space-y-2 max-h-48 overflow-y-auto">
          {membershipCampaigns.map((c) => (
            <label key={c.id} className="flex items-center gap-2 cursor-pointer rounded-md border border-border/40 px-3 py-2 hover:bg-muted/30">
              <input
                type="checkbox"
                checked={selectedYears.includes(c.membershipYear!)}
                onChange={(e) => {
                  const year = c.membershipYear!;
                  setSelectedYears((prev) => e.target.checked ? [...prev, year] : prev.filter((y) => y !== year));
                }}
                className="rounded"
              />
              <span className="text-sm font-medium">{c.membershipYear}</span>
              <span className="text-xs text-muted-foreground">— {c.title}</span>
            </label>
          ))}
        </div>
      </ConfirmModal>
    </div>
  );
}

function InfoRow({ icon, label, value, isLink }: { icon: React.ReactNode; label: string; value: string; isLink?: boolean }) {
  return (
    <div className="flex items-center gap-3">
      <span className="text-muted-foreground">{icon}</span>
      <div>
        <p className="text-xs text-muted-foreground">{label}</p>
        {isLink ? (
          <a href={value} target="_blank" rel="noopener noreferrer" className="text-sm text-primary hover:underline">{value}</a>
        ) : (
          <p className="text-sm font-medium">{value}</p>
        )}
      </div>
    </div>
  );
}
