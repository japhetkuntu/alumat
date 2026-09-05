"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { CheckCircle, XCircle, UserCheck, Lock, Eye, Linkedin, MessageCircle, Phone } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { SearchModal } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { Avatar, AvatarFallback, AvatarImage } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle, DialogDescription } from "@alumni/ui";
import { getInitials, formatDate } from "@alumni/ui";
import { getMentorProfiles, approveMentor, rejectMentor, getMentorshipRequests } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import type { MentorProfile, MentorProfileStatus, MentorshipRequest, MentorshipStatus } from "@/types";

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
  const [viewMentor, setViewMentor] = useState<MentorProfile | null>(null);
  const [viewRequest, setViewRequest] = useState<MentorshipRequest | null>(null);
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
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <header className="flex flex-col sm:flex-row sm:items-end justify-between gap-4">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Mentorship</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">Review mentor capacity and help pairing requests move forward.</p>
          {isAdmin && !isSuperAdmin && (
            <p className="text-xs text-muted-foreground mt-1">You are scoped to your graduation year group; Super Admins can manage all year groups.</p>
          )}
        </div>
        <div className="flex gap-2">
          <Button variant={view === "mentors" ? "default" : "outline"} onClick={() => setView("mentors")}>Mentor applications</Button>
          <Button variant={view === "requests" ? "default" : "outline"} onClick={() => setView("requests")}>Pairing requests</Button>
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
                  className={`px-4 py-1.5 text-[11px] font-black uppercase tracking-widest transition-all ${
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
            (mentorSearch || mentorStatusFilter) ? (
              <EmptyState icon={<UserCheck size={40} />} title="No mentor profiles found" description="No mentors match your current search or filter." className="py-8" />
            ) : (
              <EmptyState icon={<UserCheck size={40} />} title="No mentor profiles yet" description="Alumni who apply to become mentors will appear here." className="py-8" />
            )
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
                          <Button size="sm" variant="outline" className="h-8 w-8 p-0 shrink-0" title="View details" onClick={() => setViewMentor(m)}>
                            <Eye size={13} />
                          </Button>
                          <Button size="sm" className="flex-1 h-8 text-[12px] font-bold" disabled={approveMut.isPending} onClick={() => setApproveTarget(m.id)}>
                            <CheckCircle size={13} />Approve
                          </Button>
                          <Button size="sm" variant="outline" className="flex-1 h-8 text-[12px] font-bold text-destructive border-destructive hover:bg-destructive/10" disabled={rejectMut.isPending} onClick={() => setRejectTarget(m.id)}>
                            <XCircle size={13} />Decline
                          </Button>
                        </div>
                      )}

                      {m.status !== "Pending" && (
                        <div className="pt-2 border-t border-border/40">
                          <Button size="sm" variant="outline" className="h-8 text-[12px] font-bold" onClick={() => setViewMentor(m)}>
                            <Eye size={13} />View details
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
                <div className="min-w-0">
                  <p className="font-semibold">
                    {r.menteeName ?? "Unknown"} &rarr; {r.mentorProfileName ?? "Mentor Profile"}
                  </p>
                  <p className="text-xs text-muted-foreground">{r.area} · Submitted {formatDate(r.createdAt)}</p>
                  {r.message && <p className="text-xs text-muted-foreground mt-1 italic line-clamp-2">&quot;{r.message}&quot;</p>}
                </div>
                <div className="flex flex-col items-end gap-2 shrink-0">
                  <Badge variant={requestStatusVariant[r.status]}>{r.status}</Badge>
                  <Button size="sm" variant="outline" className="h-7 text-[11px] font-bold" onClick={() => setViewRequest(r)}>
                    <Eye size={12} />View details
                  </Button>
                </div>
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

      {/* Mentor Profile Detail Dialog */}
      <Dialog open={!!viewMentor} onOpenChange={(open) => !open && setViewMentor(null)}>
        <DialogContent size="lg">
          {viewMentor && (
            <>
              <DialogHeader>
                <div className="flex items-start gap-4">
                  <UserAvatar src={viewMentor.memberProfilePictureUrl} name={viewMentor.memberName ?? "Unknown"} size="lg" />
                  <div className="min-w-0 pt-1">
                    <DialogTitle>{viewMentor.memberName ?? "Unknown Member"}</DialogTitle>
                    <DialogDescription>{viewMentor.area}</DialogDescription>
                    <div className="mt-1.5">
                      <Badge variant={profileStatusVariant[viewMentor.status]} className="text-[9px] font-black uppercase tracking-widest">
                        {viewMentor.status}
                      </Badge>
                    </div>
                  </div>
                </div>
              </DialogHeader>

              <div className="space-y-4 mt-4">
                {viewMentor.bio && (
                  <div>
                    <p className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Bio</p>
                    <p className="text-[13px] leading-relaxed">{viewMentor.bio}</p>
                  </div>
                )}

                {viewMentor.yearGroups && viewMentor.yearGroups.length > 0 && (
                  <div>
                    <p className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground mb-1.5">Year groups</p>
                    <div className="flex flex-wrap gap-1.5">
                      {viewMentor.yearGroups.map((yg) => (
                        <Badge key={yg} variant="secondary" className="text-[11px]">{yg}</Badge>
                      ))}
                    </div>
                  </div>
                )}

                <div>
                  <p className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground mb-1.5">Mentee capacity</p>
                  <div className="flex items-center gap-1.5 text-[13px]">
                    <UserCheck size={13} className="text-primary" />
                    <span className="font-bold text-primary">{viewMentor.currentMenteeCount}</span>
                    <span className="text-muted-foreground">/ {viewMentor.maxMentees} mentees</span>
                    <div className="flex-1 h-1.5 bg-muted rounded-full overflow-hidden ml-1">
                      <div className="h-full bg-primary rounded-full" style={{ width: `${viewMentor.maxMentees > 0 ? (viewMentor.currentMenteeCount / viewMentor.maxMentees) * 100 : 0}%` }} />
                    </div>
                  </div>
                </div>

                {(viewMentor.contactLinkedInUrl || viewMentor.contactWhatsAppNumber || viewMentor.contactPhoneNumber) && (
                  <div>
                    <p className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground mb-1.5">Contact</p>
                    <div className="flex flex-wrap gap-2">
                      {viewMentor.contactLinkedInUrl && (
                        <a href={viewMentor.contactLinkedInUrl} target="_blank" rel="noopener noreferrer"
                          className="flex items-center gap-1.5 text-[12.5px] font-semibold px-3 py-1.5 border border-primary text-primary">
                          <Linkedin size={13} /> LinkedIn
                        </a>
                      )}
                      {viewMentor.contactWhatsAppNumber && (
                        <a href={`https://wa.me/${viewMentor.contactWhatsAppNumber.replace(/[^\d+]/g, "").replace(/^\+/, "")}`} target="_blank" rel="noopener noreferrer"
                          className="flex items-center gap-1.5 text-[12.5px] font-semibold px-3 py-1.5 border border-primary text-primary">
                          <MessageCircle size={13} /> WhatsApp
                        </a>
                      )}
                      {viewMentor.contactPhoneNumber && (
                        <a href={`tel:${viewMentor.contactPhoneNumber}`}
                          className="flex items-center gap-1.5 text-[12.5px] font-semibold px-3 py-1.5 border border-primary text-primary">
                          <Phone size={13} /> {viewMentor.contactPhoneNumber}
                        </a>
                      )}
                    </div>
                  </div>
                )}

                <p className="text-[11px] text-muted-foreground">Applied {formatDate(viewMentor.createdAt)}</p>
              </div>

              {viewMentor.status === "Pending" && (
                <DialogFooter>
                  <Button
                    variant="outline"
                    className="text-destructive border-destructive hover:bg-destructive/10"
                    disabled={rejectMut.isPending}
                    onClick={() => { setRejectTarget(viewMentor.id); setViewMentor(null); }}
                  >
                    <XCircle size={14} />Decline
                  </Button>
                  <Button
                    disabled={approveMut.isPending}
                    onClick={() => { setApproveTarget(viewMentor.id); setViewMentor(null); }}
                  >
                    <CheckCircle size={14} />Approve
                  </Button>
                </DialogFooter>
              )}
            </>
          )}
        </DialogContent>
      </Dialog>

      {/* Pairing Request Detail Dialog */}
      <Dialog open={!!viewRequest} onOpenChange={(open) => !open && setViewRequest(null)}>
        <DialogContent size="lg">
          {viewRequest && (
            <>
              <DialogHeader>
                <div className="flex items-start gap-4">
                  <UserAvatar src={viewRequest.menteeProfilePictureUrl} name={viewRequest.menteeName ?? "Unknown"} size="lg" />
                  <div className="min-w-0 pt-1">
                    <DialogTitle>{viewRequest.menteeName ?? "Unknown"}</DialogTitle>
                    <DialogDescription>
                      Requesting {viewRequest.mentorProfileName ?? "a mentor"} &middot; {viewRequest.area}
                    </DialogDescription>
                    <div className="mt-1.5">
                      <Badge variant={requestStatusVariant[viewRequest.status]}>{viewRequest.status}</Badge>
                    </div>
                  </div>
                </div>
              </DialogHeader>

              <div className="space-y-4 mt-4">
                {viewRequest.message && (
                  <div>
                    <p className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Message</p>
                    <p className="text-[13px] leading-relaxed italic">&quot;{viewRequest.message}&quot;</p>
                  </div>
                )}

                {viewRequest.status === "Accepted" && (viewRequest.contactLinkedInUrl || viewRequest.contactWhatsAppNumber || viewRequest.contactPhoneNumber) && (
                  <div>
                    <p className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground mb-1.5">Mentor contact</p>
                    <div className="flex flex-wrap gap-2">
                      {viewRequest.contactLinkedInUrl && (
                        <a href={viewRequest.contactLinkedInUrl} target="_blank" rel="noopener noreferrer"
                          className="flex items-center gap-1.5 text-[12.5px] font-semibold px-3 py-1.5 border border-primary text-primary">
                          <Linkedin size={13} /> LinkedIn
                        </a>
                      )}
                      {viewRequest.contactWhatsAppNumber && (
                        <a href={`https://wa.me/${viewRequest.contactWhatsAppNumber.replace(/[^\d+]/g, "").replace(/^\+/, "")}`} target="_blank" rel="noopener noreferrer"
                          className="flex items-center gap-1.5 text-[12.5px] font-semibold px-3 py-1.5 border border-primary text-primary">
                          <MessageCircle size={13} /> WhatsApp
                        </a>
                      )}
                      {viewRequest.contactPhoneNumber && (
                        <a href={`tel:${viewRequest.contactPhoneNumber}`}
                          className="flex items-center gap-1.5 text-[12.5px] font-semibold px-3 py-1.5 border border-primary text-primary">
                          <Phone size={13} /> {viewRequest.contactPhoneNumber}
                        </a>
                      )}
                    </div>
                  </div>
                )}

                <p className="text-[11px] text-muted-foreground">Submitted {formatDate(viewRequest.createdAt)}</p>
              </div>
            </>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
