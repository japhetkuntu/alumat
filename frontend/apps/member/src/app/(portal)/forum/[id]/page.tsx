"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Send, Pin, Lock, MessageSquare, Clock } from "lucide-react";
import { Pagination } from "@alumni/ui";
import { toast } from "sonner";

import { Button } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { getThreadPosts, replyToThread, getForumThread } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { cn } from "@alumni/ui";

/* Safe date formatter — never throws on undefined / invalid input */
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
      toast.success("Reply posted.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const posts      = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  /* ── Loading ── */
  if (threadLoading || postsLoading) {
    return (
      <div className="p-6 lg:p-10 max-w-[1400px] mx-auto space-y-6 animate-pulse">
        <div className="h-6 w-24 rounded-lg" style={{ background: "var(--secondary)" }} />
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  if (!thread) return (
    <div className="p-6 lg:p-10 max-w-[1400px] mx-auto">
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
        </div>
      </div>

      {/* ── Posts ── */}
      {posts.length === 0 ? (
        <EmptyState
          icon={<MessageSquare size={40} />}
          title="No replies yet"
          description="Be the first to reply to this thread."
        />
      ) : (
        <div className="space-y-3">
          {posts.map((post, i) => {
            const authorName = post.authorName ?? "Alumni member";
            const isOriginal = i === 0 && page === 1;
            const postedAt   = safeDate(post.createdAt);

            return (
              <div
                key={post.id}
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
                      <Badge variant="secondary" className="text-[10px] font-semibold">OP</Badge>
                    )}
                    {postedAt && (
                      <span className="text-[12px] ml-auto" style={{ color: "var(--muted-foreground)" }}>
                        {postedAt}
                      </span>
                    )}
                  </div>
                  <p
                    className="whitespace-pre-wrap leading-relaxed text-[14px]"
                    style={{ color: "var(--foreground)", opacity: 0.9 }}
                  >
                    {post.content}
                  </p>
                </div>
              </div>
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
          <Lock size={14} /> This thread is closed — no new replies
        </div>
      ) : (
        <div className="card p-5 space-y-3">
          <p className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>
            Post a reply
          </p>
          <Textarea
            value={reply}
            onChange={e => setReply(e.target.value)}
            placeholder="Share your thoughts…"
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
