"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Pin, Pencil, Send, Images, Youtube, Archive, ChevronRight } from "lucide-react";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { CardSkeleton } from "@/components/ui/skeleton";
import { YouTubeEmbed } from "@/components/ui/youtube-embed";
import { formatDate } from "@/lib/utils";
import { getNewsPost, publishNewsPost, updateNewsPost } from "@/lib/admin-api";
import { handleApiError } from "@/lib/api-client";
import { EmptyState } from "@/components/ui/empty-state";
import { toast } from "sonner";

export default function AdminNewsDetailPage() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const [publishModal, setPublishModal] = useState(false);
  const [archiveModal, setArchiveModal] = useState(false);
  const [lightbox, setLightbox] = useState<string | null>(null);

  const { data: post, isLoading } = useQuery({
    queryKey: ["admin-news-post", id],
    queryFn: () => getNewsPost(id),
  });

  const publishMut = useMutation({
    mutationFn: () => publishNewsPost(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-news-post", id] });
      qc.invalidateQueries({ queryKey: ["admin-news"] });
      setPublishModal(false);
      toast.success("Post published!");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const archiveMut = useMutation({
    mutationFn: () => post ? updateNewsPost(post.id, {
      title: post.title, content: post.content, category: post.category,
      isPinned: post.isPinned, status: "Archived",
    }) : Promise.reject("No post"),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-news-post", id] });
      qc.invalidateQueries({ queryKey: ["admin-news"] });
      setArchiveModal(false);
      toast.success("Post archived");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (isLoading) {
    return (
      <div className="p-8 lg:p-12 space-y-6 max-w-4xl mx-auto">
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  if (!post) {
    return (
      <div className="p-8 lg:p-12 max-w-4xl mx-auto">
        <Link href="/news">
          <Button variant="ghost" size="sm" className="mb-6"><ArrowLeft size={14} />Back to News</Button>
        </Link>
        <EmptyState icon={<Archive size={48} />} title="Post not found" description="This news post may have been removed or the link is incorrect." />
      </div>
    );
  }

  return (
    <div className="p-8 lg:p-12 space-y-8 max-w-4xl mx-auto">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-1.5 text-sm animate-in fade-in slide-in-from-top-4 duration-500">
        <Link href="/news">
          <Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg font-bold group">
            <ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />
            News
          </Button>
        </Link>
        <ChevronRight size={14} className="text-muted-foreground/50" />
        <span className="text-[13px] font-semibold text-foreground/70 truncate max-w-[200px] sm:max-w-xs">{post.title}</span>
      </nav>
      {/* Header */}
      <div className="flex items-start justify-between gap-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div className="flex items-start gap-4">
          <div>
            <div className="flex items-center gap-2 flex-wrap mb-1">
              {post.isPinned && <Pin size={14} className="text-orange-500" />}
              <Badge variant="outline" className="text-[10px] font-black uppercase tracking-widest">{post.category}</Badge>
              <Badge
                variant={post.status === "Published" ? "success" : post.status === "Archived" ? "warning" : "secondary"}
                className="text-[10px] font-black uppercase tracking-widest"
              >
                {post.status}
              </Badge>
            </div>
            <h1 className="text-3xl font-black tracking-tight leading-tight">{post.title}</h1>
            <p className="text-muted-foreground text-sm mt-1 font-medium">
              {post.publishedAt ? `Published ${formatDate(post.publishedAt)}` : "Not yet published"}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <Link href={`/news/${id}/edit`}>
            <Button variant="outline" size="sm" className="font-bold"><Pencil size={13} />Edit</Button>
          </Link>
          {post.status === "Draft" && (
            <Button size="sm" className="font-bold shadow-lg shadow-primary/20" onClick={() => setPublishModal(true)}>
              <Send size={13} />Publish
            </Button>
          )}
          {post.status !== "Archived" && (
            <Button size="sm" variant="outline" className="font-bold text-amber-600 border-amber-200 hover:bg-amber-50 dark:border-amber-800 dark:text-amber-400" onClick={() => setArchiveModal(true)}>
              <Archive size={13} />Archive
            </Button>
          )}
        </div>
      </div>

      {/* Cover Image */}
      {post.imageUrls && post.imageUrls.length > 0 && (
        <div className="rounded-2xl overflow-hidden shadow-xl">
          <img
            src={post.imageUrls[0]}
            alt={post.title}
            className="w-full max-h-96 object-cover"
            loading="lazy"
          />
        </div>
      )}

      {/* Content */}
      <Card className="border-border/40">
        <CardContent className="p-6 lg:p-8">
          <div
            className="prose prose-sm dark:prose-invert max-w-none leading-relaxed"
            dangerouslySetInnerHTML={{ __html: post.content }}
          />
        </CardContent>
      </Card>

      {/* Image Gallery */}
      {post.imageUrls && post.imageUrls.length > 1 && (
        <Card className="border-border/40">
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Images size={16} className="text-primary" />
              Images ({post.imageUrls.length - 1} more)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
              {post.imageUrls.slice(1).map((url, i) => (
                <button
                  key={i}
                  onClick={() => setLightbox(url)}
                  className="group relative aspect-square rounded-xl overflow-hidden bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                >
                  <img
                    src={url}
                    alt={`Image ${i + 2}`}
                    className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-110 cursor-zoom-in"
                    loading="lazy"
                  />
                  <div className="absolute inset-0 bg-black/0 group-hover:bg-black/20 transition-colors" />
                </button>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* YouTube Videos */}
      {post.youtubeVideoUrls && post.youtubeVideoUrls.length > 0 && (
        <Card className="border-border/40">
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Youtube size={16} className="text-red-500" />
              Videos ({post.youtubeVideoUrls.length})
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className={`grid gap-4 ${post.youtubeVideoUrls.length > 1 ? "grid-cols-1 md:grid-cols-2" : ""}`}>
              {post.youtubeVideoUrls.map((url, i) => (
                <YouTubeEmbed key={i} url={url} />
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Lightbox */}
      {lightbox && (
        <div
          className="fixed inset-0 z-[9999] bg-black/85 backdrop-blur-sm flex items-center justify-center p-4"
          onClick={() => setLightbox(null)}
        >
          <img
            src={lightbox}
            alt="Expanded"
            className="max-h-[90vh] max-w-[95vw] object-contain rounded-xl shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          />
        </div>
      )}

      <ConfirmModal
        open={publishModal}
        title="Publish Post"
        message={`Publish "${post.title}"? It will be visible to all members.`}
        confirmLabel="Publish"
        variant="default"
        isLoading={publishMut.isPending}
        onConfirm={() => publishMut.mutate()}
        onCancel={() => setPublishModal(false)}
      />
      <ConfirmModal
        open={archiveModal}
        title="Archive Post"
        message={`Archive "${post.title}"? It will be hidden from members.`}
        confirmLabel="Archive"
        variant="default"
        isLoading={archiveMut.isPending}
        onConfirm={() => archiveMut.mutate()}
        onCancel={() => setArchiveModal(false)}
      />
    </div>
  );
}
