"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Pin, ChevronRight, ArrowLeft, Newspaper, Calendar, Tag } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { formatDate } from "@/lib/utils";
import { getNewsPosts, getNewsPost } from "@/lib/member-api";
import { RichTextViewer } from "@/components/ui/rich-text-editor";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { MediaGallery } from "@/components/ui/media-gallery";
import type { NewsPost } from "@/types";

const categories = ["All", "Announcement", "Achievement", "News", "Event", "Opportunity"];

const categoryColor: Record<string, string> = {
  Announcement: "bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/20",
  Achievement: "bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20",
  News: "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20",
  Event: "bg-violet-500/10 text-violet-600 dark:text-violet-400 border-violet-500/20",
  Opportunity: "bg-sky-500/10 text-sky-600 dark:text-sky-400 border-sky-500/20",
};

export default function MemberNewsPage() {
  const [category, setCategory] = useState("");
  const [selectedPost, setSelectedPost] = useState<NewsPost | null>(null);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const { data, isLoading } = useQuery({
    queryKey: ["m-news", category, page],
    queryFn: () => getNewsPosts(page, pageSize, category || undefined),
    placeholderData: (prev) => prev,
  });

  const { data: postDetail } = useQuery({
    queryKey: ["m-news-post", selectedPost?.id],
    queryFn: () => getNewsPost(selectedPost!.id),
    enabled: !!selectedPost,
  });

  const posts = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  if (selectedPost) {
    const post = postDetail ?? selectedPost;
    const colorCls = categoryColor[post.category] ?? "bg-muted/50 text-muted-foreground";
    return (
      <div className="p-4 sm:p-6 lg:p-12 max-w-3xl mx-auto space-y-6 sm:space-y-8 pb-24 page-enter">
        <Button variant="ghost" size="sm" className="h-10 rounded-xl hover:bg-black/5 dark:hover:bg-white/5 font-bold group -ml-2" onClick={() => setSelectedPost(null)}>
          <ArrowLeft size={18} className="mr-2 group-hover:-translate-x-1 transition-transform" />
          Back to News
        </Button>

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
          {post.publishedAt && (
            <p className="text-sm text-muted-foreground font-medium flex items-center gap-1.5">
              <Calendar size={14} className="text-primary/60" />
              {formatDate(post.publishedAt)}
            </p>
          )}
        </header>

        {post.imageUrls && post.imageUrls.length > 0 && (
          <div className="rounded-2xl overflow-hidden border border-border/40 shadow-lg aspect-video">
            <img src={post.imageUrls[0]} alt={post.title} className="w-full h-full object-cover" />
          </div>
        )}

        <div className="prose prose-lg dark:prose-invert max-w-none">
          <RichTextViewer content={post.content} />
        </div>

        {(post.imageUrls && post.imageUrls.length > 1) || (post.youtubeVideoUrls && post.youtubeVideoUrls.length > 0) ? (
          <div className="rounded-2xl overflow-hidden border border-border/40 bg-muted/5 p-6">
            <MediaGallery imageUrls={post.imageUrls?.slice(1)} youtubeUrls={post.youtubeVideoUrls} />
          </div>
        ) : null}
      </div>
    );
  }

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-6 sm:space-y-8 lg:space-y-10 selection:bg-primary/20">
      <header className="space-y-2 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <h1 className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tight">News &amp; Announcements</h1>
        <p className="text-muted-foreground text-sm sm:text-base lg:text-lg font-medium leading-relaxed max-w-2xl">
          Stay informed with the latest from the UMaT alumni community — achievements, events, and opportunities.
        </p>
      </header>

      {/* Category filter */}
      <div className="flex gap-2 flex-wrap animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
        {categories.map((c) => {
          const active = category === (c === "All" ? "" : c);
          return (
            <button
              key={c}
              onClick={() => setCategory(c === "All" ? "" : c)}
              className={`px-4 py-1.5 rounded-full text-[12px] font-bold transition-all duration-200 border ${
                active
                  ? "bg-primary text-primary-foreground border-primary shadow-lg shadow-primary/20"
                  : "bg-muted/30 text-muted-foreground border-border/50 hover:border-primary/30 hover:text-foreground hover:bg-muted/60"
              }`}
            >
              {c}
            </button>
          );
        })}
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : posts.length === 0 ? (
        <EmptyState icon={<Newspaper size={48} />} title="No posts yet" description="News and announcements will appear here." />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {posts.map((p, i) => {
            const colorCls = categoryColor[p.category] ?? "bg-muted/50 text-muted-foreground border-border/40";
            return (
              <Card
                key={p.id}
                className="group relative flex flex-col overflow-hidden border-border/40 hover:border-primary/30 transition-all duration-500 hover:shadow-2xl hover:shadow-primary/5 cursor-pointer animate-in fade-in slide-in-from-bottom-6 duration-700"
                style={{ animationDelay: `${i * 80}ms` }}
                onClick={() => setSelectedPost(p)}
              >
                {/* Banner / visual */}
                <div className="relative h-48 overflow-hidden">
                  {p.imageUrls && p.imageUrls.length > 0 ? (
                    <img src={p.imageUrls[0]} alt={p.title} className="w-full h-full object-cover transition-transform duration-1000 group-hover:scale-110" loading="lazy" />
                  ) : (
                    <div className="w-full h-full bg-gradient-to-br from-primary/10 via-primary/5 to-muted/20 flex items-center justify-center">
                      <Newspaper size={40} className="text-primary/15" />
                    </div>
                  )}
                  <div className="absolute inset-0 bg-gradient-to-t from-black/50 via-black/10 to-transparent" />

                  <div className="absolute top-4 left-4 flex gap-2">
                    {p.isPinned && (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-orange-500/20 backdrop-blur-sm border border-orange-500/30 text-orange-300 text-[9px] font-black uppercase tracking-widest">
                        <Pin size={9} /> Pinned
                      </span>
                    )}
                    <span className={`px-2 py-0.5 rounded-full border text-[9px] font-black uppercase tracking-widest backdrop-blur-sm ${colorCls}`}>{p.category}</span>
                  </div>
                </div>

                <CardContent className="flex-1 flex flex-col p-5 space-y-3">
                  <div className="space-y-2 flex-1">
                    <div className="flex items-center gap-1.5 text-[10px] font-black uppercase tracking-widest text-primary">
                      <Calendar size={11} />
                      {p.publishedAt ? formatDate(p.publishedAt) : "Draft"}
                    </div>
                    <h3 className="text-base font-bold leading-snug group-hover:text-primary transition-colors line-clamp-3">{p.title}</h3>
                  </div>

                  <div className="flex items-center justify-between pt-3 border-t border-border/40">
                    <span className="text-[11px] font-bold text-muted-foreground/60 uppercase tracking-wider flex items-center gap-1">
                      <Tag size={11} /> {p.category}
                    </span>
                    <div className="w-8 h-8 rounded-lg bg-muted/30 group-hover:bg-primary/10 flex items-center justify-center transition-colors">
                      <ChevronRight size={16} className="text-muted-foreground group-hover:text-primary transition-colors" />
                    </div>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      <div className="pt-4">
        <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
      </div>
    </div>
  );
}
