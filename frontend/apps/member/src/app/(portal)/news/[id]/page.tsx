"use client";

import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Calendar, Pin, ChevronRight } from "lucide-react";
import Link from "next/link";

import { Button } from "@/components/ui/button";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { RichTextViewer } from "@/components/ui/rich-text-editor";
import { MediaGallery } from "@/components/ui/media-gallery";
import { formatDate } from "@/lib/utils";
import { getNewsPost } from "@/lib/member-api";
import { Newspaper } from "lucide-react";

const categoryColor: Record<string, string> = {
  Announcement: "bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/20",
  Achievement: "bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20",
  News: "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20",
  Event: "bg-violet-500/10 text-violet-600 dark:text-violet-400 border-violet-500/20",
  Opportunity: "bg-sky-500/10 text-sky-600 dark:text-sky-400 border-sky-500/20",
};

export default function NewsDetailPage() {
  const { id } = useParams<{ id: string }>();

  const { data: post, isLoading } = useQuery({
    queryKey: ["m-news-post", id],
    queryFn: () => getNewsPost(id),
  });

  if (isLoading) {
    return (
      <div className="p-4 sm:p-6 lg:p-12 max-w-3xl mx-auto space-y-6">
        <div className="h-10 w-32 rounded-xl bg-muted/50 animate-pulse" />
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  if (!post) {
    return (
      <div className="p-4 sm:p-6 lg:p-12 max-w-3xl mx-auto space-y-6">
        <Link href="/news">
          <Button variant="ghost" size="sm" className="h-10 rounded-xl hover:bg-black/5 dark:hover:bg-white/5 font-bold group -ml-2">
            <ArrowLeft size={18} className="mr-2 group-hover:-translate-x-1 transition-transform" />
            Back to News
          </Button>
        </Link>
        <EmptyState icon={<Newspaper size={48} />} title="Article not found" description="This article may have been removed or the link is invalid." />
      </div>
    );
  }

  const colorCls = categoryColor[post.category] ?? "bg-muted/50 text-muted-foreground border-border/40";

  return (
    <div className="p-4 sm:p-6 lg:p-12 max-w-3xl mx-auto space-y-6 sm:space-y-8 pb-24 page-enter">
      <nav className="flex items-center gap-1.5 text-sm animate-in fade-in slide-in-from-top-4 duration-500">
        <Link href="/news">
          <Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg hover:bg-black/5 dark:hover:bg-white/5 font-bold group -ml-2">
            <ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />
            News
          </Button>
        </Link>
        <ChevronRight size={14} className="text-muted-foreground/50" />
        <span className="text-[13px] font-semibold text-foreground/70 truncate max-w-[200px] sm:max-w-xs">{post.title}</span>
      </nav>

      <header className="space-y-4">
        <div className="flex items-center gap-2 flex-wrap">
          {post.isPinned && (
            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-orange-500/10 border border-orange-500/20 text-orange-600 dark:text-orange-400 text-[10px] font-black uppercase tracking-widest">
              <Pin size={10} /> Pinned
            </span>
          )}
          <span className={`px-2.5 py-1 rounded-full border text-[10px] font-black uppercase tracking-widest ${colorCls}`}>{post.category}</span>
        </div>
        <h1 className="text-xl sm:text-2xl lg:text-3xl xl:text-4xl font-black tracking-tight leading-tight">{post.title}</h1>
        <div className="flex items-center gap-4 text-sm text-muted-foreground font-medium">
          {post.publishedAt && (
            <span className="flex items-center gap-1.5">
              <Calendar size={14} className="text-primary/60" />
              {formatDate(post.publishedAt)}
            </span>
          )}
          {post.authorName && <span>by {post.authorName}</span>}
        </div>
      </header>

      {post.imageUrls && post.imageUrls.length > 0 && (
        <div className="rounded-2xl overflow-hidden border border-border/40 shadow-lg aspect-video">
          <img src={post.imageUrls[0]} alt={post.title} className="w-full h-full object-cover" />
        </div>
      )}

      <div className="prose prose-lg dark:prose-invert max-w-none">
        <RichTextViewer content={post.content} />
      </div>

      {((post.imageUrls && post.imageUrls.length > 1) || (post.youtubeVideoUrls && post.youtubeVideoUrls.length > 0)) && (
        <div className="rounded-2xl overflow-hidden border border-border/40 bg-muted/5 p-6">
          <MediaGallery imageUrls={post.imageUrls?.slice(1)} youtubeUrls={post.youtubeVideoUrls} />
        </div>
      )}
    </div>
  );
}
