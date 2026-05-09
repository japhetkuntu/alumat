"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { UserCheck, Plus, CheckCircle, XCircle, Clock, Inbox } from "lucide-react";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { getInitials, formatDate } from "@/lib/utils";
import { getMentors, requestMentorship, registerAsMentor, getMyMentorshipRequests, getMyMentorProfile, getIncomingMentorshipRequests, acceptMentorshipRequest, rejectMentorshipRequest } from "@/lib/member-api";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import type { MentorProfileStatus, MentorshipStatus } from "@/types";

const reqStatusVariant: Record<MentorshipStatus, "success" | "warning" | "destructive" | "secondary"> = {
  Accepted: "success", Pending: "warning", Rejected: "destructive", Completed: "secondary",
};

const profileStatusVariant: Record<MentorProfileStatus, "success" | "warning" | "destructive"> = {
  Approved: "success", Pending: "warning", Rejected: "destructive",
};

export default function MemberMentorshipPage() {
  const [view, setView] = useState<"find" | "requests" | "incoming" | "become">("find");
  const [requestForm, setRequestForm] = useState({ mentorProfileId: "", area: "", message: "" });
  const [mentorForm, setMentorForm] = useState({ area: "", bio: "", maxMentees: "3" });
  const [confirmAction, setConfirmAction] = useState<{ type: "accept" | "reject"; id: string } | null>(null);
  const qc = useQueryClient();

  const { data: mentorsData, isLoading } = useQuery({
    queryKey: ["m-mentors"],
    queryFn: () => getMentors(1, 30),
  });

  const { data: requestsData } = useQuery({
    queryKey: ["m-my-requests"],
    queryFn: () => getMyMentorshipRequests(),
  });

  const { data: myProfile } = useQuery({
    queryKey: ["m-my-mentor-profile"],
    queryFn: () => getMyMentorProfile(),
    retry: false,
  });

  const { data: incomingData } = useQuery({
    queryKey: ["m-incoming-requests"],
    queryFn: () => getIncomingMentorshipRequests(),
    enabled: !!myProfile,
  });

  const requestMut = useMutation({
    mutationFn: () => requestMentorship({ mentorProfileId: requestForm.mentorProfileId, area: requestForm.area, message: requestForm.message || undefined }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["m-my-requests"] }); setRequestForm({ mentorProfileId: "", area: "", message: "" }); toast.success("Mentorship request sent!"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const becomeMut = useMutation({
    mutationFn: () => registerAsMentor({ area: mentorForm.area, bio: mentorForm.bio || undefined, maxMentees: Number(mentorForm.maxMentees) }),
    onSuccess: () => {
      toast.success("Application submitted! You will be notified once approved.");
      setMentorForm({ area: "", bio: "", maxMentees: "3" });
      qc.invalidateQueries({ queryKey: ["m-my-mentor-profile"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const acceptMut = useMutation({
    mutationFn: (id: string) => acceptMentorshipRequest(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-incoming-requests"] });
      qc.invalidateQueries({ queryKey: ["m-my-mentor-profile"] });
      setConfirmAction(null);
      toast.success("Request accepted! You are now mentoring this member.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const rejectMut = useMutation({
    mutationFn: (id: string) => rejectMentorshipRequest(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["m-incoming-requests"] }); setConfirmAction(null); toast.success("Request declined"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const mentors = mentorsData?.results ?? [];
  const requests = requestsData?.results ?? [];
  const incoming = incomingData?.results ?? [];

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-8">
      <header className="flex flex-col sm:flex-row sm:items-start justify-between gap-4 flex-wrap animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div className="space-y-1">
          <h1 className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tight">Mentorship</h1>
          <p className="text-muted-foreground text-sm sm:text-base lg:text-lg font-medium max-w-xl">Find a mentor to guide your career, or give back by mentoring fellow alumni.</p>
        </div>
        <div className="flex gap-2 flex-wrap">
          <button onClick={() => setView("find")} className={`px-4 py-2 rounded-full text-[13px] font-semibold transition-all ${ view === "find" ? "bg-primary text-primary-foreground shadow-sm" : "bg-muted text-muted-foreground hover:bg-muted/80"}` }>Find Mentor</button>
          <button onClick={() => setView("requests")} className={`px-4 py-2 rounded-full text-[13px] font-semibold flex items-center gap-1.5 transition-all ${ view === "requests" ? "bg-primary text-primary-foreground shadow-sm" : "bg-muted text-muted-foreground hover:bg-muted/80"}` }>
            <Clock size={13} />My Requests
          </button>
          {myProfile && (
            <button onClick={() => setView("incoming")} className={`px-4 py-2 rounded-full text-[13px] font-semibold flex items-center gap-1.5 transition-all ${ view === "incoming" ? "bg-primary text-primary-foreground shadow-sm" : "bg-muted text-muted-foreground hover:bg-muted/80"}` }>
              <Inbox size={13} />Incoming{incoming.filter(r => r.status === "Pending").length > 0 && ` (${incoming.filter(r => r.status === "Pending").length})`}
            </button>
          )}
          <button onClick={() => setView("become")} className={`px-4 py-2 rounded-full text-[13px] font-semibold flex items-center gap-1.5 transition-all ${ view === "become" ? "bg-primary text-primary-foreground shadow-sm" : "bg-muted text-muted-foreground hover:bg-muted/80"}` }>
            <Plus size={13} />Become Mentor
          </button>
        </div>
      </header>

      {/* Mentor profile status banner */}
      {myProfile && (
        <Card>
          <CardContent className="p-4 flex items-center justify-between gap-4">
            <div>
              <p className="text-sm font-medium">Your Mentor Profile</p>
              <p className="text-xs text-muted-foreground">{myProfile.area} · {myProfile.currentMenteeCount}/{myProfile.maxMentees} mentees</p>
            </div>
            <Badge variant={profileStatusVariant[myProfile.status]}>{myProfile.status}</Badge>
          </CardContent>
        </Card>
      )}

      {view === "find" && (
        <div className="space-y-6">
          {isLoading ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">{Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}</div>
          ) : mentors.length === 0 ? (
            <EmptyState icon={<UserCheck size={48} />} title="No mentors available right now" description="Check back later as more alumni register as mentors." className="py-12" />
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
              {mentors.map((m, i) => {
                const mentorName = m.memberName ?? "Alumni Mentor";
                const isFull = m.currentMenteeCount >= m.maxMentees;
                return (
                  <Card key={m.id} className="group border-border/40 hover:border-primary/30 hover:shadow-xl hover:shadow-primary/5 transition-all duration-500 animate-in fade-in slide-in-from-bottom-4 duration-700" style={{ animationDelay: `${i * 50}ms` }}>
                    <CardContent className="p-6 space-y-4">
                      <div className="flex items-start gap-4">
                        <Avatar size="lg" className="shrink-0 ring-2 ring-background shadow-md group-hover:ring-primary/20 transition-all">
                          {m.memberProfilePictureUrl && <AvatarImage src={m.memberProfilePictureUrl} alt={mentorName} />}
                          <AvatarFallback name={mentorName} className="text-[15px]">{getInitials(mentorName)}</AvatarFallback>
                        </Avatar>
                        <div className="flex-1 min-w-0 space-y-1">
                          <p className="font-bold text-[15px] truncate group-hover:text-primary transition-colors">{mentorName}</p>
                          <p className="text-[12px] text-muted-foreground font-medium">{m.area}</p>
                          <div className="flex items-center gap-1.5">
                            {isFull
                              ? <Badge variant="secondary" className="text-[10px] font-bold">Full</Badge>
                              : <Badge variant="success" className="text-[10px] font-bold">Available</Badge>
                            }
                            <span className="text-[10px] text-muted-foreground">{m.currentMenteeCount}/{m.maxMentees} mentees</span>
                          </div>
                        </div>
                      </div>

                      {m.bio && (
                        <p className="text-[12px] text-muted-foreground leading-relaxed line-clamp-3">{m.bio}</p>
                      )}

                      <Button
                        size="sm"
                        className="w-full"
                        disabled={isFull}
                        variant={isFull ? "outline" : "default"}
                        onClick={() => !isFull && setRequestForm({ ...requestForm, mentorProfileId: m.id, area: m.area })}
                      >
                        {isFull ? "Mentor Full" : "Request Mentorship"}
                      </Button>
                    </CardContent>
                  </Card>
                );
              })}
            </div>
          )}

          {requestForm.mentorProfileId && (
            <Card>
              <CardHeader><CardTitle className="text-base">Send Mentorship Request</CardTitle></CardHeader>
              <CardContent>
                <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); requestMut.mutate(); }}>
                  <div className="space-y-2">
                    <Label>Area of interest</Label>
                    <Input placeholder="e.g. Career guidance, Mining Engineering..." value={requestForm.area} onChange={(e) => setRequestForm({ ...requestForm, area: e.target.value })} required />
                  </div>
                  <div className="space-y-2">
                    <Label>Message (optional)</Label>
                    <Textarea placeholder="Introduce yourself and what you hope to gain..." rows={3} value={requestForm.message} onChange={(e) => setRequestForm({ ...requestForm, message: e.target.value })} />
                  </div>
                  <div className="flex gap-3">
                    <Button type="submit" size="sm" disabled={requestMut.isPending}>{requestMut.isPending ? "Sending..." : "Send Request"}</Button>
                    <Button type="button" size="sm" variant="outline" onClick={() => setRequestForm({ mentorProfileId: "", area: "", message: "" })}>Cancel</Button>
                  </div>
                </form>
              </CardContent>
            </Card>
          )}
        </div>
      )}

      {view === "requests" && (
        <div className="space-y-3">
          {requests.length === 0 ? (
            <EmptyState icon={<Clock size={40} />} title="No requests sent yet" description="Browse available mentors above and send a request." className="py-8" />
          ) : requests.map((r) => (
            <Card key={r.id}>
              <CardContent className="p-4 flex items-center justify-between">
                <div>
                  <p className="font-medium">{r.area}</p>
                  <p className="text-xs text-muted-foreground">
                    {r.mentorProfileName ? `To: ${r.mentorProfileName}` : ""}
                    {" · "}{formatDate(r.createdAt)}
                  </p>
                  {r.message && <p className="text-xs text-muted-foreground mt-1 italic">&quot;{r.message}&quot;</p>}
                </div>
                <Badge variant={reqStatusVariant[r.status]}>{r.status}</Badge>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {view === "incoming" && (
        <div className="space-y-3">
          {incoming.length === 0 ? (
            <EmptyState icon={<Inbox size={40} />} title="No incoming requests" description="Mentorship requests from other alumni will appear here." className="py-8" />
          ) : incoming.map((r) => (
            <Card key={r.id}>
              <CardContent className="p-4 flex items-center justify-between gap-4">
                <div className="flex items-center gap-3 flex-1">
                  <Avatar size="sm" className="shrink-0">
                    {r.menteeProfilePictureUrl && <AvatarImage src={r.menteeProfilePictureUrl} alt={r.menteeName ?? "M"} />}
                    <AvatarFallback className="text-xs" name={r.menteeName ?? "M"}>{getInitials(r.menteeName ?? "M")}</AvatarFallback>
                  </Avatar>
                  <div>
                    <p className="font-medium text-sm">{r.menteeName ?? "Unknown"}</p>
                    <p className="text-xs text-muted-foreground">{r.area} · {formatDate(r.createdAt)}</p>
                    {r.message && <p className="text-xs text-muted-foreground mt-1 italic">&quot;{r.message}&quot;</p>}
                  </div>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  {r.status === "Pending" ? (
                    <>
                      <Button size="sm" className="gap-1" onClick={() => setConfirmAction({ type: "accept", id: r.id })}>
                        <CheckCircle size={13} />Accept
                      </Button>
                      <Button size="sm" variant="outline" className="gap-1 text-destructive border-destructive" onClick={() => setConfirmAction({ type: "reject", id: r.id })}>
                        <XCircle size={13} />Decline
                      </Button>
                    </>
                  ) : (
                    <Badge variant={reqStatusVariant[r.status]}>{r.status}</Badge>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {view === "become" && (
        <Card>
          <CardHeader><CardTitle className="text-base">Register as a Mentor</CardTitle></CardHeader>
          <CardContent>
            {myProfile && myProfile.status !== "Rejected" ? (
              <div className="text-center py-4">
                <p className="text-sm text-muted-foreground">You already have a mentor profile.</p>
                <Badge variant={profileStatusVariant[myProfile.status]} className="mt-2">{myProfile.status}</Badge>
              </div>
            ) : (
              <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); becomeMut.mutate(); }}>
                <div className="space-y-2">
                  <Label>Area of expertise</Label>
                  <Input placeholder="e.g. Mining Engineering, Environmental Science..." value={mentorForm.area} onChange={(e) => setMentorForm({ ...mentorForm, area: e.target.value })} required />
                </div>
                <div className="space-y-2">
                  <Label>Bio / Background</Label>
                  <Textarea placeholder="Tell potential mentees about your experience..." rows={4} value={mentorForm.bio} onChange={(e) => setMentorForm({ ...mentorForm, bio: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label>Max mentees</Label>
                  <Input type="number" min={1} max={10} value={mentorForm.maxMentees} onChange={(e) => setMentorForm({ ...mentorForm, maxMentees: e.target.value })} required />
                </div>
                <Button type="submit" size="sm" disabled={becomeMut.isPending}>{becomeMut.isPending ? "Submitting..." : myProfile?.status === "Rejected" ? "Resubmit Application" : "Submit Application"}</Button>
                {myProfile?.status === "Rejected" && (
                  <p className="text-sm text-muted-foreground">Your previous request was rejected. You can update your profile and submit again.</p>
                )}
              </form>
            )}
          </CardContent>
        </Card>
      )}

      {/* Accept/Reject confirmation modals */}
      <ConfirmModal
        open={confirmAction?.type === "accept"}
        title="Accept Mentorship Request"
        message="Accept this mentorship request? The mentee will be notified."
        confirmLabel="Accept"
        variant="default"
        isLoading={acceptMut.isPending}
        onConfirm={() => confirmAction && acceptMut.mutate(confirmAction.id)}
        onCancel={() => setConfirmAction(null)}
      />
      <ConfirmModal
        open={confirmAction?.type === "reject"}
        title="Decline Mentorship Request"
        message="Decline this mentorship request? The mentee will be notified."
        confirmLabel="Decline"
        variant="destructive"
        isLoading={rejectMut.isPending}
        onConfirm={() => confirmAction && rejectMut.mutate(confirmAction.id)}
        onCancel={() => setConfirmAction(null)}
      />
    </div>
  );
}
