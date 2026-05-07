"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { CheckCircle, XCircle, UserCheck, Lock } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { SearchModal } from "@/components/ui/search-modal";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { getInitials, formatDate } from "@/lib/utils";
import { getMentorProfiles, approveMentor, rejectMentor, getMentorshipRequests } from "@/lib/admin-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { useAuth } from "@/hooks/use-auth";
import type { MentorProfileStatus, MentorshipStatus } from "@/types";

const profileStatusVariant: Record<MentorProfileStatus, "success" | "warning" | "destructive"> = {
  Approved: "success",
  Pending: "warning",
  Rejected: "destructive",
};

const requestStatusVariant: Record<MentorshipStatus, "success" | "warning" | "destructive" | "secondary"> = {
  Accepted: "success",
  Pending: "warning",
  Rejected: "destructive",
  Completed: "secondary",
};

export default function AdminMentorshipPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const isAdmin = user?.role === "Admin";
  const [view, setView] = useState<"mentors" | "requests">("mentors");
  const [approveTarget, setApproveTarget] = useState<string | null>(null);
  const [rejectTarget, setRejectTarget] = useState<string | null>(null);
  const [mentorPage, setMentorPage] = useState(1);
  const [requestPage, setRequestPage] = useState(1);
  const [mentorStatusFilter, setMentorStatusFilter] = useState("");
  const [mentorSearch, setMentorSearch] = useState("");
  const mentorPageSize = 20;
  const requestPageSize = 20;
  const qc = useQueryClient();

  const { data: profilesData, isLoading: profilesLoading } = useQuery({
    queryKey: ["admin-mentor-profiles", mentorPage, mentorStatusFilter, mentorSearch],
    queryFn: () => getMentorProfiles(mentorPage, mentorPageSize, mentorStatusFilter || undefined, mentorSearch || undefined),
    placeholderData: (prev) => prev,
    enabled: isSuperAdmin || isAdmin,
  });

  const { data: requestsData, isLoading: requestsLoading } = useQuery({
    queryKey: ["admin-mentorship-requests", requestPage],
    queryFn: () => getMentorshipRequests(requestPage, requestPageSize),
    placeholderData: (prev) => prev,
    enabled: isSuperAdmin || isAdmin,
  });

  const approveMut = useMutation({
    mutationFn: (id: string) => approveMentor(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-mentor-profiles"] }); setApproveTarget(null); toast.success("Mentor approved"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const rejectMut = useMutation({
    mutationFn: (id: string) => rejectMentor(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-mentor-profiles"] }); setRejectTarget(null); toast.success("Mentor declined"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const mentors = profilesData?.results ?? [];
  const mentorTotalPages = profilesData?.totalPages ?? 1;
  const requests = requestsData?.results ?? [];
  const requestTotalPages = requestsData?.totalPages ?? 1;

  if (!isSuperAdmin && !isAdmin) {
    return (
      <div className="p-8 lg:p-12 space-y-6 max-w-7xl mx-auto">
        <EmptyState
          icon={<Lock size={40} />}
          title="Access denied"
          description="Only Admins and Super Admins can access mentorship management."
        />
      </div>
    );
  }

  return (
    <div className="p-8 lg:p-12 space-y-10 max-w-7xl mx-auto">
      <header className="flex flex-col sm:flex-row sm:items-end justify-between gap-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div className="space-y-1">
          <p className="text-[11px] font-black uppercase tracking-[0.2em] text-primary">Program</p>
          <h1 className="text-4xl font-black tracking-tight text-foreground">Mentorship</h1>
          <p className="text-muted-foreground font-medium">Approve mentors and manage alumni mentorship pairings</p>
          {isAdmin && !isSuperAdmin && (
            <p className="text-xs text-muted-foreground">You are scoped to your graduation year group; Super Admins can manage all year groups.</p>
          )}
        </div>
        <div className="flex gap-2">
          <Button variant={view === "mentors" ? "default" : "outline"} className="font-bold" onClick={() => setView("mentors")}>Mentors</Button>
          <Button variant={view === "requests" ? "default" : "outline"} className="font-bold" onClick={() => setView("requests")}>Requests</Button>
        </div>
      </header>

      {view === "mentors" && (
        <div className="space-y-6">
          {/* Search + Filter */}
          <div className="flex flex-col sm:flex-row gap-3 items-start">
            <div className="flex-1 min-w-0 max-w-sm">
            <SearchModal
              title="Search mentors"
              value={mentorSearch}
              onChange={(value) => { setMentorSearch(value); setMentorPage(1); }}
              placeholder="Search mentors..."
            >
              {profilesLoading ? (
                <p className="text-sm text-muted-foreground">Searching...</p>
              ) : mentors.length === 0 ? (
                <p className="text-sm text-muted-foreground">No mentors match your search.</p>
              ) : (
                <div className="space-y-2 max-h-72 overflow-y-auto">
                  {mentors.slice(0, 5).map((m) => (
                    <div key={m.id} className="flex items-center justify-between gap-3 rounded-lg border border-border/40 bg-muted/30 px-3 py-2">
                      <div className="min-w-0">
                        <p className="text-sm font-semibold truncate">{m.memberName ?? "Unknown"}</p>
                        <p className="text-xs text-muted-foreground truncate">{m.area}</p>
                      </div>
                      <Badge variant={profileStatusVariant[m.status]} className="text-[10px] font-bold uppercase tracking-widest">
                        {m.status}
                      </Badge>
                    </div>
                  ))}
                  {mentors.length > 5 && (
                    <p className="text-xs text-muted-foreground">Showing {Math.min(5, mentors.length)} of {mentors.length} results. Close to view the full list.</p>
                  )}
                </div>
              )}
            </SearchModal>
          </div>
            <div className="flex items-center gap-2 flex-wrap">
              {["", "Pending", "Approved", "Rejected"].map((s) => (
                <button
                  key={s}
                  onClick={() => { setMentorStatusFilter(s); setMentorPage(1); }}
                  className={`px-4 py-1.5 rounded-full text-[11px] font-black uppercase tracking-widest transition-all ${
                    mentorStatusFilter === s ? "bg-primary text-primary-foreground shadow-md shadow-primary/20" : "bg-muted/50 text-muted-foreground hover:bg-muted"
                  }`}
                >
                  {s === "" ? "All" : s}
                </button>
              ))}
            </div>
          </div>

          {profilesLoading ? (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5">
              {Array.from({ length: 6 }).map((_, i) => <div key={i} className="h-44 rounded-xl bg-muted animate-pulse" />)}
            </div>
          ) : mentors.length === 0 ? (
            <EmptyState icon={<UserCheck size={40} />} title="No mentor profiles found" description="Adjust your filters or check back later." className="py-8" />
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5">
              {mentors.map((m, i) => {
                const name = m.memberName ?? "Unknown Member";
                return (
                  <Card
                    key={m.id}
                    className="group overflow-hidden border-border/40 hover:border-primary/30 hover:shadow-xl hover:shadow-primary/5 transition-all duration-300 animate-in fade-in slide-in-from-bottom-6 duration-700"
                    style={{ animationDelay: `${i * 60}ms` }}
                  >
                    <CardContent className="p-5">
                      <div className="flex items-start gap-4 mb-4">
                        <Avatar className="h-14 w-14 ring-2 ring-border/40 group-hover:ring-primary/30 transition-all">
                          <AvatarImage src={m.memberProfilePictureUrl ?? undefined} alt={name} />
                          <AvatarFallback className="text-base font-bold bg-primary/10 text-primary">
                            {getInitials(name)}
                          </AvatarFallback>
                        </Avatar>
                        <div className="flex-1 min-w-0">
                          <p className="font-bold text-[14px] leading-tight group-hover:text-primary transition-colors">{name}</p>
                          <p className="text-[12px] text-muted-foreground font-medium mt-0.5">{m.area}</p>
                          <div className="mt-1.5">
                            <Badge variant={profileStatusVariant[m.status]} className="text-[9px] font-black uppercase tracking-widest">{m.status}</Badge>
                          </div>
                        </div>
                      </div>

                      {m.bio && (
                        <p className="text-[12px] text-muted-foreground leading-relaxed line-clamp-2 mb-3">{m.bio}</p>
                      )}

                      {m.status === "Approved" && (
                        <div className="flex items-center gap-1.5 mb-3 text-[11px]">
                          <UserCheck size={11} className="text-primary" />
                          <span className="font-bold text-primary">{m.currentMenteeCount}</span>
                          <span className="text-muted-foreground">/ {m.maxMentees} mentees</span>
                          <div className="flex-1 h-1.5 bg-muted rounded-full overflow-hidden ml-1">
                            <div className="h-full bg-primary rounded-full" style={{ width: `${m.maxMentees > 0 ? (m.currentMenteeCount / m.maxMentees) * 100 : 0}%` }} />
                          </div>
                        </div>
                      )}

                      {m.status === "Pending" && (
                        <div className="flex gap-2 pt-2 border-t border-border/40">
                          <Button size="sm" className="flex-1 h-8 text-[12px] font-bold" disabled={approveMut.isPending} onClick={() => setApproveTarget(m.id)}>
                            <CheckCircle size={13} />Approve
                          </Button>
                          <Button size="sm" variant="outline" className="flex-1 h-8 text-[12px] font-bold text-destructive border-destructive hover:bg-destructive/10" disabled={rejectMut.isPending} onClick={() => setRejectTarget(m.id)}>
                            <XCircle size={13} />Decline
                          </Button>
                        </div>
                      )}
                    </CardContent>
                  </Card>
                );
              })}
            </div>
          )}
          <Pagination page={mentorPage} totalPages={mentorTotalPages} onPageChange={setMentorPage} />
        </div>
      )}

      {view === "requests" && (
        <div className="space-y-3">
          {requestsLoading ? (
            <div className="space-y-3">{Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}</div>
          ) : requests.length === 0 ? (
            <EmptyState icon={<UserCheck size={40} />} title="No mentorship requests yet" description="Member mentorship requests will appear here." className="py-8" />
          ) : requests.map((r) => (
            <Card key={r.id} className="stagger-item hover:shadow-md transition-shadow">
              <CardContent className="p-4 flex items-center justify-between gap-4">
                <div>
                  <p className="font-semibold">
                    {r.menteeName ?? "Unknown"} &rarr; Mentor Profile
                  </p>
                  <p className="text-xs text-muted-foreground">{r.area} · Submitted {formatDate(r.createdAt)}</p>
                  {r.message && <p className="text-xs text-muted-foreground mt-1 italic">&quot;{r.message}&quot;</p>}
                </div>
                <Badge variant={requestStatusVariant[r.status]}>{r.status}</Badge>
              </CardContent>
            </Card>
          ))}

          <Pagination page={requestPage} totalPages={requestTotalPages} onPageChange={setRequestPage} />
        </div>
      )}

      <ConfirmModal
        open={!!approveTarget}
        title="Approve Mentor"
        message="Approve this mentor profile? They will be able to accept mentees."
        confirmLabel="Approve"
        variant="default"
        isLoading={approveMut.isPending}
        onConfirm={() => approveTarget && approveMut.mutate(approveTarget)}
        onCancel={() => setApproveTarget(null)}
      />
      <ConfirmModal
        open={!!rejectTarget}
        title="Decline Mentor"
        message="Decline this mentor application?"
        confirmLabel="Decline"
        variant="destructive"
        isLoading={rejectMut.isPending}
        onConfirm={() => rejectTarget && rejectMut.mutate(rejectTarget)}
        onCancel={() => setRejectTarget(null)}
      />
    </div>
  );
}
