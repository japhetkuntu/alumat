"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Star, CheckCircle, XCircle, Plus, Search, Loader2 } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Avatar, AvatarImage, AvatarFallback } from "@/components/ui/avatar";
import { formatDate, getInitials } from "@/lib/utils";
import { getSpotlights, approveSpotlight, rejectSpotlight, createSpotlight, getMembers } from "@/lib/admin-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import type { Spotlight, Member } from "@/types";

const statusColors: Record<string, string> = {
  Pending: "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400",
  Approved: "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400",
  Rejected: "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400",
};

export default function AdminSpotlightsPage() {
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [approveTarget, setApproveTarget] = useState<Spotlight | null>(null);
  const [rejectTarget, setRejectTarget] = useState<Spotlight | null>(null);
  const [rejectReason, setRejectReason] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [memberSearch, setMemberSearch] = useState("");
  const [selectedMember, setSelectedMember] = useState<Member | null>(null);
  const [createForm, setCreateForm] = useState({ title: "", story: "", imageUrl: "" });
  const pageSize = 10;
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["admin-spotlights", statusFilter, page],
    queryFn: () => getSpotlights(page, pageSize, statusFilter || undefined),
    placeholderData: (prev) => prev,
  });

  const approveMut = useMutation({
    mutationFn: (id: string) => approveSpotlight(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-spotlights"] });
      setApproveTarget(null);
      toast.success("Spotlight approved and featured");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const rejectMut = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) => rejectSpotlight(id, reason),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-spotlights"] });
      setRejectTarget(null);
      setRejectReason("");
      toast.success("Spotlight rejected");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const memberSearchQuery = useQuery({
    queryKey: ["admin-member-search", memberSearch],
    queryFn: () => getMembers({ search: memberSearch, pageSize: 8, status: "Active" }),
    enabled: showCreate && memberSearch.length >= 2,
  });

  const createMut = useMutation({
    mutationFn: createSpotlight,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-spotlights"] });
      setShowCreate(false);
      setSelectedMember(null);
      setCreateForm({ title: "", story: "", imageUrl: "" });
      setMemberSearch("");
      toast.success("Spotlight created and featured");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const spotlights = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-8 lg:p-12 space-y-10 max-w-7xl mx-auto">
      <header className="flex items-end justify-between gap-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div className="space-y-1">
          <p className="text-[11px] font-black uppercase tracking-[0.2em] text-primary">Community</p>
          <h1 className="text-4xl font-black tracking-tight text-foreground">Alumni Spotlights</h1>
          <p className="text-muted-foreground font-medium">Review and manage spotlight submissions from members</p>
        </div>
        <Button onClick={() => setShowCreate(true)} className="shadow-lg shadow-primary/20 font-bold h-11 px-5">
          <Plus size={16} />Feature Member
        </Button>
      </header>

      {/* Status filter tabs */}
      <div className="flex items-center gap-2 flex-wrap">
        {["", "Pending", "Approved", "Rejected"].map((s) => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setPage(1); }}
            className={`px-4 py-1.5 rounded-full text-[11px] font-black uppercase tracking-widest transition-all ${
              statusFilter === s
                ? "bg-primary text-primary-foreground shadow-md shadow-primary/20"
                : "bg-muted/50 text-muted-foreground hover:bg-muted"
            }`}
          >
            {s === "" ? "All" : s}
          </button>
        ))}
      </div>

      {/* Spotlights list */}
      {isLoading ? (
        <div className="grid gap-4">
          {Array.from({ length: 3 }).map((_, i) => (
            <CardSkeleton key={i} />
          ))}
        </div>
      ) : spotlights.length === 0 ? (
        <EmptyState
          icon={<Star size={48} />}
          title="No spotlights found"
          description={statusFilter ? `No ${statusFilter.toLowerCase()} spotlights yet.` : "No spotlight submissions yet."}
        />
      ) : (
        <div className="grid gap-4">
          {spotlights.map((s) => (
            <Card key={s.id} className="overflow-hidden">
              <CardContent className="p-5">
                <div className="flex items-start gap-4">
                  <Avatar className="h-11 w-11 shrink-0">
                    {s.memberProfilePictureUrl && <AvatarImage src={s.memberProfilePictureUrl} />}
                    <AvatarFallback className="text-xs font-bold">
                      {s.memberName ? getInitials(s.memberName) : "?"}
                    </AvatarFallback>
                  </Avatar>

                  <div className="flex-1 min-w-0 space-y-2">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <h3 className="font-bold text-sm leading-tight">{s.title}</h3>
                        <p className="text-xs text-muted-foreground mt-0.5">
                          {s.memberName ?? "Unknown"} · Class of {s.memberGraduationYear ?? "N/A"} · {formatDate(s.createdAt)}
                        </p>
                      </div>
                      <Badge className={`shrink-0 text-[10px] uppercase font-bold ${statusColors[s.status] ?? ""}`}>
                        {s.status}
                      </Badge>
                    </div>

                    <p className="text-sm text-muted-foreground leading-relaxed line-clamp-3">{s.story}</p>

                    {s.imageUrl && (
                      <img
                        src={s.imageUrl}
                        alt={s.title}
                        className="mt-2 rounded-lg max-h-48 object-cover"
                      />
                    )}

                    {s.featuredMonth && (
                      <p className="text-xs text-muted-foreground">
                        Featured: {new Date(s.featuredMonth).toLocaleDateString("en-US", { month: "long", year: "numeric" })}
                      </p>
                    )}

                    {s.status === "Pending" && (
                      <div className="flex gap-2 pt-1">
                        <Button
                          size="sm"
                          className="h-8 text-xs font-bold"
                          onClick={() => setApproveTarget(s)}
                        >
                          <CheckCircle size={14} className="mr-1" />Approve
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-8 text-xs font-bold text-destructive hover:text-destructive"
                          onClick={() => setRejectTarget(s)}
                        >
                          <XCircle size={14} className="mr-1" />Reject
                        </Button>
                      </div>
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
      )}

      {/* Approve modal */}
      <ConfirmModal
        open={!!approveTarget}
        title="Approve Spotlight"
        message={`Approve "${approveTarget?.title}" by ${approveTarget?.memberName ?? "this member"}? It will be featured on the spotlights page.`}
        confirmLabel="Approve"
        variant="default"
        isLoading={approveMut.isPending}
        onConfirm={() => approveTarget && approveMut.mutate(approveTarget.id)}
        onCancel={() => setApproveTarget(null)}
      />

      {/* Reject modal */}
      <ConfirmModal
        open={!!rejectTarget}
        title="Reject Spotlight"
        message={`Reject "${rejectTarget?.title}" by ${rejectTarget?.memberName ?? "this member"}?`}
        confirmLabel="Reject"
        variant="destructive"
        isLoading={rejectMut.isPending}
        onConfirm={() => rejectTarget && rejectMut.mutate({ id: rejectTarget.id, reason: rejectReason || undefined })}
        onCancel={() => { setRejectTarget(null); setRejectReason(""); }}
      >
        <div className="space-y-2">
          <Label>Reason (optional)</Label>
          <Input
            placeholder="Why is this being rejected?"
            value={rejectReason}
            onChange={(e) => setRejectReason(e.target.value)}
          />
        </div>
      </ConfirmModal>

      {/* Create spotlight dialog */}
      <Dialog open={showCreate} onOpenChange={(v) => { if (!v) { setShowCreate(false); setSelectedMember(null); setCreateForm({ title: "", story: "", imageUrl: "" }); setMemberSearch(""); } }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Feature a Member</DialogTitle>
          </DialogHeader>

          {/* Member search */}
          {!selectedMember ? (
            <div className="space-y-3">
              <Label>Search for a member</Label>
              <div className="relative">
                <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
                <Input
                  placeholder="Search by name or email..."
                  value={memberSearch}
                  onChange={(e) => setMemberSearch(e.target.value)}
                  className="pl-9"
                />
              </div>
              {memberSearchQuery.isLoading && (
                <div className="flex items-center justify-center py-4"><Loader2 size={18} className="animate-spin text-muted-foreground" /></div>
              )}
              {memberSearchQuery.data && (
                <div className="space-y-1 max-h-52 overflow-y-auto">
                  {(memberSearchQuery.data.results ?? []).length === 0 ? (
                    <p className="text-sm text-muted-foreground text-center py-3">No members found</p>
                  ) : (
                    (memberSearchQuery.data.results ?? []).map((m) => (
                      <button
                        key={m.id}
                        type="button"
                        onClick={() => setSelectedMember(m)}
                        className="w-full flex items-center gap-3 rounded-lg border border-border/40 bg-muted/20 px-3 py-2.5 text-left hover:bg-muted/50 transition-colors"
                      >
                        <Avatar className="h-8 w-8 shrink-0">
                          {m.profilePictureUrl && <AvatarImage src={m.profilePictureUrl} />}
                          <AvatarFallback className="text-[10px] font-bold">{getInitials(`${m.firstName} ${m.lastName}`)}</AvatarFallback>
                        </Avatar>
                        <div className="min-w-0 flex-1">
                          <p className="text-sm font-semibold truncate">{m.firstName} {m.lastName}</p>
                          <p className="text-xs text-muted-foreground truncate">{m.email} · Class of {m.graduationYear}</p>
                        </div>
                      </button>
                    ))
                  )}
                </div>
              )}
            </div>
          ) : (
            <div className="space-y-4">
              {/* Selected member */}
              <div className="flex items-center gap-3 rounded-lg border border-border/40 bg-muted/20 px-3 py-2.5">
                <Avatar className="h-9 w-9 shrink-0">
                  {selectedMember.profilePictureUrl && <AvatarImage src={selectedMember.profilePictureUrl} />}
                  <AvatarFallback className="text-xs font-bold">{getInitials(`${selectedMember.firstName} ${selectedMember.lastName}`)}</AvatarFallback>
                </Avatar>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-semibold">{selectedMember.firstName} {selectedMember.lastName}</p>
                  <p className="text-xs text-muted-foreground">{selectedMember.email} · Class of {selectedMember.graduationYear}</p>
                </div>
                <Button variant="ghost" size="sm" className="text-xs shrink-0" onClick={() => setSelectedMember(null)}>Change</Button>
              </div>

              {/* Spotlight form */}
              <div className="space-y-2">
                <Label>Title</Label>
                <Input
                  placeholder="e.g. Outstanding Community Leader"
                  value={createForm.title}
                  onChange={(e) => setCreateForm((f) => ({ ...f, title: e.target.value }))}
                  required
                />
              </div>
              <div className="space-y-2">
                <Label>Story</Label>
                <Textarea
                  placeholder="Write about this member's achievements, contributions, or story..."
                  value={createForm.story}
                  onChange={(e) => setCreateForm((f) => ({ ...f, story: e.target.value }))}
                  rows={4}
                  required
                />
              </div>
              <div className="space-y-2">
                <Label>Image URL (optional)</Label>
                <Input
                  placeholder="https://..."
                  value={createForm.imageUrl}
                  onChange={(e) => setCreateForm((f) => ({ ...f, imageUrl: e.target.value }))}
                />
              </div>

              <DialogFooter>
                <Button variant="outline" onClick={() => { setShowCreate(false); setSelectedMember(null); setCreateForm({ title: "", story: "", imageUrl: "" }); setMemberSearch(""); }}>Cancel</Button>
                <Button
                  disabled={!createForm.title.trim() || !createForm.story.trim() || createMut.isPending}
                  onClick={() => createMut.mutate({ memberId: selectedMember.id, title: createForm.title, story: createForm.story, imageUrl: createForm.imageUrl || undefined })}
                >
                  {createMut.isPending ? <Loader2 size={14} className="animate-spin mr-2" /> : null}
                  Feature Member
                </Button>
              </DialogFooter>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
