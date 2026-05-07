"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Star, Eye, GraduationCap, Plus, Loader2 } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { formatDate } from "@/lib/utils";
import { getSpotlights, submitSpotlight, getMySpotlights } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { getInitials } from "@/lib/utils";
import Link from "next/link";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";

export default function SpotlightsPage() {
  const [page, setPage] = useState(1);
  const [showSubmit, setShowSubmit] = useState(false);
  const [tab, setTab] = useState<"approved" | "mine">("approved");
  const [title, setTitle] = useState("");
  const [story, setStory] = useState("");
  const pageSize = 10;
  const qc = useQueryClient();

  const { data: spotlightsData, isLoading } = useQuery({
    queryKey: ["m-spotlights", page],
    queryFn: () => getSpotlights(page, pageSize),
    enabled: tab === "approved",
  });

  const { data: mySpotlights, isLoading: loadingMine } = useQuery({
    queryKey: ["m-my-spotlights"],
    queryFn: getMySpotlights,
    enabled: tab === "mine",
  });

  const submitMut = useMutation({
    mutationFn: () => submitSpotlight({ title, story }),
    onSuccess: () => {
      toast.success("Spotlight submitted for review!");
      setShowSubmit(false);
      setTitle("");
      setStory("");
      qc.invalidateQueries({ queryKey: ["m-my-spotlights"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const spotlights = spotlightsData?.results ?? [];
  const totalPages = spotlightsData?.totalPages ?? 1;

  const statusVariant: Record<string, "info" | "success" | "destructive" | "secondary"> = {
    Pending: "info",
    Approved: "success",
    Rejected: "destructive",
  };

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-6 sm:space-y-8 lg:space-y-10 selection:bg-primary/20">
      <header className="flex items-start justify-between gap-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div className="space-y-2">
          <h1 className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tight text-foreground">
            Alumni Spotlights
          </h1>
          <p className="text-muted-foreground text-sm sm:text-base lg:text-lg font-medium leading-relaxed max-w-2xl">
            Celebrate the achievements and success stories of fellow UMaT alumni.
          </p>
        </div>
        <Dialog open={showSubmit} onOpenChange={setShowSubmit}>
          <DialogTrigger asChild>
            <Button size="sm" className="shrink-0">
              <Plus className="w-4 h-4 mr-1" /> Submit Story
            </Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Submit Your Spotlight</DialogTitle>
              <DialogDescription>Share your success story with the alumni community. It will be reviewed by an admin before publishing.</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 pt-2">
              <div>
                <Label htmlFor="title">Title</Label>
                <Input id="title" value={title} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. From UMaT to Google..." />
              </div>
              <div>
                <Label htmlFor="story">Your Story</Label>
                <Textarea id="story" value={story} onChange={(e) => setStory(e.target.value)} placeholder="Share your journey..." rows={6} />
              </div>
              <Button onClick={() => submitMut.mutate()} disabled={!title.trim() || !story.trim() || submitMut.isPending} className="w-full">
                {submitMut.isPending && <Loader2 className="w-4 h-4 mr-2 animate-spin" />}
                Submit for Review
              </Button>
            </div>
          </DialogContent>
        </Dialog>
      </header>

      <div className="flex gap-2">
        <Button variant={tab === "approved" ? "default" : "outline"} size="sm" onClick={() => setTab("approved")}>
          Featured
        </Button>
        <Button variant={tab === "mine" ? "default" : "outline"} size="sm" onClick={() => setTab("mine")}>
          My Submissions
        </Button>
      </div>

      {tab === "approved" && (
        <>
          {isLoading ? (
            <div className="grid gap-6">
              {Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}
            </div>
          ) : spotlights.length === 0 ? (
            <EmptyState icon={<Star size={48} />} title="No spotlights yet" description="Be the first to share your story!" />
          ) : (
            <div className="grid gap-6">
              {spotlights.map((s) => (
                <Card key={s.id} className="overflow-hidden hover:shadow-md transition-shadow">
                  <CardContent className="p-5 sm:p-6">
                    <div className="flex items-start gap-4">
                      <Avatar className="w-12 h-12 shrink-0">
                        <AvatarFallback>{getInitials(s.memberName ?? "")}</AvatarFallback>
                      </Avatar>
                      <div className="flex-1 min-w-0 space-y-2">
                        <h3 className="font-bold text-lg">{s.title}</h3>
                        <div className="flex items-center gap-2 text-sm text-muted-foreground">
                          <span>{s.memberName}</span>
                          {s.memberGraduationYear && (
                            <>
                              <span>·</span>
                              <span className="flex items-center gap-1">
                                <GraduationCap className="w-3.5 h-3.5" /> Class of {s.memberGraduationYear}
                              </span>
                            </>
                          )}
                          <span>·</span>
                          <span>{formatDate(s.createdAt)}</span>
                        </div>
                        <p className="text-sm text-muted-foreground leading-relaxed whitespace-pre-wrap">{s.story}</p>
                      </div>
                    </div>
                  </CardContent>
                </Card>
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
            <EmptyState icon={<Star size={48} />} title="No submissions yet" description="Submit your spotlight story to share with the community." />
          ) : (
            <div className="grid gap-4">
              {mySpotlights.map((s) => (
                <Card key={s.id}>
                  <CardContent className="p-4 sm:p-5">
                    <div className="flex items-center justify-between gap-2 mb-2">
                      <h3 className="font-semibold">{s.title}</h3>
                      <Badge variant={statusVariant[s.status] ?? "secondary"}>{s.status}</Badge>
                    </div>
                    <p className="text-sm text-muted-foreground line-clamp-2">{s.story}</p>
                    <p className="text-xs text-muted-foreground mt-2">Submitted {formatDate(s.createdAt)}</p>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
