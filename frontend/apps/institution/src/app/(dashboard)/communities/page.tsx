"use client";

import { ReadMore } from "@alumni/ui";
import { shareMessages } from "@alumni/ui";
import { richTextToPlain } from "@alumni/ui";
import { RichTextEditor, CheckOption } from "@alumni/ui";
import { LoadError } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Crown, Users } from "@alumni/ui";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { FormSheet, FormSection } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { TableSkeleton, Skeleton } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { formatDate, cn } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { FormError } from "@alumni/ui";
import { SettlementAccountFields, type SettlementAccountValue } from "@alumni/ui";
import {
  getCommunities, getCommunityGrowth, createCommunity, updateCommunity, getCommunityMembers, setCommunityMemberRole, approveCommunityRequest, rejectCommunityRequest, removeCommunityMember,
  uploadImage, submitCommunityPayoutSetup, getBatchPayoutBanks, resolveBatchPayoutAccount, type CommunityListItem, type CommunityMemberItem,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { LinkOrUpload } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { MemberShareButton } from "@/components/institution/member-share-button";

const SOURCE_LABEL: Record<string, string> = { whatsapp: "Chat message", link: "Shared link", qr: "QR code", sms: "SMS", email: "Email" };

/** How members have been joining through leaders' invitations. Quiet when there is nothing yet. */
function GrowthCard() {
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["community-growth"], queryFn: getCommunityGrowth });

  if (isLoading) {
    return (
      <Card aria-busy="true" aria-label="Loading invitation growth"><CardContent className="space-y-4 p-5">
        <div className="space-y-2"><Skeleton className="h-4 w-44" /><Skeleton className="h-3 w-72 max-w-full" /></div>
        <div className="flex gap-10"><Skeleton className="h-9 w-16" /><Skeleton className="h-9 w-16" /></div>
      </CardContent></Card>
    );
  }
  if (isError || !data) return <Card><CardContent className="p-2"><LoadError title="We couldn't load invitation growth" onRetry={() => refetch()} /></CardContent></Card>;

  return (
    <Card>
      <CardContent className="space-y-5 p-5">
        <div>
          <h2 className="text-[15px] font-semibold">Joining through invitations</h2>
          <p className="mt-1 text-[13px] text-muted-foreground">When a group leader shares their invitation with their group, the people who join through it are counted here.</p>
        </div>
        {data.totalViaInvites === 0 ? (
          <div className="border border-dashed border-border px-4 py-5 text-[13.5px] text-muted-foreground">
            <p className="font-semibold text-foreground">No one has joined through an invitation yet.</p>
            <p className="mt-1">Make someone a Leader of a group and ask them to open it. They will find an invitation to share with their group.</p>
          </div>
        ) : (
          <div className="grid gap-6 md:grid-cols-[auto_1fr] md:gap-12">
            <dl className="flex gap-10">
              <div><dd className="text-[26px] font-bold tabular-nums leading-none">{data.totalViaInvites}</dd><dt className="mt-1.5 text-[12px] text-muted-foreground">Joined in total</dt></div>
              <div><dd className="text-[26px] font-bold tabular-nums leading-none">{data.last30Days}</dd><dt className="mt-1.5 text-[12px] text-muted-foreground">Last 30 days</dt></div>
            </dl>
            <div className="grid gap-6 sm:grid-cols-2">
              <div>
                <p className="text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">Where they came from</p>
                <ul className="mt-2 divide-y divide-border/60">
                  {data.bySource.map((s) => (
                    <li key={s.source} className="flex justify-between py-1.5 text-[13.5px]"><span>{SOURCE_LABEL[s.source] ?? s.source}</span><span className="tabular-nums font-semibold">{s.count}</span></li>
                  ))}
                </ul>
              </div>
              <div>
                <p className="text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">By group</p>
                <ul className="mt-2 divide-y divide-border/60">
                  {data.byCommunity.slice(0, 5).map((c) => (
                    <li key={c.communityId} className="flex justify-between gap-3 py-1.5 text-[13.5px]"><span className="truncate">{c.name}</span><span className="tabular-nums font-semibold">{c.joined}</span></li>
                  ))}
                </ul>
              </div>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function CommunityForm({
  id, initial, onSave, onUploadingChange,
}: {
  id: string;
  initial?: CommunityListItem;
  onUploadingChange?: (v: boolean) => void;
  onSave: (data: { name: string; kind?: string; description?: string; coverImageUrl?: string; isPublic: boolean }) => void;
}) {
  const [name, setName] = useState(initial?.name ?? "");
  const [kind, setKind] = useState(initial?.kind ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [coverImageUrl, setCoverImageUrl] = useState(initial?.coverImageUrl ?? "");
  const [isPublic, setIsPublic] = useState(!!initial?.isPublic);
  const [coverImageFile, setCoverImageFile] = useState<File | null>(null);
  const [uploading, setUploadingState] = useState(false);
  const setUploading = (v: boolean) => { setUploadingState(v); onUploadingChange?.(v); };

  return (
    <form
          id={id}
          className="space-y-5"
          onSubmit={async (e) => {
            e.preventDefault();
            let resolvedCoverUrl = coverImageUrl.trim();
            if (coverImageFile) {
              setUploading(true);
              try {
                const { url } = await uploadImage(coverImageFile);
                resolvedCoverUrl = url;
              } catch (err) {
                toast.error(handleApiError(err));
                setUploading(false);
                return;
              }
              setUploading(false);
            }
            onSave({ name: name.trim(), kind: kind.trim(), description: description.trim() || undefined, coverImageUrl: resolvedCoverUrl || undefined, isPublic });
          }}
        >
          <FormSection title="The group">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Kumasi Chapter" required />
            </div>
            <div className="space-y-2">
              <Label>What kind of group is this? <span className="font-normal text-muted-foreground">(optional)</span></Label>
              <Input value={kind} onChange={(e) => setKind(e.target.value)} placeholder="e.g. Chapter, Committee, Department, Team" maxLength={40} list="group-kinds" />
              <datalist id="group-kinds">
                {["Chapter", "Committee", "Department", "Ministry", "Team", "Class", "Region", "Interest group"].map((k) => <option key={k} value={k} />)}
              </datalist>
            </div>
            <div className="space-y-2">
              <Label>Description <span className="font-normal text-muted-foreground">(optional)</span></Label>
              <RichTextEditor variant="compact" placeholder="What this group is for." minHeight={110} value={description} onChange={setDescription} />
            </div>
            <LinkOrUpload
              label="Cover image (optional)"
              url={coverImageUrl}
              onUrlChange={setCoverImageUrl}
              file={coverImageFile}
              onFileChange={setCoverImageFile}
            />
            <CheckOption
              id="community-public"
              checked={isPublic}
              onChange={setIsPublic}
              label="Show on the public website"
              hint="Visitors see the group's name, description, cover image and how many members it has, never who they are. Joining still needs an account."
            />
          </FormSection>
        </form>
  );
}

type MembersTab = "requests" | "members" | "declined";

/** "3 days ago" for a recent date, the date itself after a month. */
function ago(iso: string): string {
  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000);
  if (days < 1) return "today";
  if (days === 1) return "yesterday";
  if (days < 30) return `${days} days ago`;
  return formatDate(iso);
}

function MembersPanel({ community, onClose }: { community: CommunityListItem; onClose: () => void }) {
  const qc = useQueryClient();
  const { data: members = [], isLoading, isError, refetch } = useQuery({
    queryKey: ["community-members", community.id],
    queryFn: () => getCommunityMembers(community.id),
  });

  const requests = members.filter((m) => m.status === "Pending");
  const approved = members.filter((m) => m.status === "Approved");
  const declined = members.filter((m) => m.status === "Rejected");

  // Open on the requests when there are any: that is what an administrator opens this for.
  const [pickedTab, setTab] = useState<MembersTab | null>(null);
  const tab: MembersTab = pickedTab ?? (requests.length > 0 ? "requests" : "members");
  const [search, setSearch] = useState("");

  // What "Promote to leader" and "Demote to member" actually do is easy to
  // misread as granting institution-admin access: it doesn't. It only
  // flips a per-community flag that unlocks a couple of Member Portal
  // actions (approving join requests, removing members) for that one
  // community. Confirming with that spelled out is the fix for staff not
  // being sure what the button does, rather than a silent one-click toggle.
  const [promoteTarget, setPromoteTarget] = useState<CommunityMemberItem | null>(null);
  const [demoteTarget, setDemoteTarget] = useState<CommunityMemberItem | null>(null);
  const [declineTarget, setDeclineTarget] = useState<CommunityMemberItem | null>(null);
  const [removeTarget, setRemoveTarget] = useState<CommunityMemberItem | null>(null);
  const [reason, setReason] = useState("");

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["community-members", community.id] });
    qc.invalidateQueries({ queryKey: ["communities"] });
  };

  const roleMut = useMutation({
    mutationFn: ({ memberId, role }: { memberId: string; role: "Member" | "Leader" }) => setCommunityMemberRole(community.id, memberId, role),
    onSuccess: (_data, variables) => {
      refresh();
      toast.success(variables.role === "Leader" ? "Promoted to leader" : "Role updated");
      setPromoteTarget(null);
      setDemoteTarget(null);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const approveMut = useMutation({
    mutationFn: (m: CommunityMemberItem) => approveCommunityRequest(community.id, m.memberId),
    onSuccess: (_d, m) => { refresh(); toast.success(`${m.memberName} is in. They have been told.`); },
    // Someone else may have decided it a moment ago; showing the fresh list explains why.
    onError: (e) => { toast.error(handleApiError(e)); refresh(); },
  });
  const declineMut = useMutation({
    mutationFn: (m: CommunityMemberItem) => rejectCommunityRequest(community.id, m.memberId, reason),
    onSuccess: (_d, m) => { refresh(); toast.success(`Request from ${m.memberName} declined.`); setDeclineTarget(null); setReason(""); },
    onError: (e) => { toast.error(handleApiError(e)); refresh(); setDeclineTarget(null); },
  });
  const removeMut = useMutation({
    mutationFn: (m: CommunityMemberItem) => removeCommunityMember(community.id, m.memberId, reason),
    onSuccess: (_d, m) => { refresh(); toast.success(`${m.memberName} was removed from ${community.name}.`); setRemoveTarget(null); setReason(""); },
    onError: (e) => { toast.error(handleApiError(e)); refresh(); setRemoveTarget(null); },
  });

  const needle = search.trim().toLowerCase();
  const matches = (m: CommunityMemberItem) => !needle || m.memberName.toLowerCase().includes(needle) || m.memberEmail.toLowerCase().includes(needle);
  const shown = (tab === "requests" ? requests : tab === "members" ? approved : declined).filter(matches);

  const tabs: { key: MembersTab; label: string; count: number }[] = [
    { key: "requests", label: "Requests", count: requests.length },
    { key: "members", label: "Members", count: approved.length },
    ...(declined.length > 0 ? [{ key: "declined" as const, label: "Declined", count: declined.length }] : []),
  ];

  return (
    <Card>
      <div className="px-4 py-3.5 border-b border-border">
        <div className="flex items-center justify-between">
          <b className="text-[13.5px]">{community.name}</b>
          <Button size="sm" variant="outline" onClick={onClose}>Close</Button>
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-2" role="group" aria-label="Show">
          {tabs.map((t) => (
            <button key={t.key} type="button" aria-pressed={tab === t.key} onClick={() => { setTab(t.key); setSearch(""); }}
              className={cn("min-h-10 border px-3.5 text-[13px] font-semibold transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary",
                tab === t.key ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card hover:border-primary/50")}>
              {t.label}
              <span className={cn("ml-2 tabular-nums", tab === t.key ? "text-primary-foreground/80" : "text-muted-foreground")}>{t.count}</span>
            </button>
          ))}
          {(tab !== "requests" || requests.length > 6) && (members.length > 8) && (
            <Input className="ml-auto h-10 w-full sm:w-56" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search by name or email" aria-label="Search this group" />
          )}
        </div>
        {tab === "members" && (
          <p className="text-[12px] text-muted-foreground mt-3">
            A <b>Leader</b> is still an ordinary member (same login, same Member Portal): the role just unlocks approving join requests and removing members for this one community, from their own Member Portal. It doesn&apos;t create an admin account or grant any access here in the Institution Portal.
          </p>
        )}
        {tab === "declined" && (
          <p className="text-[12px] text-muted-foreground mt-3">These people asked to join and were told no. You can still let one in if you change your mind, and they can ask again themselves.</p>
        )}
      </div>
      <CardContent className="p-0">
        {isError ? (
          <LoadError className="py-8" onRetry={() => refetch()} />
        ) : isLoading ? (
          <div className="p-4 space-y-3">{Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-14 w-full" />)}</div>
        ) : shown.length === 0 ? (
          needle ? (
            <EmptyState className="py-8" title="No one matches that" description="Try part of a name or an email address." />
          ) : tab === "requests" ? (
            <EmptyState className="py-8" title="No requests waiting" description="When someone asks to join this group, they appear here for you to approve or decline. They are told either way." />
          ) : tab === "members" ? (
            <EmptyState className="py-8" title="No one has joined yet" description="Approved members appear here. Make someone a Leader to let them look after requests themselves." />
          ) : (
            <EmptyState className="py-8" title="No declined requests" description="Requests you decline are kept here." />
          )
        ) : (
          <ul className="divide-y divide-border">
            {shown.map((m) => (
              <li key={m.membershipId} className="flex flex-col gap-3 px-4 py-3.5 sm:flex-row sm:items-center sm:justify-between">
                <div className="min-w-0">
                  <p className="flex flex-wrap items-center gap-2 font-medium">
                    <span className="truncate">{m.memberName}</span>
                    {m.role === "Leader" && m.status === "Approved" && <Badge variant="info" className="gap-1"><Crown size={11} /> Leader</Badge>}
                  </p>
                  <p className="truncate text-[12px] text-muted-foreground">{m.memberEmail}</p>
                  <p className="text-[12px] text-muted-foreground">{tab === "members" ? "Asked to join " : "Asked "}{ago(m.requestedAt)}</p>
                </div>
                <div className="flex flex-wrap items-center gap-2 sm:justify-end">
                  {tab === "requests" && (
                    <>
                      <Button size="sm" onClick={() => approveMut.mutate(m)} isLoading={approveMut.isPending && approveMut.variables?.membershipId === m.membershipId} disabled={declineMut.isPending}>Approve</Button>
                      <Button size="sm" variant="outline" onClick={() => { setReason(""); setDeclineTarget(m); }} disabled={approveMut.isPending}>Decline</Button>
                    </>
                  )}
                  {tab === "members" && (
                    <>
                      {m.role === "Leader" ? (
                        <Button size="sm" variant="outline" onClick={() => setDemoteTarget(m)}>Demote to member</Button>
                      ) : (
                        <Button size="sm" variant="secondary" onClick={() => setPromoteTarget(m)}>Promote to leader</Button>
                      )}
                      <Button size="sm" variant="ghost" className="text-destructive hover:text-destructive" onClick={() => { setReason(""); setRemoveTarget(m); }}>Remove</Button>
                    </>
                  )}
                  {tab === "declined" && (
                    <Button size="sm" variant="secondary" onClick={() => approveMut.mutate(m)} isLoading={approveMut.isPending && approveMut.variables?.membershipId === m.membershipId}>Approve instead</Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </CardContent>

      <ConfirmModal
        open={!!declineTarget}
        variant="default"
        title={`Decline ${declineTarget?.memberName ?? "this request"}?`}
        message={`They will be told their request to join "${community.name}" was not approved. You can add a short reason, which they will see.`}
        confirmLabel="Decline request"
        isLoading={declineMut.isPending}
        onConfirm={() => declineTarget && declineMut.mutate(declineTarget)}
        onCancel={() => { setDeclineTarget(null); setReason(""); }}
      >
        <Label htmlFor="decline-reason" className="text-[13px]">Reason <span className="font-normal text-muted-foreground">(optional)</span></Label>
        <Textarea id="decline-reason" rows={3} maxLength={300} value={reason} onChange={(e) => setReason(e.target.value)} className="mt-1.5 resize-none" placeholder="e.g. This group is for members of the 2018 cohort." />
      </ConfirmModal>
      <ConfirmModal
        open={!!removeTarget}
        title={`Remove ${removeTarget?.memberName ?? "this member"}?`}
        message={`They lose access to "${community.name}" and what is shared in it, and they are told.${removeTarget?.role === "Leader" ? " They are a Leader, so they also lose that role." : ""} They can ask to join again later.`}
        confirmLabel="Remove from group"
        isLoading={removeMut.isPending}
        onConfirm={() => removeTarget && removeMut.mutate(removeTarget)}
        onCancel={() => { setRemoveTarget(null); setReason(""); }}
      >
        <Label htmlFor="remove-reason" className="text-[13px]">Reason <span className="font-normal text-muted-foreground">(optional, they will see it)</span></Label>
        <Textarea id="remove-reason" rows={3} maxLength={300} value={reason} onChange={(e) => setReason(e.target.value)} className="mt-1.5 resize-none" />
      </ConfirmModal>
      <ConfirmModal
        open={!!promoteTarget}
        variant="default"
        title="Promote to leader?"
        message={`${promoteTarget?.memberName} will be able to approve or reject join requests and remove members in "${community.name}", from their own Member Portal login: no new account, no access to this Institution Portal. You can demote them back to a regular member at any time.`}
        confirmLabel="Promote"
        isLoading={roleMut.isPending}
        onConfirm={() => promoteTarget && roleMut.mutate({ memberId: promoteTarget.memberId, role: "Leader" })}
        onCancel={() => setPromoteTarget(null)}
      />
      <ConfirmModal
        open={!!demoteTarget}
        variant="default"
        title="Demote to member?"
        message={`${demoteTarget?.memberName} will lose the ability to approve join requests or remove members in "${community.name}". They stay a regular member of the community either way.`}
        confirmLabel="Demote"
        isLoading={roleMut.isPending}
        onConfirm={() => demoteTarget && roleMut.mutate({ memberId: demoteTarget.memberId, role: "Member" })}
        onCancel={() => setDemoteTarget(null)}
      />
    </Card>
  );
}

const payoutStatusVariant: Record<CommunityListItem["payoutStatus"], "neutral" | "warning" | "success" | "destructive"> = {
  None: "neutral",
  Pending: "warning",
  Approved: "success",
  Rejected: "destructive",
};

/** Where the money for this group's fundraisers and paid events goes. Needs platform approval, so nothing changes until then. */
function PayoutSetupModal({ group, onClose }: { group: CommunityListItem; onClose: () => void }) {
  const [useOwn, setUseOwn] = useState(!group.useInstitutionAccount);
  const [value, setValue] = useState<SettlementAccountValue>({
    settlementBankCode: "",
    settlementBankName: group.settlementBankName ?? "",
    settlementAccountNumber: group.settlementAccountNumber ?? "",
    settlementAccountName: group.settlementAccountName ?? "",
  });
  const [formError, setFormError] = useState<string | null>(null);
  const qc = useQueryClient();
  const submitMut = useMutation({
    onMutate: () => setFormError(null),
    mutationFn: () => submitCommunityPayoutSetup(group.id, {
      useInstitutionAccount: !useOwn,
      settlementBankCode: useOwn ? value.settlementBankCode : undefined,
      settlementBankName: useOwn ? value.settlementBankName : undefined,
      settlementAccountNumber: useOwn ? value.settlementAccountNumber : undefined,
      settlementAccountName: useOwn ? value.settlementAccountName : undefined,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["communities"] }); toast.success("Submitted for platform review"); onClose(); },
    onError: (e) => { const message = handleApiError(e); setFormError(message); toast.error(message); },
  });

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader><DialogTitle>Payout setup: {group.name}</DialogTitle></DialogHeader>
        <div className="space-y-4">
          <p className="text-[13px] text-muted-foreground">
            Choose where money paid to this group goes: its fundraisers and its paid event tickets. It can settle into the institution's account, or into one of its own. Either way, the platform approves it first. Until then, this group keeps using the institution's account.
          </p>
          <div className="flex flex-wrap gap-2">
            <Button type="button" size="sm" variant={!useOwn ? "default" : "outline"} onClick={() => setUseOwn(false)}>Use institution's account</Button>
            <Button type="button" size="sm" variant={useOwn ? "default" : "outline"} onClick={() => setUseOwn(true)}>This group's own account</Button>
          </div>
          {useOwn && (
            <SettlementAccountFields value={value} onChange={setValue} getBanks={getBatchPayoutBanks} resolveAccount={resolveBatchPayoutAccount} />
          )}
        </div>
        <FormError message={formError} className="mt-3" />
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button
            disabled={useOwn && (!value.settlementBankCode || !value.settlementAccountNumber || !value.settlementAccountName)}
            isLoading={submitMut.isPending}
            onClick={() => submitMut.mutate()}
          >
            Submit for review
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function CommunitiesPage() {
  const { user } = useAuth();
  const [payoutTarget, setPayoutTarget] = useState<CommunityListItem | null>(null);
  const canSetUpPayout = (c: CommunityListItem) => user?.role === "SuperAdmin" || (user?.role === "ScopedAdmin" && !!user.communityIds?.includes(c.id));
  const [showCreate, setShowCreate] = useState(false);
  const [editing, setEditing] = useState<CommunityListItem | null>(null);
  const [uploadingCover, setUploadingCover] = useState(false);
  const [viewingMembers, setViewingMembers] = useState<CommunityListItem | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<CommunityListItem | null>(null);
  const qc = useQueryClient();

  const { data: communities = [], isLoading, isError, refetch } = useQuery({
    queryKey: ["communities"],
    queryFn: getCommunities,
  });

  const createMut = useMutation({
    mutationFn: createCommunity,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["communities"] }); setShowCreate(false); toast.success("Community created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, ...body }: { id: string; name: string; kind?: string; description?: string; coverImageUrl?: string; isActive: boolean; isPublic?: boolean }) => updateCommunity(id, body),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["communities"] }); qc.invalidateQueries({ queryKey: ["community-growth"] }); setEditing(null); setDeactivateTarget(null); toast.success("Community updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const toggleActive = (c: CommunityListItem) => updateMut.mutate({ id: c.id, name: c.name, description: c.description ?? undefined, coverImageUrl: c.coverImageUrl ?? undefined, isActive: !c.isActive });

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <header className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Groups</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">
            Sub-groups inside your community network. Promote an approved member to Leader so they can manage day-to-day requests and moderation themselves.
          </p>
        </div>
        <Button onClick={() => setShowCreate(true)}>
          <Plus size={16} />Create group
        </Button>
      </header>

      <FormSheet
        open={showCreate || editing !== null}
        onClose={() => { setShowCreate(false); setEditing(null); setUploadingCover(false); }}
        title={editing ? "Edit group" : "Create group"}
        description={editing ? editing.name : "A chapter, committee, team or interest group. Members can ask to join and you approve them."}
        formId="community-form"
        submitLabel={editing ? "Save changes" : "Create group"}
        saving={createMut.isPending || updateMut.isPending || uploadingCover}
        size="lg"
      >
        {editing ? (
          <CommunityForm
            key={editing.id}
            id="community-form"
            initial={editing}
            onUploadingChange={setUploadingCover}
            onSave={(d) => updateMut.mutate({ id: editing.id, ...d, isActive: editing.isActive })}
          />
        ) : showCreate ? (
          <CommunityForm id="community-form" onUploadingChange={setUploadingCover} onSave={(d) => createMut.mutate(d)} />
        ) : null}
      </FormSheet>

      {viewingMembers && <MembersPanel community={viewingMembers} onClose={() => setViewingMembers(null)} />}

      <GrowthCard />

      <Card className="overflow-hidden">
        <div className="px-4 py-3.5 border-b border-border flex items-center justify-between">
          <b className="text-[13.5px]">All communities</b>
          <span className="text-[12.5px] text-muted-foreground">{communities.length} total</span>
        </div>
        <CardContent className="p-0">
          <Table stackOnMobile className="min-w-[760px]">
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Members</TableHead>
                <TableHead>Pending</TableHead>
                <TableHead>Leaders</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Payout</TableHead>
                <TableHead>Created</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {isError ? (
                <TableRow><TableCell colSpan={8}><LoadError className="py-8" onRetry={() => refetch()} /></TableCell></TableRow>
              ) : isLoading ? (
                <TableSkeleton rows={4} cols={8} />
              ) : communities.length === 0 ? (
                <TableRow><TableCell colSpan={8}><EmptyState className="py-8" title="Groups bring together members who share something" description="Create a group for a chapter, a committee, a team or a year group. Members can ask to join, and you approve them here." /></TableCell></TableRow>
              ) : communities.map((c) => (
                <TableRow key={c.id}>
                  <TableCell>
                    <div className="flex items-center gap-3">
                      {c.coverImageUrl ? (
                        <img src={c.coverImageUrl} alt="" className="w-10 h-10 rounded-lg object-cover shrink-0" />
                      ) : (
                        <div className="w-10 h-10 rounded-lg shrink-0 flex items-center justify-center bg-secondary">
                          <Users size={16} className="text-muted-foreground opacity-40" />
                        </div>
                      )}
                      <div className="min-w-0">
                        <p className="font-medium truncate">{c.name}{c.kind && <span className="ml-2 align-middle rounded-full bg-secondary px-2 py-0.5 text-[11px] font-medium text-muted-foreground">{c.kind}</span>}</p>
                        {c.description && <ReadMore lines={2} className="text-[12px] text-muted-foreground">{richTextToPlain(c.description)}</ReadMore>}
                        {(c.joinedViaInvites ?? 0) > 0 && (
                          <p className="mt-0.5 text-[12px] text-muted-foreground">{c.joinedViaInvites} joined by invitation</p>
                        )}
                      </div>
                    </div>
                  </TableCell>
                  <TableCell>{c.approvedCount}</TableCell>
                  <TableCell>{c.pendingCount > 0 ? <Badge variant="warning">{c.pendingCount}</Badge> : 0}</TableCell>
                  <TableCell>{c.leaderCount}</TableCell>
                  <TableCell><Badge variant={c.isActive ? "success" : "neutral"}>{c.isActive ? "Active" : "Inactive"}</Badge></TableCell>
                  <TableCell>
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant={payoutStatusVariant[c.payoutStatus]}>{c.payoutStatus === "None" ? "Institution account" : c.payoutStatus}</Badge>
                      {canSetUpPayout(c) && (
                        <Button size="sm" variant="outline" onClick={() => setPayoutTarget(c)}>{c.payoutStatus === "None" ? "Set up" : "Edit"}</Button>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>{formatDate(c.createdAt)}</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2 justify-end">
                      {c.isActive && <MemberShareButton memberPath={`/communities/${c.id}`} title={c.name} message={shareMessages.community({ name: c.name, description: c.description })} />}
                      <Button size="sm" variant="outline" onClick={() => setViewingMembers(c)}>Members</Button>
                      <Button size="sm" variant="outline" onClick={() => setEditing(c)}>Edit</Button>
                      <Button
                        size="sm"
                        variant={c.isActive ? "destructive" : "secondary"}
                        onClick={() => c.isActive ? setDeactivateTarget(c) : toggleActive(c)}
                        isLoading={updateMut.isPending}
                      >
                        {c.isActive ? "Deactivate" : "Activate"}
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <ConfirmModal
        open={!!deactivateTarget}
        title="Deactivate this community?"
        message={`"${deactivateTarget?.name ?? ""}" will be hidden from members and no longer accept new join requests. You can reactivate it later.`}
        confirmLabel="Deactivate"
        variant="destructive"
        isLoading={updateMut.isPending}
        onConfirm={() => { if (deactivateTarget) toggleActive(deactivateTarget); }}
        onCancel={() => setDeactivateTarget(null)}
      />
      {payoutTarget && <PayoutSetupModal group={payoutTarget} onClose={() => setPayoutTarget(null)} />}
    </div>
  );
}
