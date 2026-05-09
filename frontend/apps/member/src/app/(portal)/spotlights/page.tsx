"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Star, GraduationCap, Plus, Loader2, Quote, Sparkles,
  ChevronRight, Send, X, CheckCircle,
} from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { formatDate, getInitials, cn } from "@/lib/utils";
import { getSpotlights, submitSpotlight, getMySpotlights } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@/components/ui/skeleton";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import type { Spotlight } from "@/types";

const GRADIENT_PAIRS = [
  "from-violet-500 to-indigo-500",
  "from-emerald-500 to-teal-500",
  "from-orange-500 to-rose-500",
  "from-blue-500 to-cyan-500",
  "from-pink-500 to-purple-500",
  "from-amber-500 to-orange-500",
];

function gradientForName(name: string) {
  let sum = 0;
  for (const ch of name) sum += ch.charCodeAt(0);
  return GRADIENT_PAIRS[sum % GRADIENT_PAIRS.length];
}

function SpotlightHeroCard({ spotlight, rank }: { spotlight: Spotlight; rank: number }) {
  const [expanded, setExpanded] = useState(false);
  const grad = gradientForName(spotlight.memberName ?? "A");
  const isFirst = rank === 0;

  return (
    <div
      className={cn(
        "group relative rounded-2xl sm:rounded-3xl overflow-hidden border border-border/40 transition-all duration-300",
        isFirst
          ? "shadow-2xl shadow-primary/10 hover:shadow-2xl hover:shadow-primary/20"
          : "shadow-sm hover:shadow-lg hover:shadow-black/5",
      )}
    >
      {/* Gradient header banner */}
      <div className={cn("relative bg-gradient-to-br", grad, isFirst ? "h-40 sm:h-52" : "h-28 sm:h-36")}>
        {spotlight.imageUrl && (
          <img
            src={spotlight.imageUrl}
            alt={spotlight.title}
            className="absolute inset-0 w-full h-full object-cover opacity-30 mix-blend-overlay"
          />
        )}
        <div className="absolute -top-8 -right-8 w-40 h-40 rounded-full bg-white/10" />
        <div className="absolute -bottom-12 -left-8 w-56 h-56 rounded-full bg-white/5" />
        {isFirst && (
          <div className="absolute top-4 left-4 flex items-center gap-1.5 bg-white/20 backdrop-blur-sm rounded-full px-3 py-1">
            <Sparkles size={11} className="text-white" />
            <span className="text-[11px] font-black text-white uppercase tracking-widest">Featured</span>
          </div>
        )}
        <div className="absolute bottom-3 right-4">
          <Quote size={isFirst ? 48 : 32} className="text-white/10" />
        </div>
        <div className="absolute -bottom-6 left-5 sm:left-6">
          <div className="p-1 rounded-full bg-background shadow-xl">
            <Avatar className={cn("ring-2 ring-background shadow-md", isFirst ? "h-14 w-14 sm:h-16 sm:w-16" : "h-12 w-12")}>
              {spotlight.memberProfilePictureUrl && (
                <img src={spotlight.memberProfilePictureUrl} alt={spotlight.memberName ?? ""} className="object-cover" />
              )}
              <AvatarFallback className={cn("text-white font-black text-lg bg-gradient-to-br", grad)}>
                {getInitials(spotlight.memberName ?? "A")}
              </AvatarFallback>
            </Avatar>
          </div>
        </div>
      </div>

      {/* Body */}
      <div className="bg-background pt-9 pb-5 px-5 sm:px-6">
        <div className="flex items-start justify-between gap-3 mb-3">
          <div>
            <h3 className={cn("font-black tracking-tight leading-tight", isFirst ? "text-xl sm:text-2xl" : "text-lg")}>
              {spotlight.title}
            </h3>
            <div className="flex items-center gap-2 mt-1 flex-wrap">
              <span className="font-semibold text-sm text-muted-foreground">{spotlight.memberName}</span>
              {spotlight.memberGraduationYear && (
                <>
                  <span className="text-muted-foreground/40">·</span>
                  <span className="flex items-center gap-1 text-xs text-muted-foreground/70">
                    <GraduationCap size={12} />
                    Class of {spotlight.memberGraduationYear}
                  </span>
                </>
              )}
            </div>
          </div>
          {spotlight.featuredMonth && (
            <Badge variant="secondary" className="shrink-0 text-[10px] font-bold uppercase tracking-wide">
              {new Date(spotlight.featuredMonth).toLocaleDateString("en-US", { month: "short", year: "numeric" })}
            </Badge>
          )}
        </div>
        <div className="relative">
          <p className={cn(
            "text-muted-foreground text-[14px] leading-relaxed whitespace-pre-wrap",
            !expanded && "line-clamp-3",
          )}>
            {spotlight.story}
          </p>
          {spotlight.story.length > 180 && (
            <button
              onClick={() => setExpanded((v) => !v)}
              className="mt-1.5 text-[12px] font-bold text-primary hover:underline flex items-center gap-0.5"
            >
              {expanded ? "Show less" : "Read full story"}
              <ChevronRight size={12} className={cn("transition-transform", expanded && "rotate-90")} />
            </button>
          )}
        </div>
        <p className="text-[11px] text-muted-foreground/40 mt-3">{formatDate(spotlight.createdAt)}</p>
      </div>
    </div>
  );
}

function SubmitDrawer({
  open,
  onClose,
  onSubmit,
  isPending,
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
    <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative w-full max-w-lg bg-background rounded-t-3xl sm:rounded-2xl shadow-2xl p-6 space-y-5 animate-in slide-in-from-bottom-4 duration-300 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-xl font-black">Share Your Story</h2>
            <p className="text-sm text-muted-foreground mt-0.5">Your submission will be reviewed before publishing.</p>
          </div>
          <button onClick={onClose} className="p-2 rounded-xl hover:bg-muted transition-colors">
            <X size={18} />
          </button>
        </div>
        <div className="space-y-4">
          <div>
            <Label className="font-bold mb-1.5 block">Spotlight Title</Label>
            <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. From UMaT to Silicon Valley" className="h-11" />
          </div>
          <div>
            <Label className="font-bold mb-1.5 block">Your Story</Label>
            <Textarea value={story} onChange={(e) => setStory(e.target.value)} placeholder="Tell us about your journey, achievements, and what UMaT meant to your career..." rows={6} className="resize-none" />
            <p className="text-[11px] text-muted-foreground mt-1">{story.length} characters</p>
          </div>
          <Button
            className="w-full h-11 font-bold text-base"
            onClick={() => onSubmit({ title, story })}
            disabled={!title.trim() || story.trim().length < 20 || isPending}
          >
            {isPending ? <Loader2 className="w-4 h-4 mr-2 animate-spin" /> : <Send className="w-4 h-4 mr-2" />}
            Submit for Review
          </Button>
        </div>
      </div>
    </div>
  );
}

export default function SpotlightsPage() {
  const [page, setPage] = useState(1);
  const [showSubmit, setShowSubmit] = useState(false);
  const [tab, setTab] = useState<"featured" | "mine">("featured");
  const pageSize = 8;
  const qc = useQueryClient();

  const { data: spotlightsData, isLoading } = useQuery({
    queryKey: ["m-spotlights", page],
    queryFn: () => getSpotlights(page, pageSize),
    enabled: tab === "featured",
  });

  const { data: mySpotlights, isLoading: loadingMine } = useQuery({
    queryKey: ["m-my-spotlights"],
    queryFn: getMySpotlights,
    enabled: tab === "mine",
  });

  const submitMut = useMutation({
    mutationFn: (data: { title: string; story: string }) => submitSpotlight(data),
    onSuccess: () => {
      toast.success("Story submitted! Our team will review it shortly.", { duration: 5000 });
      setShowSubmit(false);
      qc.invalidateQueries({ queryKey: ["m-my-spotlights"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const spotlights = spotlightsData?.results ?? [];
  const totalPages = spotlightsData?.totalPages ?? 1;

  const statusMeta: Record<string, { label: string; variant: "info" | "success" | "destructive" | "secondary" }> = {
    Pending:  { label: "Under Review", variant: "info" },
    Approved: { label: "Published",    variant: "success" },
    Rejected: { label: "Not selected", variant: "destructive" },
  };

  return (
    <div className="w-full max-w-[900px] mx-auto selection:bg-primary/20">
      {/* Hero intro */}
      <div className="relative overflow-hidden rounded-none sm:rounded-3xl bg-gradient-to-br from-primary/10 via-primary/5 to-transparent border-b sm:border border-border/40 px-4 sm:px-10 py-10 sm:py-14 mb-6 sm:mb-10">
        <div className="absolute top-0 right-0 w-72 h-72 bg-primary/5 rounded-full -translate-y-1/2 translate-x-1/3" />
        <div className="absolute bottom-0 left-0 w-48 h-48 bg-primary/5 rounded-full translate-y-1/2 -translate-x-1/3" />
        <div className="relative">
          <div className="flex items-center gap-2 mb-3">
            <Star size={18} className="text-primary fill-primary" />
            <span className="text-[11px] font-black uppercase tracking-[0.2em] text-primary">Alumni Spotlights</span>
          </div>
          <h1 className="text-3xl sm:text-4xl lg:text-5xl font-black tracking-tight leading-tight mb-3">
            Celebrating Our<br className="hidden sm:block" /> Alumni&apos;s Achievements
          </h1>
          <p className="text-muted-foreground text-base sm:text-lg font-medium max-w-xl leading-relaxed mb-6">
            Discover the inspiring stories of UMaT graduates making an impact across the world.
          </p>
          <Button size="lg" className="font-bold shadow-lg shadow-primary/20 h-12 px-6" onClick={() => setShowSubmit(true)}>
            <Plus size={16} className="mr-2" />
            Share My Story
          </Button>
        </div>
      </div>

      <div className="px-2 sm:px-0 space-y-6 pb-10">
        {/* Tabs */}
        <div className="flex gap-2">
          {(["featured", "mine"] as const).map((t) => (
            <button
              key={t}
              onClick={() => setTab(t)}
              className={cn(
                "px-5 py-2 rounded-full text-[12px] font-black uppercase tracking-wider transition-all",
                tab === t
                  ? "bg-primary text-primary-foreground shadow-md"
                  : "bg-muted/60 text-muted-foreground hover:bg-muted",
              )}
            >
              {t === "featured" ? "✦ Featured Stories" : "My Submissions"}
            </button>
          ))}
        </div>

        {tab === "featured" && (
          <>
            {isLoading ? (
              <div className="grid gap-5">
                {Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}
              </div>
            ) : spotlights.length === 0 ? (
              <div className="text-center py-20">
                <Star size={48} className="mx-auto mb-3 text-muted-foreground/20" />
                <p className="font-bold text-lg">No spotlights yet</p>
                <p className="text-muted-foreground text-sm mt-1">Be the first to share your story!</p>
                <Button className="mt-4" onClick={() => setShowSubmit(true)}>Share My Story</Button>
              </div>
            ) : (
              <div className="grid gap-5">
                {spotlights.map((s, i) => (
                  <SpotlightHeroCard key={s.id} spotlight={s} rank={i} />
                ))}
              </div>
            )}
            {totalPages > 1 && <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />}
          </>
        )}

        {tab === "mine" && (
          <>
            {loadingMine ? (
              <div className="grid gap-4">
                {Array.from({ length: 2 }).map((_, i) => <CardSkeleton key={i} />)}
              </div>
            ) : !mySpotlights?.length ? (
              <div className="text-center py-20">
                <Sparkles size={40} className="mx-auto mb-3 text-muted-foreground/20" />
                <p className="font-bold text-lg">No submissions yet</p>
                <p className="text-muted-foreground text-sm mt-1 max-w-sm mx-auto">
                  Share your achievements and let the community celebrate with you.
                </p>
                <Button className="mt-4" onClick={() => setShowSubmit(true)}>Submit My Story</Button>
              </div>
            ) : (
              <div className="grid gap-4">
                {mySpotlights.map((s) => {
                  const meta = statusMeta[s.status] ?? { label: s.status, variant: "secondary" as const };
                  return (
                    <Card key={s.id} className="overflow-hidden">
                      <CardContent className="p-0">
                        <div className={cn("h-1.5 w-full", {
                          "bg-blue-500": s.status === "Pending",
                          "bg-emerald-500": s.status === "Approved",
                          "bg-red-500": s.status === "Rejected",
                        })} />
                        <div className="p-4 sm:p-5">
                          <div className="flex items-start justify-between gap-3">
                            <h3 className="font-bold">{s.title}</h3>
                            <Badge variant={meta.variant} className="shrink-0">{meta.label}</Badge>
                          </div>
                          <p className="text-sm text-muted-foreground mt-1.5 line-clamp-2">{s.story}</p>
                          <p className="text-[11px] text-muted-foreground/50 mt-3">Submitted {formatDate(s.createdAt)}</p>
                          {s.status === "Pending" && (
                            <p className="text-[12px] text-blue-600 dark:text-blue-400 mt-2 flex items-center gap-1">
                              <Loader2 size={11} className="animate-spin" />
                              Awaiting review by our team
                            </p>
                          )}
                          {s.status === "Approved" && (
                            <p className="text-[12px] text-emerald-600 dark:text-emerald-400 mt-2 flex items-center gap-1">
                              <CheckCircle size={11} />
                              Your story is now featured!
                            </p>
                          )}
                        </div>
                      </CardContent>
                    </Card>
                  );
                })}
              </div>
            )}
          </>
        )}
      </div>

      <SubmitDrawer
        open={showSubmit}
        onClose={() => setShowSubmit(false)}
        onSubmit={(data) => submitMut.mutate(data)}
        isPending={submitMut.isPending}
      />
    </div>
  );
}
