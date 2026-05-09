"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, CheckCircle, XCircle, ShieldBan, ShieldCheck, MailCheck, MailX, MapPin, Building2, Briefcase, GraduationCap, Calendar, Link as LinkIcon, Smartphone, CreditCard } from "lucide-react";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { UserAvatar } from "@/components/ui/user-avatar";
import { Textarea } from "@/components/ui/textarea";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { formatDate } from "@/lib/utils";
import { getMember, approveMember, rejectMember, banMember, unbanMember, activateMembership, getCampaigns } from "@/lib/admin-api";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
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
  const qc = useQueryClient();

  const { data: member, isLoading } = useQuery({
    queryKey: ["admin-member", id],
    queryFn: () => getMember(id),
  });

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

  if (isLoading) return <div className="p-6 lg:p-8 space-y-6 page-enter"><CardSkeleton /><CardSkeleton /></div>;
  if (!member) return (
    <div className="p-6 lg:p-8">
      <Link href="/members" className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground mb-6">
        <ArrowLeft size={14} /> Back to Members
      </Link>
      <EmptyState icon={<XCircle size={48} />} title="Member not found" description="This member profile doesn't exist or may have been removed." />
    </div>
  );

  const fullName = `${member.firstName} ${member.lastName}`;

  return (
    <div className="p-6 lg:p-8 space-y-6 page-enter">
      <div className="flex items-center gap-3">
        <Link href="/members">
          <Button size="sm" variant="ghost"><ArrowLeft size={14} />Back</Button>
        </Link>
        <div>
          <h1 className="text-[22px] font-bold tracking-tight">Member Details</h1>
          <p className="text-muted-foreground text-[13px]">View and manage member information</p>
        </div>
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
                  ? <span className="flex items-center gap-1 text-xs text-green-600"><MailCheck size={12} />Verified</span>
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

      {/* Details grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Personal Info */}
        <Card>
          <CardHeader><CardTitle className="text-base">Personal Information</CardTitle></CardHeader>
          <CardContent className="space-y-3">
            <InfoRow icon={<GraduationCap size={14} />} label="Graduation Year" value={String(member.graduationYear)} />
            <InfoRow icon={<Calendar size={14} />} label="Joined" value={formatDate(member.createdAt)} />
            {member.phone && <InfoRow icon={<Smartphone size={14} />} label="Phone" value={member.phone} />}
          </CardContent>
        </Card>

        {/* Professional Info */}
        <Card>
          <CardHeader><CardTitle className="text-base">Professional Information</CardTitle></CardHeader>
          <CardContent className="space-y-3">
            {member.company ? <InfoRow icon={<Building2 size={14} />} label="Company" value={member.company} /> : <p className="text-sm text-muted-foreground">Not provided</p>}
            {member.jobTitle && <InfoRow icon={<Briefcase size={14} />} label="Job Title" value={member.jobTitle} />}
            {member.location && <InfoRow icon={<MapPin size={14} />} label="Location" value={member.location} />}
            {member.linkedInUrl && <InfoRow icon={<LinkIcon size={14} />} label="LinkedIn" value={member.linkedInUrl} isLink />}
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

      {/* Membership Activation */}
      {member.status === "Active" && membershipCampaigns.length > 0 && (
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle className="text-base">Membership</CardTitle>
              <Button size="sm" className="gap-1" onClick={() => { setSelectedYears([]); setModal({ type: "activate-membership" }); }}>
                <CreditCard size={13} />Activate Membership
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-2">
            <div className="text-sm flex flex-wrap items-center gap-2">
              <span className="font-medium">Status:</span>
              <Badge variant={member.isMembershipActive ? "success" : "warning"}>
                {member.isMembershipActive ? "Active" : "Inactive"}
              </Badge>
            </div>
            {member.membershipYearsPaid != null && member.membershipYearsPaid > 0 && (
              <p className="text-sm"><span className="font-medium">Years Paid:</span> {member.membershipYearsPaid}</p>
            )}
          </CardContent>
        </Card>
      )}

      {/* Admin info */}
      {((member.rejectionCount ?? 0) > 0 || member.banReason) && (
        <Card>
          <CardHeader><CardTitle className="text-base text-destructive">Admin Notes</CardTitle></CardHeader>
          <CardContent className="space-y-2">
            {(member.rejectionCount ?? 0) > 0 && (
              <p className="text-sm"><span className="font-medium text-orange-600">Rejections:</span> {member.rejectionCount}/3</p>
            )}
            {member.banReason && (
              <p className="text-sm"><span className="font-medium text-destructive">Ban Reason:</span> {member.banReason}</p>
            )}
          </CardContent>
        </Card>
      )}

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
