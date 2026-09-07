"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Crown, Users } from "@alumni/ui";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { TableSkeleton } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import {
  getCommunities, createCommunity, updateCommunity, getCommunityMembers, setCommunityMemberRole,
  type CommunityListItem, type CommunityMemberItem,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";

function CommunityForm({
  initial, onSave, onCancel, saving,
}: {
  initial?: CommunityListItem;
  onSave: (data: { name: string; description?: string; coverImageUrl?: string }) => void;
  onCancel: () => void;
  saving: boolean;
}) {
  const [name, setName] = useState(initial?.name ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [coverImageUrl, setCoverImageUrl] = useState(initial?.coverImageUrl ?? "");

  return (
    <Card>
      <CardHeader><CardTitle className="text-base">{initial ? "Edit community" : "Create community"}</CardTitle></CardHeader>
      <CardContent>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            onSave({ name: name.trim(), description: description.trim() || undefined, coverImageUrl: coverImageUrl.trim() || undefined });
          }}
        >
          <div className="space-y-2">
            <Label>Name</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Mining Engineering Alumni" required />
          </div>
          <div className="space-y-2">
            <Label>Description</Label>
            <Textarea rows={2} value={description} onChange={(e) => setDescription(e.target.value)} placeholder="What this community is for" />
          </div>
          <div className="space-y-2">
            <Label>Cover image URL (optional)</Label>
            <Input value={coverImageUrl} onChange={(e) => setCoverImageUrl(e.target.value)} placeholder="https://…" />
          </div>
          <div className="flex gap-3">
            <Button type="submit" size="sm" isLoading={saving} loadingText="Saving">{initial ? "Save changes" : "Create community"}</Button>
            <Button type="button" size="sm" variant="outline" onClick={onCancel}>Cancel</Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

function MembersPanel({ community, onClose }: { community: CommunityListItem; onClose: () => void }) {
  const qc = useQueryClient();
  const { data: members = [], isLoading } = useQuery({
    queryKey: ["community-members", community.id],
    queryFn: () => getCommunityMembers(community.id),
  });

  const roleMut = useMutation({
    mutationFn: ({ memberId, role }: { memberId: string; role: "Member" | "Leader" }) => setCommunityMemberRole(community.id, memberId, role),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["community-members", community.id] });
      qc.invalidateQueries({ queryKey: ["communities"] });
      toast.success("Role updated");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <Card>
      <div className="px-4 py-3.5 border-b border-border flex items-center justify-between">
        <b className="text-[13.5px]">{community.name} — members</b>
        <Button size="sm" variant="outline" onClick={onClose}>Close</Button>
      </div>
      <CardContent className="p-0">
        <Table className="min-w-[600px]">
          <TableHeader>
            <TableRow>
              <TableHead>Member</TableHead>
              <TableHead>Role</TableHead>
              <TableHead>Status</TableHead>
              <TableHead />
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableSkeleton rows={4} cols={4} />
            ) : members.length === 0 ? (
              <TableRow><TableCell colSpan={4} className="text-center py-8 text-muted-foreground">No requests or members yet</TableCell></TableRow>
            ) : members.map((m: CommunityMemberItem) => (
              <TableRow key={m.membershipId}>
                <TableCell>
                  <p className="font-medium">{m.memberName}</p>
                  <p className="text-[12px] text-muted-foreground">{m.memberEmail}</p>
                </TableCell>
                <TableCell>{m.role === "Leader" ? <Badge variant="info" className="gap-1"><Crown size={11} /> Leader</Badge> : "Member"}</TableCell>
                <TableCell>
                  <Badge variant={m.status === "Approved" ? "success" : m.status === "Pending" ? "warning" : "neutral"}>{m.status}</Badge>
                </TableCell>
                <TableCell>
                  {m.status === "Approved" && (
                    <div className="flex justify-end">
                      {m.role === "Leader" ? (
                        <Button size="sm" variant="outline" onClick={() => roleMut.mutate({ memberId: m.memberId, role: "Member" })} isLoading={roleMut.isPending}>Demote to member</Button>
                      ) : (
                        <Button size="sm" variant="secondary" onClick={() => roleMut.mutate({ memberId: m.memberId, role: "Leader" })} isLoading={roleMut.isPending}>Promote to leader</Button>
                      )}
                    </div>
                  )}
                  {m.status === "Pending" && (
                    <Button size="sm" variant="secondary" onClick={() => roleMut.mutate({ memberId: m.memberId, role: "Member" })} isLoading={roleMut.isPending}>Approve</Button>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function CommunitiesPage() {
  const [showCreate, setShowCreate] = useState(false);
  const [editing, setEditing] = useState<CommunityListItem | null>(null);
  const [viewingMembers, setViewingMembers] = useState<CommunityListItem | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<CommunityListItem | null>(null);
  const qc = useQueryClient();

  const { data: communities = [], isLoading } = useQuery({
    queryKey: ["communities"],
    queryFn: getCommunities,
  });

  const createMut = useMutation({
    mutationFn: createCommunity,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["communities"] }); setShowCreate(false); toast.success("Community created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, ...body }: { id: string; name: string; description?: string; coverImageUrl?: string; isActive: boolean }) => updateCommunity(id, body),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["communities"] }); setEditing(null); setDeactivateTarget(null); toast.success("Community updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const toggleActive = (c: CommunityListItem) => updateMut.mutate({ id: c.id, name: c.name, description: c.description ?? undefined, coverImageUrl: c.coverImageUrl ?? undefined, isActive: !c.isActive });

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <header className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Communities</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">
            Sub-groups inside your alumni network. Promote an approved member to Leader so they can manage day-to-day requests and moderation themselves.
          </p>
        </div>
        <Button onClick={() => setShowCreate(true)}>
          <Plus size={16} />Create community
        </Button>
      </header>

      {showCreate && (
        <CommunityForm onSave={(d) => createMut.mutate(d)} onCancel={() => setShowCreate(false)} saving={createMut.isPending} />
      )}

      {editing && (
        <CommunityForm
          initial={editing}
          onSave={(d) => updateMut.mutate({ id: editing.id, ...d, isActive: editing.isActive })}
          onCancel={() => setEditing(null)}
          saving={updateMut.isPending}
        />
      )}

      {viewingMembers && <MembersPanel community={viewingMembers} onClose={() => setViewingMembers(null)} />}

      <Card className="overflow-hidden">
        <div className="px-4 py-3.5 border-b border-border flex items-center justify-between">
          <b className="text-[13.5px]">All communities</b>
          <span className="text-[12.5px] text-muted-foreground">{communities.length} total</span>
        </div>
        <CardContent className="p-0">
          <Table className="min-w-[760px]">
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Members</TableHead>
                <TableHead>Pending</TableHead>
                <TableHead>Leaders</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Created</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableSkeleton rows={4} cols={7} />
              ) : communities.length === 0 ? (
                <TableRow><TableCell colSpan={7} className="text-center py-8 text-muted-foreground flex items-center justify-center gap-2"><Users size={16} /> No communities yet</TableCell></TableRow>
              ) : communities.map((c) => (
                <TableRow key={c.id}>
                  <TableCell>
                    <p className="font-medium">{c.name}</p>
                    {c.description && <p className="text-[12px] text-muted-foreground line-clamp-1">{c.description}</p>}
                  </TableCell>
                  <TableCell>{c.approvedCount}</TableCell>
                  <TableCell>{c.pendingCount > 0 ? <Badge variant="warning">{c.pendingCount}</Badge> : 0}</TableCell>
                  <TableCell>{c.leaderCount}</TableCell>
                  <TableCell><Badge variant={c.isActive ? "success" : "neutral"}>{c.isActive ? "Active" : "Inactive"}</Badge></TableCell>
                  <TableCell>{formatDate(c.createdAt)}</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2 justify-end">
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
    </div>
  );
}
