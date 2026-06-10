"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  UserCheck, Plus, CheckCircle, XCircle, Clock,
  Inbox, Users, ChevronDown,
} from "lucide-react";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { getInitials, formatDate, cn } from "@/lib/utils";
import {
  getMentors, requestMentorship, registerAsMentor,
  getMyMentorshipRequests, getMyMentorProfile,
  getIncomingMentorshipRequests, acceptMentorshipRequest,
  rejectMentorshipRequest,
} from "@/lib/member-api";
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

type View = "find" | "requests" | "incoming" | "become";

/* ─────────────────────────────────────────────────────────────────────────
   TAB BUTTON
   ───────────────────────────────────────────────────────────────────────── */
function TabBtn({
  active, onClick, children,
}: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={cn(
        "px-4 py-2 rounded-full text-[13px] font-semibold flex items-center gap-1.5 transition-colors border",
        active ? "text-white border-transparent" : "border-border hover:border-primary/40",
      )}
      style={active
        ? { background: "var(--primary)", color: "white" }
        : { background: "var(--background)", color: "var(--muted-foreground)" }}
    >
      {children}
    </button>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   MENTORSHIP REQUEST FORM — slide-in below selected mentor card
   ───────────────────────────────────────────────────────────────────────── */
function RequestForm({
  area,
  onAreaChange,
  message,
  onMessageChange,
  onSubmit,
  onCancel,
  isPending,
}: {
  area: string;
  onAreaChange: (v: string) => void;
  message: string;
  onMessageChange: (v: string) => void;
  onSubmit: () => void;
  onCancel: () => void;
  isPending: boolean;
}) {
  return (
    <div
      className="rounded-2xl border p-5 space-y-4"
      style={{ borderColor: "var(--color-border-info)", background: "var(--color-background-info)" }}
    >
      <div className="flex items-center gap-2 mb-1">
        <ChevronDown size={15} style={{ color: "var(--primary)" }} />
        <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
          Send a mentorship request
        </p>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="req-area" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
          Area of interest
        </Label>
        <Input
          id="req-area"
          placeholder="e.g. Career guidance, Mining Engineering…"
          value={area}
          onChange={e => onAreaChange(e.target.value)}
          className="h-11 text-[14px]"
          required
        />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="req-message" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
          Message{" "}
          <span className="font-normal" style={{ color: "var(--muted-foreground)" }}>(optional)</span>
        </Label>
        <Textarea
          id="req-message"
          placeholder="Introduce yourself and what you hope to gain…"
          rows={3}
          value={message}
          onChange={e => onMessageChange(e.target.value)}
          className="text-[14px] resize-none"
        />
      </div>
      <div className="flex gap-2">
        <Button
          type="button"
          className="font-semibold gap-1.5 text-[13.5px]"
          style={{ height: 40 }}
          disabled={isPending || !area.trim()}
          onClick={onSubmit}
        >
          {isPending ? "Sending…" : "Send request"}
        </Button>
        <Button
          type="button"
          variant="outline"
          style={{ height: 40 }}
          onClick={onCancel}
        >
          Cancel
        </Button>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PAGE
   ───────────────────────────────────────────────────────────────────────── */
export default function MemberMentorshipPage() {
  const [view,          setView]          = useState<View>("find");
  const [requestForm,   setRequestForm]   = useState({ mentorProfileId: "", area: "", message: "" });
  const [mentorForm,    setMentorForm]    = useState({ area: "", bio: "", maxMentees: "3" });
  const [confirmAction, setConfirmAction] = useState<{ type: "accept" | "reject"; id: string } | null>(null);
  const qc = useQueryClient();

  const { data: mentorsData, isLoading } = useQuery({
    queryKey: ["m-mentors"],
    queryFn:  () => getMentors(1, 30),
  });

  const { data: requestsData } = useQuery({
    queryKey: ["m-my-requests"],
    queryFn:  getMyMentorshipRequests,
  });

  const { data: myProfile } = useQuery({
    queryKey: ["m-my-mentor-profile"],
    queryFn:  getMyMentorProfile,
    retry:    false,
  });

  const { data: incomingData } = useQuery({
    queryKey: ["m-incoming-requests"],
    queryFn:  getIncomingMentorshipRequests,
    enabled:  !!myProfile,
  });

  const requestMut = useMutation({
    mutationFn: () => requestMentorship({
      mentorProfileId: requestForm.mentorProfileId,
      area:            requestForm.area,
      message:         requestForm.message || undefined,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-my-requests"] });
      setRequestForm({ mentorProfileId: "", area: "", message: "" });
      toast.success("Mentorship request sent.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const becomeMut = useMutation({
    mutationFn: () => registerAsMentor({
      area:       mentorForm.area,
      bio:        mentorForm.bio || undefined,
      maxMentees: Number(mentorForm.maxMentees),
    }),
    onSuccess: () => {
      toast.success("Application submitted. You'll be notified once approved.");
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
      toast.success("Request accepted.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const rejectMut = useMutation({
    mutationFn: (id: string) => rejectMentorshipRequest(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-incoming-requests"] });
      setConfirmAction(null);
      toast.success("Request declined.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const mentors  = mentorsData?.results ?? [];
  const requests = requestsData?.results ?? [];
  const incoming = incomingData?.results ?? [];
  const pendingIncoming = incoming.filter(r => r.status === "Pending").length;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      {/* ── Header ── */}
      <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4">
        <div>
          <h1
            className="font-[family-name:var(--font-display)] tracking-tight"
            style={{ fontSize: "clamp(1.5rem, 3vw, 2rem)", fontWeight: 700, color: "var(--foreground)" }}
          >
            Mentorship
          </h1>
          <p className="mt-1 text-[14px]" style={{ color: "var(--muted-foreground)" }}>
            Find a mentor, or give back by mentoring fellow alumni.
          </p>
        </div>

        {/* Tab nav */}
        <div className="flex flex-wrap gap-2">
          <TabBtn active={view === "find"}     onClick={() => setView("find")}>
            <UserCheck size={13} /> Find a mentor
          </TabBtn>
          <TabBtn active={view === "requests"} onClick={() => setView("requests")}>
            <Clock size={13} /> My requests
          </TabBtn>
          {myProfile && (
            <TabBtn active={view === "incoming"} onClick={() => setView("incoming")}>
              <Inbox size={13} />
              Incoming{pendingIncoming > 0 ? ` (${pendingIncoming})` : ""}
            </TabBtn>
          )}
          <TabBtn active={view === "become"} onClick={() => setView("become")}>
            <Plus size={13} /> Become a mentor
          </TabBtn>
        </div>
      </div>

      {/* ── My mentor profile banner ── */}
      {myProfile && (
        <div
          className="flex items-center justify-between gap-4 rounded-xl border px-4 py-3"
          style={{ borderColor: "var(--border)", background: "var(--secondary)" }}
        >
          <div>
            <p className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>
              Your mentor profile
            </p>
            <p className="text-[12.5px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
              {myProfile.area} · {myProfile.currentMenteeCount}/{myProfile.maxMentees} mentees
            </p>
          </div>
          <Badge variant={profileStatusVariant[myProfile.status]} className="text-[11px] font-semibold shrink-0">
            {myProfile.status}
          </Badge>
        </div>
      )}

      {/* ══════════════════════════════════════════════
          FIND A MENTOR
      ══════════════════════════════════════════════ */}
      {view === "find" && (
        <div className="space-y-5">
          {isLoading ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              {Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}
            </div>
          ) : mentors.length === 0 ? (
            <EmptyState
              icon={<UserCheck size={40} />}
              title="No mentors available right now"
              description="Check back later as more alumni register as mentors."
            />
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              {mentors.map(m => {
                const mentorName = m.memberName ?? "Alumni Mentor";
                const isFull     = m.currentMenteeCount >= m.maxMentees;
                const isSelected = requestForm.mentorProfileId === m.id;

                return (
                  <div
                    key={m.id}
                    className={cn(
                      "rounded-2xl border p-5 flex flex-col gap-4 transition-all duration-150",
                      isSelected && "ring-2 ring-primary/30",
                    )}
                    style={{
                      borderColor: isSelected ? "var(--primary)" : "var(--border)",
                      background:  "var(--background)",
                    }}
                  >
                    {/* Avatar + name */}
                    <div className="flex items-start gap-3">
                      <Avatar size="lg" className="shrink-0">
                        {m.memberProfilePictureUrl && (
                          <AvatarImage src={m.memberProfilePictureUrl} alt={mentorName} />
                        )}
                        <AvatarFallback name={mentorName}>
                          {getInitials(mentorName)}
                        </AvatarFallback>
                      </Avatar>
                      <div className="flex-1 min-w-0">
                        <p className="text-[14.5px] font-semibold truncate" style={{ color: "var(--foreground)" }}>
                          {mentorName}
                        </p>
                        <p className="text-[12.5px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                          {m.area}
                        </p>
                        <div className="flex items-center gap-1.5 mt-1.5">
                          <Badge
                            variant={isFull ? "secondary" : "success"}
                            className="text-[10.5px] font-semibold"
                          >
                            {isFull ? "Full" : "Available"}
                          </Badge>
                          <span className="text-[11px]" style={{ color: "var(--muted-foreground)" }}>
                            {m.currentMenteeCount}/{m.maxMentees} mentees
                          </span>
                        </div>
                      </div>
                    </div>

                    {m.bio && (
                      <p className="text-[13px] leading-relaxed line-clamp-3" style={{ color: "var(--muted-foreground)" }}>
                        {m.bio}
                      </p>
                    )}

                    <Button
                      size="sm"
                      variant={isFull || isSelected ? "outline" : "default"}
                      className="w-full font-semibold text-[13.5px]"
                      style={{ height: 40 }}
                      disabled={isFull}
                      onClick={() => {
                        if (isFull) return;
                        setRequestForm(isSelected
                          ? { mentorProfileId: "", area: "", message: "" }
                          : { mentorProfileId: m.id, area: m.area, message: "" });
                      }}
                    >
                      {isFull ? "Mentor full" : isSelected ? "Cancel" : "Request mentorship"}
                    </Button>
                  </div>
                );
              })}
            </div>
          )}

          {/* Request form — shown when a mentor is selected */}
          {requestForm.mentorProfileId && (
            <RequestForm
              area={requestForm.area}
              onAreaChange={v => setRequestForm(f => ({ ...f, area: v }))}
              message={requestForm.message}
              onMessageChange={v => setRequestForm(f => ({ ...f, message: v }))}
              onSubmit={() => requestMut.mutate()}
              onCancel={() => setRequestForm({ mentorProfileId: "", area: "", message: "" })}
              isPending={requestMut.isPending}
            />
          )}
        </div>
      )}

      {/* ══════════════════════════════════════════════
          MY REQUESTS
      ══════════════════════════════════════════════ */}
      {view === "requests" && (
        <div className="space-y-2">
          {requests.length === 0 ? (
            <EmptyState
              icon={<Clock size={40} />}
              title="No requests sent yet"
              description="Browse available mentors and send a request."
            />
          ) : requests.map(r => (
            <div
              key={r.id}
              className="rounded-2xl border p-4 sm:p-5 flex flex-wrap items-start justify-between gap-4"
              style={{ borderColor: "var(--border)", background: "var(--background)" }}
            >
              <div className="min-w-0 flex-1">
                <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
                  {r.area}
                </p>
                <p className="text-[12.5px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                  {r.mentorProfileName ? `To: ${r.mentorProfileName}` : ""}
                  {r.mentorProfileName && " · "}
                  {formatDate(r.createdAt)}
                </p>
                {r.message && (
                  <p className="text-[12.5px] mt-1.5 italic" style={{ color: "var(--muted-foreground)" }}>
                    "{r.message}"
                  </p>
                )}
              </div>
              <Badge variant={reqStatusVariant[r.status]} className="text-[11px] font-semibold shrink-0">
                {r.status}
              </Badge>
            </div>
          ))}
        </div>
      )}

      {/* ══════════════════════════════════════════════
          INCOMING REQUESTS
      ══════════════════════════════════════════════ */}
      {view === "incoming" && (
        <div className="space-y-2">
          {incoming.length === 0 ? (
            <EmptyState
              icon={<Inbox size={40} />}
              title="No incoming requests"
              description="Mentorship requests from other alumni will appear here."
            />
          ) : incoming.map(r => (
            <div
              key={r.id}
              className="rounded-2xl border p-4 sm:p-5 flex flex-wrap items-center justify-between gap-4"
              style={{ borderColor: "var(--border)", background: "var(--background)" }}
            >
              {/* Mentee info */}
              <div className="flex items-center gap-3 flex-1 min-w-0">
                <Avatar size="sm" className="shrink-0">
                  {r.menteeProfilePictureUrl && (
                    <AvatarImage src={r.menteeProfilePictureUrl} alt={r.menteeName ?? "M"} />
                  )}
                  <AvatarFallback name={r.menteeName ?? "M"}>
                    {getInitials(r.menteeName ?? "M")}
                  </AvatarFallback>
                </Avatar>
                <div className="min-w-0">
                  <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
                    {r.menteeName ?? "Unknown member"}
                  </p>
                  <p className="text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>
                    {r.area} · {formatDate(r.createdAt)}
                  </p>
                  {r.message && (
                    <p className="text-[12.5px] mt-1 italic" style={{ color: "var(--muted-foreground)" }}>
                      "{r.message}"
                    </p>
                  )}
                </div>
              </div>

              {/* Actions / status */}
              <div className="flex items-center gap-2 shrink-0">
                {r.status === "Pending" ? (
                  <>
                    <Button
                      size="sm"
                      className="gap-1.5 font-semibold text-[13px]"
                      style={{ height: 36 }}
                      onClick={() => setConfirmAction({ type: "accept", id: r.id })}
                    >
                      <CheckCircle size={13} /> Accept
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      className="gap-1.5 font-semibold text-[13px]"
                      style={{ height: 36, borderColor: "var(--destructive)", color: "var(--destructive)" }}
                      onClick={() => setConfirmAction({ type: "reject", id: r.id })}
                    >
                      <XCircle size={13} /> Decline
                    </Button>
                  </>
                ) : (
                  <Badge variant={reqStatusVariant[r.status]} className="text-[11px] font-semibold">
                    {r.status}
                  </Badge>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* ══════════════════════════════════════════════
          BECOME A MENTOR
      ══════════════════════════════════════════════ */}
      {view === "become" && (
        <div className="max-w-lg">
          {myProfile && myProfile.status !== "Rejected" ? (
            <div
              className="rounded-2xl border p-6 text-center space-y-3"
              style={{ borderColor: "var(--border)", background: "var(--background)" }}
            >
              <div
                className="w-12 h-12 rounded-full flex items-center justify-center mx-auto"
                style={{ background: "var(--secondary)" }}
              >
                <Users size={22} style={{ color: "var(--muted-foreground)" }} />
              </div>
              <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
                You already have a mentor profile.
              </p>
              <Badge variant={profileStatusVariant[myProfile.status]} className="text-[11px] font-semibold">
                {myProfile.status}
              </Badge>
              {myProfile.status === "Pending" && (
                <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
                  Your application is under review. You'll be notified once it's approved.
                </p>
              )}
            </div>
          ) : (
            <div
              className="rounded-2xl border p-5 sm:p-6 space-y-5"
              style={{ borderColor: "var(--border)", background: "var(--background)" }}
            >
              <div>
                <h2
                  className="font-[family-name:var(--font-display)] mb-1"
                  style={{ fontSize: "1.05rem", fontWeight: 700, color: "var(--foreground)" }}
                >
                  Register as a mentor
                </h2>
                <p className="text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
                  Share your experience with fellow graduates. Your application will be reviewed before going live.
                </p>
              </div>

              {myProfile?.status === "Rejected" && (
                <div
                  className="flex items-start gap-2.5 p-3.5 rounded-xl text-[13px]"
                  style={{ background: "rgba(239,68,68,0.07)", border: "1px solid rgba(239,68,68,0.2)", color: "var(--destructive)" }}
                >
                  Your previous application was not approved. Update your details and resubmit.
                </div>
              )}

              <div className="space-y-4">
                <div className="space-y-1.5">
                  <Label htmlFor="mentor-area" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Area of expertise
                  </Label>
                  <Input
                    id="mentor-area"
                    placeholder="e.g. Mining Engineering, Environmental Science…"
                    value={mentorForm.area}
                    onChange={e => setMentorForm(f => ({ ...f, area: e.target.value }))}
                    className="h-11 text-[14px]"
                    required
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="mentor-bio" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Background{" "}
                    <span className="font-normal" style={{ color: "var(--muted-foreground)" }}>(optional)</span>
                  </Label>
                  <Textarea
                    id="mentor-bio"
                    placeholder="Tell potential mentees about your experience and what you can help with…"
                    rows={4}
                    value={mentorForm.bio}
                    onChange={e => setMentorForm(f => ({ ...f, bio: e.target.value }))}
                    className="text-[14px] resize-none"
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="mentor-max" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Maximum mentees at one time
                  </Label>
                  <Input
                    id="mentor-max"
                    type="number"
                    min={1}
                    max={10}
                    value={mentorForm.maxMentees}
                    onChange={e => setMentorForm(f => ({ ...f, maxMentees: e.target.value }))}
                    className="h-11 text-[14px] w-28"
                    required
                  />
                </div>
                <Button
                  className="font-semibold text-[14px] gap-2"
                  style={{ height: 44 }}
                  disabled={becomeMut.isPending || !mentorForm.area.trim()}
                  onClick={() => becomeMut.mutate()}
                >
                  {becomeMut.isPending
                    ? "Submitting…"
                    : myProfile?.status === "Rejected"
                    ? "Resubmit application"
                    : "Submit application"}
                </Button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* ── Confirm modals ── */}
      <ConfirmModal
        open={confirmAction?.type === "accept"}
        title="Accept this request?"
        message="The mentee will be notified and you'll be connected."
        confirmLabel="Accept"
        variant="default"
        isLoading={acceptMut.isPending}
        onConfirm={() => confirmAction && acceptMut.mutate(confirmAction.id)}
        onCancel={() => setConfirmAction(null)}
      />
      <ConfirmModal
        open={confirmAction?.type === "reject"}
        title="Decline this request?"
        message="The mentee will be notified that you're unable to take them on at this time."
        confirmLabel="Decline"
        variant="destructive"
        isLoading={rejectMut.isPending}
        onConfirm={() => confirmAction && rejectMut.mutate(confirmAction.id)}
        onCancel={() => setConfirmAction(null)}
      />
    </div>
  );
}
