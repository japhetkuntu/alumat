"use client";

import { useParams, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Calendar, Pin } from "@alumni/ui";

import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { RichTextViewer } from "@alumni/ui";
import { MediaGallery } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { getNewsPost } from "@/lib/member-api";
import { Newspaper } from "@alumni/ui";

export default function NewsDetailPage() {
  const { id }   = useParams<{ id: string }>();
  const router   = useRouter();

  const { data: post, isLoading } = useQuery({
    queryKey: ["m-news-post", id],
    queryFn:  () => getNewsPost(id),
  });

  /* ── Loading ── */
  if (isLoading) {
    return (
      <div className="p-6 lg:p-10 max-w-3xl mx-auto space-y-6 animate-pulse">
        <div className="h-6 w-24 rounded-lg" style={{ background: "var(--secondary)" }} />
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  if (!post) return (
    <div className="p-6 lg:p-10 max-w-3xl mx-auto">
      <button
        onClick={() => router.push("/news")}
        className="flex items-center gap-1.5 text-[13.5px] font-semibold mb-6 transition-colors hover:underline"
        style={{ color: "var(--muted-foreground)" }}
      >
        <ArrowLeft size={15} /> Back to news
      </button>
      <EmptyState
        icon={<Newspaper size={40} />}
        title="Article not found"
        description="This article may have been removed or the link is invalid."
      />
    </div>
  );

  const hasGallery = (post.imageUrls?.length ?? 0) > 1 || (post.youtubeVideoUrls?.length ?? 0) > 0;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-3xl mx-auto pb-20">

      {/* ── Nav ── */}
      <button
        onClick={() => router.push("/news")}
        className="flex items-center gap-1.5 text-[13.5px] font-semibold mb-7 transition-colors hover:underline"
        style={{ color: "var(--muted-foreground)" }}
      >
        <ArrowLeft size={15} /> Back to news
      </button>

      {/* ── Header ── */}
      <div className="space-y-4 mb-7">

        {/* Category + pinned */}
        <div className="flex flex-wrap items-center gap-2">
          {post.isPinned && (
            <Badge variant="warning" className="gap-1.5">
              <Pin size={10} /> Pinned
            </Badge>
          )}
          <Badge>{post.category}</Badge>
        </div>

        {/* Title */}
        <h1
          className="font-[family-name:var(--font-display)] leading-tight"
          style={{
            fontSize: "clamp(1.5rem, 4vw, 2.25rem)",
            fontWeight: 700,
            letterSpacing: "-0.02em",
            color: "var(--foreground)",
          }}
        >
          {post.title}
        </h1>

        {/* Date + author */}
        <div className="flex flex-wrap items-center gap-4">
          {post.publishedAt && (
            <span className="flex items-center gap-1.5 text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
              <Calendar size={14} style={{ color: "var(--primary)" }} />
              {formatDate(post.publishedAt)}
            </span>
          )}
          {post.authorName && (
            <span className="text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
              by <span className="font-semibold" style={{ color: "var(--foreground)" }}>{post.authorName}</span>
            </span>
          )}
        </div>
      </div>

      {/* ── Hero image ── */}
      {post.imageUrls?.[0] && (
        <div
          className="rounded-2xl overflow-hidden border mb-8"
          style={{ borderColor: "var(--border)" }}
        >
          <img
            src={post.imageUrls[0]}
            alt={post.title}
            className="w-full object-cover"
            style={{ maxHeight: 460 }}
          />
        </div>
      )}

      {/* ── Content ── */}
      <div className="prose prose-base dark:prose-invert max-w-none mb-8">
        <RichTextViewer content={post.content} />
      </div>

      {/* ── Media gallery (remaining images + videos) ── */}
      {hasGallery && (
        <div>
          <h2
            className="font-[family-name:var(--font-display)] mb-4 pb-3 border-b"
            style={{ fontSize: "1.1rem", fontWeight: 700, color: "var(--foreground)", borderColor: "var(--border)" }}
          >
            Photos &amp; videos
          </h2>
          <MediaGallery
            imageUrls={post.imageUrls?.slice(1)}
            youtubeUrls={post.youtubeVideoUrls}
          />
        </div>
      )}

    </div>
  );
}
