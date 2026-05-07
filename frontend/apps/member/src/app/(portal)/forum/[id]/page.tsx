"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Send } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import Link from "next/link";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Textarea } from "@/components/ui/textarea";
import { CardSkeleton } from "@/components/ui/skeleton";
import { formatDate, getInitials } from "@/lib/utils";
import { getThreadPosts, replyToThread } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";

export default function ThreadDetailPage() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const [page, setPage] = useState(1);
  const pageSize = 30;
  const [reply, setReply] = useState("");

  const { data, isLoading } = useQuery({
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

  if (isLoading) return <div className="p-6 space-y-4"><CardSkeleton /><CardSkeleton /></div>;

  return (
    <div className="p-6 lg:p-8 max-w-3xl mx-auto space-y-6 page-enter">
      <Link href="/forum">
        <Button size="sm" variant="ghost"><ArrowLeft size={14} />Back to Forum</Button>
      </Link>

      {posts.length === 0 ? (
        <p className="text-center text-muted-foreground py-12">No posts in this thread.</p>
      ) : (
        <div className="space-y-4">
          {posts.map((post, i) => {
            const authorName = post.authorName ?? "Unknown";
            return (
              <Card key={post.id} className={i === 0 ? "border-primary/30" : ""}>
                <CardContent className="p-4">
                  <div className="flex items-start gap-3">
                    <Avatar className="h-9 w-9">
                      <AvatarFallback className="text-xs" name={authorName}>{getInitials(authorName)}</AvatarFallback>
                    </Avatar>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="text-sm font-medium">{authorName}</span>
                        <span className="text-xs text-muted-foreground">{formatDate(post.createdAt)}</span>
                      </div>
                      <p className="text-sm mt-2 whitespace-pre-wrap">{post.content}</p>
                    </div>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      <Card>
        <CardHeader><CardTitle className="text-base">Reply</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <Textarea
            value={reply}
            onChange={(e) => setReply(e.target.value)}
            placeholder="Write your reply..."
            rows={3}
          />
          <Button
            size="sm"
            disabled={!reply.trim() || replyMut.isPending}
            onClick={() => replyMut.mutate()}
          >
            <Send size={14} className="mr-1" /> Post Reply
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
