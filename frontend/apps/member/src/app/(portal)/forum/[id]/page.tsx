"use client";

import { ReportButton } from "@/components/member/report-button";
import { Fragment, useState } from "react";
import { formatDistanceToNowStrict } from "date-fns";
import { useParams, useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Send, Pin, Lock, MessageSquare, Clock, Check } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { toast } from "sonner";

import { Button } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { getThreadPosts, replyToThread, getForumThread, toggleHelpful } from "@/lib/member-api";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";
import { cn } from "@alumni/ui";

/* Safe date formatter — never throws on undefined / invalid input */
/** "3 days ago", never throwing on a bad date. The full date stays as the hover title. */
function ago(value: string | null | undefined): string {
  if (!value) return "";
  const d = new Date(value);
  return isNaN(d.getTime()) ? "" : `${formatDistanceToNowStrict(d)} ago`;
}

function safeDate(value: string | null | undefined): string {
  if (!value) return "";
  const d = new Date(value);
  if (isNaN(d.getTime())) return "";
  return formatDate(value);
}

export default function ThreadDetailPage() {
  const { id }  = useParams<{ id: string }>();
  const router  = useRouter();
  const qc      = useQueryClient();
  const [page,  setPage]  = useState(1);
  const [reply, setReply] = useState("");
  const { user } = useAuth();
  const pageSize = 30;

  const { data: thread, isLoading: threadLoading } = useQuery({
    queryKey: ["m-forum-thread", id],
    queryFn:  () => getForumThread(id),
  });

  const { data, isLoading: postsLoading } = useQuery({
    queryKey: ["m-thread-posts", id, page],
    queryFn:  () => getThreadPosts(id, page, pageSize),
  });

  const replyMut = useMutation({
    mutationFn: () => replyToThread(id, reply),
    onSuccess: () => {
      setReply("");
      qc.invalidateQueries({ queryKey: ["m-thread-posts", id] });
      // replyCount is shown on this thread's own header, the forum list, and
      // the community preview card — all need to reflect the new count.
      qc.invalidateQueries({ queryKey: ["m-forum-thread", id] });
      qc.invalidateQueries({ queryKey: ["m-forum-threads"] });
      qc.invalidateQueries({ queryKey: ["m-community-threads-preview"] });
      toast.success("Reply posted.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  // Marking is instant on screen and settled by the server; nobody is told, so it never adds to anyone's notifications.
  const helpfulMut = useMutation({
    mutationFn: (postId: string) => toggleHelpful(postId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["m-thread-posts", id] }),
    onError: (e) => toast.error(handleApiError(e)),
  });

  const posts      = data?.results ?? [];
  // The reply the most people found helpful, if any did: it earns a badge so a good answer is easy to spot.
  const topHelpful = posts.slice(1).reduce<{ id: string; n: number } | null>((best, p) => ((p.helpfulCount ?? 0) > (best?.n ?? 0) ? { id: p.id, n: p.helpfulCount ?? 0 } : best), null);
  const totalPages = data?.totalPages ?? 1;

  /* ── Loading ── */
  if (threadLoading || postsLoading) {
    return (
      <div className="p-4 sm:p-6 lg:p-10 max-w-[1400px] mx-auto space-y-6 animate-pulse">
        <div className="h-6 w-24 rounded-lg" style={{ background: "var(--secondary)" }} />
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  if (!thread) return (
    <div className="p-4 sm:p-6 lg:p-10 max-w-[1400px] mx-auto">
      <button
        onClick={() => router.push("/forum")}
        className="flex items-center gap-1.5 text-[13.5px] font-semibold mb-6 transition-colors hover:underline"
        style={{ color: "var(--muted-foreground)" }}
      >
        <ArrowLeft size={15} /> Back to forum
      </button>
      <EmptyState
        icon={<MessageSquare size={40} />}
        title="Thread not found"
        description="This thread may have been removed or the link is invalid."
      />
    </div>
  );

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto pb-20 space-y-6 sm:space-y-8">

      {/* ── Nav ── */}
      <button
        onClick={() => router.push(thread.communityId ? `/communities/${thread.communityId}` : "/forum")}
        className="flex items-center gap-1.5 text-[13.5px] font-semibold transition-colors hover:underline"
        style={{ color: "var(--muted-foreground)" }}
      >
        <ArrowLeft size={15} /> {thread.communityId ? "Back to community" : "Back to forum"}
      </button>

      {/* ── Thread header ── */}
      <div className="space-y-3">
        {/* Labels */}
        <div className="flex flex-wrap items-center gap-2">
          {thread.categoryName && (
            <Badge variant="info">{thread.categoryName}</Badge>
          )}
          {thread.isPinned && (
            <Badge variant="warning" className="gap-1.5">
              <Pin size={10} /> Pinned
            </Badge>
          )}
          {thread.isClosed && (
            <Badge variant="neutral" className="gap-1.5">
              <Lock size={10} /> Closed
            </Badge>
          )}
        </div>

        {/* Title */}
        <h1
          className="font-[family-name:var(--font-display)] leading-tight"
          style={{ fontSize: "clamp(1.25rem, 3vw, 1.75rem)", fontWeight: 700, letterSpacing: "-0.02em", color: "var(--foreground)" }}
        >
          {thread.title}
        </h1>

        {/* Meta */}
        <div className="flex flex-wrap items-center gap-4">
          {safeDate(thread.createdAt) && (
            <span className="flex items-center gap-1.5 text-[13px]" style={{ color: "var(--muted-foreground)" }}>
              <Clock size={13} style={{ color: "var(--primary)" }} />
              {safeDate(thread.createdAt)}
            </span>
          )}
          <span className="flex items-center gap-1.5 text-[13px]" style={{ color: "var(--muted-foreground)" }}>
            <MessageSquare size={13} style={{ color: "var(--primary)" }} />
            {thread.replyCount ?? 0} {(thread.replyCount ?? 0) === 1 ? "reply" : "replies"}
          </span>
          <ReportButton entityType="ForumThread" entityId={thread.id} entityTitle={thread.title} />
        </div>
      </div>

      {/* ── Posts ── */}
      {posts.length === 0 ? (
        <EmptyState
          icon={<MessageSquare size={40} />}
          title="No replies yet"
          description="A reply can be advice, an answer or a personal story. Be the first to respond."
          action={<Button variant="outline" size="sm" className="font-semibold" onClick={() => document.getElementById("forum-reply")?.focus()}>Write a reply</Button>}
        />
      ) : (
        <div className="space-y-3">
          {posts.map((post, i) => {
            const authorName = post.authorName ?? "Member";
            const isOriginal = i === 0 && page === 1;
            const postedAt   = safeDate(post.createdAt);

            return (
              <Fragment key={post.id}>
              <div
                className={cn("flex gap-3 sm:gap-4 card p-4 sm:p-5", isOriginal && "bg-primary/5 border-primary/20")}
              >
                {/* Avatar */}
                <div className="shrink-0">
                  <UserAvatar
                    src={post.authorProfilePictureUrl}
                    name={authorName}
                    size="sm"
                  />
                </div>

                {/* Content */}
                <div className="flex-1 min-w-0 space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>
                      {authorName}
                    </span>
                    {isOriginal && (
                      <Badge variant="secondary" className="text-[12px] font-semibold">OP</Badge>
                    )}
                    {topHelpful?.id === post.id && (
                      <Badge variant="success" className="gap-1 text-[12px] font-semibold"><Check size={10} /> Most helpful</Badge>
                    )}
                    {postedAt && (
                      <span className="text-[12px] ml-auto" style={{ color: "var(--muted-foreground)" }} title={postedAt}>
                        {ago(post.createdAt)}
                      </span>
                    )}
                  </div>
                  <p
                    className="whitespace-pre-wrap leading-relaxed text-[14px]"
                    style={{ color: "var(--foreground)", opacity: 0.9 }}
                  >
                    {post.content}
                  </p>
                  {!isOriginal && (
                    <div className="flex items-center gap-2 pt-1">
                      {post.authorId !== user?.id ? (
                        <button
                          type="button"
                          onClick={() => helpfulMut.mutate(post.id)}
                          disabled={helpfulMut.isPending && helpfulMut.variables === post.id}
                          aria-pressed={!!post.markedHelpfulByMe}
                          className={cn(
                            "inline-flex items-center gap-1.5 border px-2.5 py-1 text-[12.5px] font-medium transition-colors",
                            post.markedHelpfulByMe ? "border-primary bg-primary/10 text-primary" : "border-border text-muted-foreground hover:border-primary hover:text-foreground",
                          )}
                        >
                          <Check size={12} /> {post.markedHelpfulByMe ? "Marked helpful" : "Helpful"}{(post.helpfulCount ?? 0) > 0 && <span className="tabular-nums"> · {post.helpfulCount}</span>}
                        </button>
                      ) : (post.helpfulCount ?? 0) > 0 ? (
                        <span className="inline-flex items-center gap-1.5 text-[12.5px] font-medium text-primary"><Check size={12} /> {post.helpfulCount} found this helpful</span>
                      ) : null}
                    </div>
                  )}
                </div>
              </div>
              {isOriginal && (
                posts.length === 1 ? (
                  !thread.isClosed && (
                    <div className="flex flex-col items-start gap-2.5 border border-dashed border-border bg-card/50 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
                      <p className="text-[14px]">
                        <span className="font-semibold">No replies yet.</span>{" "}
                        <span className="text-muted-foreground">A line of advice, an answer or your own story is all it takes. {authorName.split(" ")[0]} will be glad to hear from you.</span>
                      </p>
                      <Button size="sm" onClick={() => document.getElementById("forum-reply")?.focus()}>Be the first to reply</Button>
                    </div>
                  )
                ) : (
                  <p className="px-1 pt-2 text-[13px] font-semibold text-muted-foreground">{posts.length - 1}{page > 1 ? "+" : ""} {posts.length - 1 === 1 ? "reply" : "replies"}</p>
                )
              )}
              </Fragment>
            );
          })}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      {/* ── Reply box / closed state ── */}
      {thread.isClosed ? (
        <div
          className="flex items-center justify-center gap-2 py-5 card text-[13.5px] font-medium"
          style={{ color: "var(--muted-foreground)" }}
        >
          <Lock size={14} /> This thread is closed, no new replies
        </div>
      ) : (
        <div className="card p-5 space-y-3">
          <p className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>
            Post a reply
          </p>
          <Textarea
            id="forum-reply"
            value={reply}
            onChange={e => setReply(e.target.value)}
            placeholder={`Reply to ${(thread.authorName ?? "this thread").split(" ")[0]}…`}
            rows={4}
            className="resize-none text-[14px]"
          />
          <Button
            size="sm"
            className="gap-2 font-semibold"
            style={{ height: 38 }}
            disabled={!reply.trim() || replyMut.isPending}
            onClick={() => replyMut.mutate()}
          >
            <Send size={13} />
            {replyMut.isPending ? "Posting…" : "Post reply"}
          </Button>
        </div>
      )}
    </div>
  );
}
