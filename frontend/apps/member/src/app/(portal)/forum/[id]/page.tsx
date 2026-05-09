"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Send, Pin, Lock, MessageSquare, Clock, ChevronRight } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import Link from "next/link";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Textarea } from "@/components/ui/textarea";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { UserAvatar } from "@/components/ui/user-avatar";
import { formatDate } from "@/lib/utils";
import { getThreadPosts, replyToThread, getForumThread } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";

export default function ThreadDetailPage() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const [page, setPage] = useState(1);
  const pageSize = 30;
  const [reply, setReply] = useState("");

  const { data: thread, isLoading: threadLoading } = useQuery({
    queryKey: ["m-forum-thread", id],
    queryFn: () => getForumThread(id),
  });

  const { data, isLoading: postsLoading } = useQuery({
    queryKey: ["m-thread-posts", id, page],
    queryFn: () => getThreadPosts(id, page, pageSize),
  });

  const replyMut = useMutation({
    mutationFn: () => replyToThread(id, reply),
    onSuccess: () => {
      setReply("");
      qc.invalidateQueries({ queryKey: ["m-thread-posts", id] });
      toast.success("Reply posted");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const posts = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  if (threadLoading || postsLoading) {
    return (
      <div className="p-4 sm:p-6 lg:p-12 max-w-3xl mx-auto space-y-6">
        <div className="h-10 w-32 rounded-xl bg-muted/50 animate-pulse" />
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  if (!thread && !postsLoading) {
    return (
      <div className="p-4 sm:p-6 lg:p-12 max-w-3xl mx-auto space-y-6">
        <nav className="flex items-center gap-1.5 text-sm">
          <Link href="/forum">
            <Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg hover:bg-black/5 dark:hover:bg-white/5 font-bold group -ml-2">
              <ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />
              Forum
            </Button>
          </Link>
        </nav>
        <EmptyState icon={<MessageSquare size={48} />} title="Thread not found" description="This thread may have been removed or the link is invalid." />
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 lg:p-12 max-w-3xl mx-auto space-y-6 sm:space-y-8 pb-24 page-enter">
      <nav className="flex items-center gap-1.5 text-sm animate-in fade-in slide-in-from-top-4 duration-500">
        <Link href="/forum">
          <Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg hover:bg-black/5 dark:hover:bg-white/5 font-bold group -ml-2">
            <ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />
            Forum
          </Button>
        </Link>
        {thread && (
          <>
            <ChevronRight size={14} className="text-muted-foreground/50" />
            <span className="text-[13px] font-semibold text-foreground/70 truncate max-w-[200px] sm:max-w-xs">{thread.title}</span>
          </>
        )}
      </nav>

      {thread && (
        <header className="space-y-3">
          <div className="flex items-center gap-2 flex-wrap">
            {thread.categoryName && (
              <span className="px-2.5 py-1 rounded-full bg-primary/10 text-primary text-[10px] font-black uppercase tracking-widest">{thread.categoryName}</span>
            )}
            {thread.isPinned && (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-orange-500/10 border border-orange-500/20 text-orange-600 dark:text-orange-400 text-[10px] font-black uppercase tracking-widest">
                <Pin size={10} /> Pinned
              </span>
            )}
            {thread.isClosed && (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-muted border border-border/50 text-muted-foreground text-[10px] font-black uppercase tracking-widest">
                <Lock size={10} /> Closed
              </span>
            )}
          </div>
          <h1 className="text-xl sm:text-2xl lg:text-3xl font-black tracking-tight leading-tight">{thread.title}</h1>
          <div className="flex items-center gap-4 text-sm text-muted-foreground">
            <span className="flex items-center gap-1.5"><Clock size={13} />{formatDate(thread.createdAt)}</span>
            <span className="flex items-center gap-1.5"><MessageSquare size={13} />{thread.replyCount} {thread.replyCount === 1 ? "reply" : "replies"}</span>
          </div>
        </header>
      )}

      {posts.length === 0 ? (
        <EmptyState icon={<MessageSquare size={40} />} title="No posts yet" description="Be the first to reply to this thread." />
      ) : (
        <div className="space-y-4">
          {posts.map((post, i) => {
            const authorName = post.authorName ?? "Unknown";
            const isFirst = i === 0 && page === 1;
            return (
              <div
                key={post.id}
                className={`flex gap-4 p-5 rounded-2xl border transition-colors ${
                  isFirst ? "border-primary/20 bg-primary/5" : "border-border/40 bg-card"
                }`}
              >
                <div className="shrink-0">
                  <UserAvatar src={post.authorProfilePictureUrl} name={authorName} size="sm" />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-2 flex-wrap">
                    <span className="font-bold text-sm">{authorName}</span>
                    {isFirst && <Badge variant="secondary" className="text-[9px] font-bold">OP</Badge>}
                    <span className="text-[11px] text-muted-foreground ml-auto">{formatDate(post.createdAt)}</span>
                  </div>
                  <p className="text-sm whitespace-pre-wrap leading-relaxed text-foreground/90">{post.content}</p>
                </div>
              </div>
            );
          })}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      {!thread?.isClosed ? (
        <div className="border border-border/40 rounded-2xl p-5 bg-card space-y-3">
          <p className="text-sm font-bold text-foreground">Post a reply</p>
          <Textarea
            value={reply}
            onChange={(e) => setReply(e.target.value)}
            placeholder="Share your thoughts..."
            rows={4}
            className="resize-none"
          />
          <Button
            size="sm"
            className="gap-2 font-bold"
            disabled={!reply.trim() || replyMut.isPending}
            onClick={() => replyMut.mutate()}
          >
            <Send size={14} /> {replyMut.isPending ? "Posting..." : "Post Reply"}
          </Button>
        </div>
      ) : (
        <div className="flex items-center justify-center gap-2 py-6 text-sm text-muted-foreground border border-border/40 rounded-2xl bg-muted/20">
          <Lock size={15} /> This thread is closed — no new replies
        </div>
      )}
    </div>
  );
}

