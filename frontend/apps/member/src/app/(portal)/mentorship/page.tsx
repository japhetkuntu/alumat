"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  UserCheck, Plus, CheckCircle, XCircle, Clock,
  Inbox, Users, ChevronDown, Linkedin, MessageCircle, Phone,
  Sparkles, GraduationCap, HeartHandshake,
} from "lucide-react";
import { Avatar, AvatarFallback, AvatarImage } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { getInitials, formatDate, cn } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import {
  getMentors, requestMentorship, registerAsMentor,
  getMyMentorshipRequests, getMyMentorProfile,
  getIncomingMentorshipRequests, acceptMentorshipRequest,
  rejectMentorshipRequest,
} from "@/lib/member-api";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
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
  const [mentorForm,    setMentorForm]    = useState({
    area: "", bio: "", maxMentees: "3",
    contactLinkedInUrl: "", contactWhatsAppNumber: "", contactPhoneNumber: "",
  });
  const [confirmAction, setConfirmAction] = useState<{ type: "accept" | "reject"; id: string } | null>(null);
  const qc = useQueryClient();

  const { data: mentorsData, isLoading } = useQuery({
    queryKey: ["m-mentors"],
    queryFn:  () => getMentors(1, 30),
  });

  const { data: requestsData } = useQuery({
    queryKey: ["m-my-requests"],
    queryFn:  () => getMyMentorshipRequests(),
  });

  const { data: myProfile } = useQuery({
    queryKey: ["m-my-mentor-profile"],
    queryFn:  getMyMentorProfile,
    retry:    false,
  });

  const { data: incomingData } = useQuery({
    queryKey: ["m-incoming-requests"],
    queryFn:  () => getIncomingMentorshipRequests(),
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
      area:                  mentorForm.area,
      bio:                   mentorForm.bio || undefined,
      maxMentees:            Number(mentorForm.maxMentees),
      contactLinkedInUrl:    mentorForm.contactLinkedInUrl || undefined,
      contactWhatsAppNumber: mentorForm.contactWhatsAppNumber || undefined,
      contactPhoneNumber:    mentorForm.contactPhoneNumber || undefined,
    }),
    onSuccess: () => {
      toast.success("Application submitted. You'll be notified once approved.");
      setMentorForm({ area: "", bio: "", maxMentees: "3", contactLinkedInUrl: "", contactWhatsAppNumber: "", contactPhoneNumber: "" });
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
      <PageHeader
        eyebrow="Community · Growth"
        title="Mentorship"
        description="Find a mentor, or give back by mentoring fellow alumni."
      />

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
        <div>
          {requests.length === 0 ? (
            <EmptyState
              icon={<Clock size={40} />}
              title="No requests sent yet"
              description="Browse available mentors and send a request."
            />
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
              {requests.map(r => (
                <div key={r.id} className="card p-5 space-y-3">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="text-[14.5px] font-semibold truncate" style={{ color: "var(--foreground)" }}>
                        {r.area}
                      </p>
                      <p className="text-[12px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                        {r.mentorProfileName ? `To: ${r.mentorProfileName}` : ""}
                        {r.mentorProfileName && " · "}
                        {formatDate(r.createdAt)}
                      </p>
                    </div>
                    <Badge variant={reqStatusVariant[r.status]} className="text-[11px] font-semibold shrink-0">
                      {r.status}
                    </Badge>
                  </div>
                  {r.message && (
                    <p className="text-[13px] leading-relaxed italic" style={{ color: "var(--muted-foreground)" }}>
                      &ldquo;{r.message}&rdquo;
                    </p>
                  )}
                  {r.status === "Accepted" && (r.contactLinkedInUrl || r.contactWhatsAppNumber || r.contactPhoneNumber) && (
                    <div
                      className="p-3 rounded-xl space-y-2"
                      style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
                    >
                      <p className="text-[12px] font-semibold" style={{ color: "var(--foreground)" }}>
                        Contact your mentor
                      </p>
                      <div className="flex flex-wrap gap-2">
                        {r.contactLinkedInUrl && (
                          <a
                            href={r.contactLinkedInUrl}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="flex items-center gap-1.5 text-[12.5px] font-semibold px-3 py-1.5 rounded-full border"
                            style={{ color: "var(--primary)", borderColor: "var(--primary)" }}
                          >
                            <Linkedin size={13} /> LinkedIn
                          </a>
                        )}
                        {r.contactWhatsAppNumber && (
                          <a
                            href={`https://wa.me/${r.contactWhatsAppNumber.replace(/[^\d+]/g, "").replace(/^\+/, "")}`}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="flex items-center gap-1.5 text-[12.5px] font-semibold px-3 py-1.5 rounded-full border"
                            style={{ color: "var(--primary)", borderColor: "var(--primary)" }}
                          >
                            <MessageCircle size={13} /> WhatsApp
                          </a>
                        )}
                        {r.contactPhoneNumber && (
                          <a
                            href={`tel:${r.contactPhoneNumber}`}
                            className="flex items-center gap-1.5 text-[12.5px] font-semibold px-3 py-1.5 rounded-full border"
                            style={{ color: "var(--primary)", borderColor: "var(--primary)" }}
                          >
                            <Phone size={13} /> Call
                          </a>
                        )}
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* ══════════════════════════════════════════════
          INCOMING REQUESTS
      ══════════════════════════════════════════════ */}
      {view === "incoming" && (
        <div>
          {incoming.length === 0 ? (
            <EmptyState
              icon={<Inbox size={40} />}
              title="No incoming requests"
              description="Mentorship requests from other alumni will appear here."
            />
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
              {incoming.map(r => (
                <div key={r.id} className="card p-5 space-y-4">
                  {/* Mentee info */}
                  <div className="flex items-start gap-3">
                    <Avatar size="default" className="shrink-0">
                      {r.menteeProfilePictureUrl && (
                        <AvatarImage src={r.menteeProfilePictureUrl} alt={r.menteeName ?? "M"} />
                      )}
                      <AvatarFallback name={r.menteeName ?? "M"}>
                        {getInitials(r.menteeName ?? "M")}
                      </AvatarFallback>
                    </Avatar>
                    <div className="min-w-0 flex-1">
                      <div className="flex items-start justify-between gap-2">
                        <p className="text-[14.5px] font-semibold truncate" style={{ color: "var(--foreground)" }}>
                          {r.menteeName ?? "Unknown member"}
                        </p>
                        {r.status !== "Pending" && (
                          <Badge variant={reqStatusVariant[r.status]} className="text-[11px] font-semibold shrink-0">
                            {r.status}
                          </Badge>
                        )}
                      </div>
                      <p className="text-[12px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                        {r.area} · {formatDate(r.createdAt)}
                      </p>
                      {r.message && (
                        <p className="text-[12.5px] mt-1.5 italic leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
                          &ldquo;{r.message}&rdquo;
                        </p>
                      )}
                    </div>
                  </div>

                  {/* Actions */}
                  {r.status === "Pending" && (
                    <div className="flex items-center gap-2">
                      <Button
                        size="sm"
                        className="flex-1 gap-1.5 font-semibold text-[13px]"
                        style={{ height: 38 }}
                        onClick={() => setConfirmAction({ type: "accept", id: r.id })}
                      >
                        <CheckCircle size={13} /> Accept
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        className="flex-1 gap-1.5 font-semibold text-[13px]"
                        style={{ height: 38, borderColor: "var(--destructive)", color: "var(--destructive)" }}
                        onClick={() => setConfirmAction({ type: "reject", id: r.id })}
                      >
                        <XCircle size={13} /> Decline
                      </Button>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* ══════════════════════════════════════════════
          BECOME A MENTOR
      ══════════════════════════════════════════════ */}
      {view === "become" && (
        <div className="max-w-[1100px] mx-auto">
          {myProfile && myProfile.status !== "Rejected" ? (
            <div
              className="card p-8 text-center space-y-3 max-w-md mx-auto"
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
                  Your application is under review. You&apos;ll be notified once it&apos;s approved.
                </p>
              )}
            </div>
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-[1.3fr_1fr] gap-5 items-start">
            <div
              className="card p-5 sm:p-6 space-y-5"
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
                <div className="space-y-1.5 pt-1">
                  <Label className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    How should mentees reach you?{" "}
                    <span className="font-normal" style={{ color: "var(--muted-foreground)" }}>(optional)</span>
                  </Label>
                  <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>
                    Shared only with a mentee once you accept their request — fill in whichever you&apos;re comfortable sharing.
                  </p>
                  <Input
                    placeholder="LinkedIn profile URL"
                    value={mentorForm.contactLinkedInUrl}
                    onChange={e => setMentorForm(f => ({ ...f, contactLinkedInUrl: e.target.value }))}
                    className="h-11 text-[14px]"
                  />
                  <Input
                    placeholder="WhatsApp number"
                    value={mentorForm.contactWhatsAppNumber}
                    onChange={e => setMentorForm(f => ({ ...f, contactWhatsAppNumber: e.target.value }))}
                    className="h-11 text-[14px]"
                  />
                  <Input
                    placeholder="Phone number"
                    value={mentorForm.contactPhoneNumber}
                    onChange={e => setMentorForm(f => ({ ...f, contactPhoneNumber: e.target.value }))}
                    className="h-11 text-[14px]"
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

            {/* Why mentor sidebar */}
            <div className="card p-5 sm:p-6 space-y-5" style={{ background: "var(--secondary)" }}>
              <div>
                <div
                  className="w-11 h-11 rounded-xl flex items-center justify-center mb-3"
                  style={{ background: "var(--color-background-info)" }}
                >
                  <HeartHandshake size={20} style={{ color: "var(--primary)" }} />
                </div>
                <h3 className="text-[15px] font-semibold" style={{ color: "var(--foreground)" }}>
                  Why alumni mentor
                </h3>
                <p className="text-[13px] mt-1 leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
                  A few hours of your time can change the direction of a fellow graduate&apos;s career.
                </p>
              </div>
              <ul className="space-y-3.5">
                <li className="flex items-start gap-2.5">
                  <GraduationCap size={16} className="mt-0.5 shrink-0" style={{ color: "var(--primary)" }} />
                  <span className="text-[13px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
                    Guide recent graduates through career decisions, further study, or industry moves.
                  </span>
                </li>
                <li className="flex items-start gap-2.5">
                  <Sparkles size={16} className="mt-0.5 shrink-0" style={{ color: "var(--primary)" }} />
                  <span className="text-[13px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
                    Set your own capacity — you choose how many mentees to take on at once.
                  </span>
                </li>
                <li className="flex items-start gap-2.5">
                  <Users size={16} className="mt-0.5 shrink-0" style={{ color: "var(--primary)" }} />
                  <span className="text-[13px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
                    Your contact details stay private until you personally accept a mentee&apos;s request.
                  </span>
                </li>
              </ul>
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
