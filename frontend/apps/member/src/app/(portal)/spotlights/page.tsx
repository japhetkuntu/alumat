"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Star, GraduationCap, Plus, Loader2, ChevronDown,
  Send, X, CheckCircle2, Clock,
} from "lucide-react";
import { Pagination } from "@alumni/ui";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { formatDate, getInitials, cn } from "@alumni/ui";
import { getSpotlights, submitSpotlight, getMySpotlights } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import type { Spotlight } from "@/types";
import { PageHeader } from "@alumni/ui";

/* Deterministic avatar background color from name */
const AVATAR_COLORS = [
  { bg: "rgba(139,92,246,0.12)", text: "#7c3aed", border: "rgba(139,92,246,0.2)"  },
  { bg: "rgba(16,185,129,0.12)", text: "#059669", border: "rgba(16,185,129,0.2)"  },
  { bg: "rgba(245,158,11,0.12)", text: "#d97706", border: "rgba(245,158,11,0.2)"  },
  { bg: "rgba(59,130,246,0.12)", text: "#2563eb", border: "rgba(59,130,246,0.2)"  },
  { bg: "rgba(236,72,153,0.12)", text: "#be185d", border: "rgba(236,72,153,0.2)"  },
  { bg: "rgba(239,68,68,0.12)",  text: "#dc2626", border: "rgba(239,68,68,0.2)"   },
];
function avatarColor(name: string) {
  let sum = 0;
  for (const ch of name) sum += ch.charCodeAt(0);
  return AVATAR_COLORS[sum % AVATAR_COLORS.length];
}

/* ─────────────────────────────────────────────────────────────────────────
   SPOTLIGHT CARD
   ───────────────────────────────────────────────────────────────────────── */
function SpotlightCard({ spotlight, featured }: { spotlight: Spotlight; featured: boolean }) {
  const [expanded, setExpanded] = useState(false);
  const color = avatarColor(spotlight.memberName ?? "A");
  const long  = spotlight.story.length > 200;

  return (
    <div
      className={cn(
        "card overflow-hidden transition-all duration-150",
        featured && "ring-2 ring-primary/20 border-primary/40",
      )}
    >
      {/* Banner image — only if provided */}
      {spotlight.imageUrl && (
        <div className="relative w-full overflow-hidden" style={{ height: 160 }}>
          <img
            src={spotlight.imageUrl}
            alt={spotlight.title}
            className="absolute inset-0 w-full h-full object-cover"
          />
          <div className="absolute inset-0" style={{ background: "linear-gradient(to top, rgba(0,0,0,0.45) 0%, transparent 55%)" }} />
          {featured && (
            <div className="absolute top-3 left-3 inline-flex items-center gap-1.5 px-2.5 py-1 text-[10.5px] font-semibold"
              style={{ background: "rgba(0,0,0,0.35)", backdropFilter: "blur(6px)", color: "white" }}>
              <Star size={10} className="fill-white" /> Featured
            </div>
          )}
        </div>
      )}

      <div className="p-5">
        {/* Avatar + name row */}
        <div className="flex items-start gap-3 mb-4">
          <div
            className="w-10 h-10 rounded-full flex items-center justify-center shrink-0 text-[13px] font-bold overflow-hidden"
            style={{ background: color.bg, border: `1.5px solid ${color.border}`, color: color.text }}
          >
            {spotlight.memberProfilePictureUrl ? (
              <img src={spotlight.memberProfilePictureUrl} alt={spotlight.memberName ?? ""}
                className="w-full h-full object-cover" />
            ) : (
              getInitials(spotlight.memberName ?? "A")
            )}
          </div>
          <div className="flex-1 min-w-0">
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0">
                <p className="text-[14px] font-semibold truncate" style={{ color: "var(--foreground)" }}>
                  {spotlight.memberName}
                </p>
                {spotlight.memberGraduationYear && (
                  <p className="flex items-center gap-1 text-[12px]" style={{ color: "var(--muted-foreground)" }}>
                    <GraduationCap size={11} /> Class of {spotlight.memberGraduationYear}
                  </p>
                )}
              </div>
              {spotlight.featuredMonth && (
                <Badge variant="secondary" className="text-[10.5px] font-semibold shrink-0">
                  {new Date(spotlight.featuredMonth).toLocaleDateString("en-US", { month: "short", year: "numeric" })}
                </Badge>
              )}
            </div>
          </div>
        </div>

        {/* Title */}
        {!featured && (
          <p className="text-[11px] font-bold tracking-[0.1em] uppercase mb-1.5" style={{ color: "var(--primary)" }}>
            Spotlight
          </p>
        )}
        <h3
          className="font-[family-name:var(--font-display)] leading-snug mb-3"
          style={{ fontSize: featured ? "1.1rem" : "1rem", fontWeight: 700, color: "var(--foreground)" }}
        >
          {spotlight.title}
        </h3>

        {/* Story */}
        <p
          className={cn("text-[13.5px] leading-relaxed whitespace-pre-wrap", !expanded && long && "line-clamp-3")}
          style={{ color: "var(--muted-foreground)" }}
        >
          {spotlight.story}
        </p>
        {long && (
          <button
            onClick={() => setExpanded(v => !v)}
            className="mt-2 flex items-center gap-1 text-[12.5px] font-semibold transition-opacity hover:opacity-70"
            style={{ color: "var(--primary)" }}
          >
            {expanded ? "Show less" : "Read full story"}
            <ChevronDown size={13} className={cn("transition-transform duration-200", expanded && "rotate-180")} />
          </button>
        )}

        <p className="text-[11.5px] mt-3" style={{ color: "var(--muted-foreground)", opacity: 0.5 }}>
          {formatDate(spotlight.createdAt)}
        </p>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   SUBMIT DRAWER
   ───────────────────────────────────────────────────────────────────────── */
function SubmitDrawer({
  open, onClose, onSubmit, isPending,
}: {
  open: boolean;
  onClose: () => void;
  onSubmit: (data: { title: string; story: string }) => void;
  isPending: boolean;
}) {
  const [title, setTitle] = useState("");
  const [story, setStory] = useState("");
  if (!open) return null;

  return (
    <div className="fixed mobile-sheet-overlay z-50 flex items-end sm:items-center justify-center">
      <div className="absolute inset-0" style={{ background: "rgba(0,0,0,0.4)", backdropFilter: "blur(4px)" }}
        onClick={onClose} />
      <div
        className="relative w-full max-w-lg rounded-t-2xl sm:rounded-2xl p-6 space-y-5 animate-in slide-in-from-bottom-4 duration-300 bottom-sheet-scroll-90"
        style={{ background: "var(--background)", border: "1px solid var(--border)", boxShadow: "0 8px 40px rgba(0,0,0,0.2)" }}
      >
        {/* Handle */}
        <div className="flex justify-center sm:hidden mb-1">
          <div className="w-10 h-1 rounded-full" style={{ background: "var(--border)" }} />
        </div>

        <div className="flex items-start justify-between gap-3">
          <div>
            <h2
              className="font-[family-name:var(--font-display)]"
              style={{ fontSize: "1.2rem", fontWeight: 700, color: "var(--foreground)" }}
            >
              Share your story
            </h2>
            <p className="text-[13px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
              Your submission will be reviewed before publishing.
            </p>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-full flex items-center justify-center transition-colors hover:bg-secondary shrink-0"
            style={{ color: "var(--muted-foreground)" }}
          >
            <X size={16} />
          </button>
        </div>

        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="spot-title" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
              Story title
            </Label>
            <Input
              id="spot-title"
              value={title}
              onChange={e => setTitle(e.target.value)}
              placeholder="e.g. From campus to Silicon Valley"
              className="h-11 text-[14px]"
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="spot-story" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
              Your story
            </Label>
            <Textarea
              id="spot-story"
              value={story}
              onChange={e => setStory(e.target.value)}
              placeholder="Tell us about your journey, achievements, and what your time here meant to your career…"
              rows={6}
              className="text-[14px] resize-none"
            />
            <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>
              {story.length} characters
            </p>
          </div>
          <Button
            className="w-full font-semibold text-[14px] gap-2"
            style={{ height: 44 }}
            onClick={() => onSubmit({ title, story })}
            disabled={!title.trim() || story.trim().length < 20 || isPending}
          >
            {isPending
              ? <><Loader2 size={15} className="animate-spin" /> Submitting…</>
              : <><Send size={14} /> Submit for review</>}
          </Button>
        </div>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PAGE
   ───────────────────────────────────────────────────────────────────────── */
const STATUS_META: Record<string, { label: string; variant: "info" | "success" | "destructive" | "secondary" }> = {
  Pending:  { label: "Under review", variant: "info"        },
  Approved: { label: "Published",    variant: "success"     },
  Rejected: { label: "Not selected", variant: "destructive" },
};

export default function SpotlightsPage() {
  const [page,       setPage]       = useState(1);
  const [showSubmit, setShowSubmit] = useState(false);
  const [tab,        setTab]        = useState<"featured" | "mine">("featured");
  const pageSize = 8;
  const qc = useQueryClient();

  const { data: spotlightsData, isLoading } = useQuery({
    queryKey:        ["m-spotlights", page],
    queryFn:         () => getSpotlights(page, pageSize),
    enabled:         tab === "featured",
    placeholderData: (prev) => prev,
  });

  const { data: mySpotlights, isLoading: loadingMine } = useQuery({
    queryKey: ["m-my-spotlights"],
    queryFn:  getMySpotlights,
    enabled:  tab === "mine",
  });

  const submitMut = useMutation({
    mutationFn: (data: { title: string; story: string }) => submitSpotlight(data),
    onSuccess: () => {
      toast.success("Story submitted. Our team will review it shortly.");
      setShowSubmit(false);
      qc.invalidateQueries({ queryKey: ["m-my-spotlights"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const spotlights = spotlightsData?.results ?? [];
  const totalPages = spotlightsData?.totalPages ?? 1;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      {/* ── Header ── */}
      <PageHeader
        eyebrow="Stories that stay with us"
        title="Spotlight"
        description="Celebrate the wins. A spotlight on old students making waves globally and giving back to the school."
      >
        <Button
          onClick={() => setShowSubmit(true)}
          className="font-semibold gap-2 text-[13.5px] shrink-0"
          style={{ height: 42 }}
        >
          <Plus size={14} /> Share my story
        </Button>
      </PageHeader>

      {/* ── Tabs ── */}
      <div className="flex gap-2">
        {(["featured", "mine"] as const).map(t => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={cn(
              "px-4 py-2 text-[13px] font-semibold border transition-colors",
              tab === t ? "text-white border-transparent" : "border-border hover:border-primary/40",
            )}
            style={tab === t
              ? { background: "var(--primary)", color: "white" }
              : { background: "var(--background)", color: "var(--muted-foreground)" }}
          >
            {t === "featured" ? "Featured stories" : "My submissions"}
          </button>
        ))}
      </div>

      {/* ── Featured tab ── */}
      {tab === "featured" && (
        <>
          {isLoading ? (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
              {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
            </div>
          ) : spotlights.length === 0 ? (
            <EmptyState
              icon={<Star size={40} />}
              title="No honourees yet"
              description="Be the first to share your story."
              action={
                <Button onClick={() => setShowSubmit(true)} className="gap-2 font-semibold">
                  <Plus size={14} /> Share my story
                </Button>
              }
            />
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
              {spotlights.map((s, i) => (
                <div key={s.id} className={i === 0 ? "md:col-span-2" : undefined}>
                  <SpotlightCard spotlight={s} featured={i === 0} />
                </div>
              ))}
            </div>
          )}
          {totalPages > 1 && <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />}
        </>
      )}

      {/* ── My submissions tab ── */}
      {tab === "mine" && (
        <div className="max-w-[720px]">
          {loadingMine ? (
            <div className="grid gap-3">
              {Array.from({ length: 2 }).map((_, i) => <CardSkeleton key={i} />)}
            </div>
          ) : !mySpotlights?.length ? (
            <EmptyState
              icon={<Star size={40} />}
              title="No submissions yet"
              description="Share your achievements and let the community celebrate with you."
              action={
                <Button onClick={() => setShowSubmit(true)} className="gap-2 font-semibold">
                  <Plus size={14} /> Submit my story
                </Button>
              }
            />
          ) : (
            <div className="grid gap-3">
              {mySpotlights.map(s => {
                const meta = STATUS_META[s.status] ?? { label: s.status, variant: "secondary" as const };
                return (
                  <div
                    key={s.id}
                    className="card overflow-hidden"
                  >
                    {/* Status stripe */}
                    <div
                      className="h-1 w-full"
                      style={{
                        background:
                          s.status === "Approved" ? "#059669" :
                          s.status === "Rejected" ? "var(--destructive)" :
                          "var(--primary)",
                      }}
                    />
                    <div className="p-4 sm:p-5">
                      <div className="flex items-start justify-between gap-3 mb-2">
                        <h3 className="text-[14.5px] font-semibold leading-snug" style={{ color: "var(--foreground)" }}>
                          {s.title}
                        </h3>
                        <Badge variant={meta.variant} className="text-[11px] font-semibold shrink-0">
                          {meta.label}
                        </Badge>
                      </div>
                      <p className="text-[13px] line-clamp-2 leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
                        {s.story}
                      </p>
                      <div className="flex items-center gap-4 mt-3">
                        <p className="text-[12px]" style={{ color: "var(--muted-foreground)", opacity: 0.6 }}>
                          Submitted {formatDate(s.createdAt)}
                        </p>
                        {s.status === "Pending" && (
                          <p className="flex items-center gap-1 text-[12px] font-medium" style={{ color: "var(--primary)" }}>
                            <Clock size={11} /> Awaiting review
                          </p>
                        )}
                        {s.status === "Approved" && (
                          <p className="flex items-center gap-1 text-[12px] font-medium text-success">
                            <CheckCircle2 size={11} /> Published
                          </p>
                        )}
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      <SubmitDrawer
        open={showSubmit}
        onClose={() => setShowSubmit(false)}
        onSubmit={data => submitMut.mutate(data)}
        isPending={submitMut.isPending}
      />
    </div>
  );
}
