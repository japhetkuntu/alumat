"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { Newspaper, Calendar, Pin } from "lucide-react";
import { Pagination } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { getNewsPosts } from "@/lib/member-api";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import Link from "next/link";
import { cn } from "@alumni/ui";
import { SourceBadge } from "@/components/member/source-badge";
import { SourceFilterChips } from "@/components/member/source-filter-chips";

const CATEGORIES = ["All", "Announcement", "Achievement", "News", "Event", "Opportunity"];

function CategoryPill({ category }: { category: string }) {
  return <Badge className="text-[11px]">{category}</Badge>;
}

export default function MemberNewsPage() {
  const searchParams = useSearchParams();
  const [category, setCategory] = useState("");
  const [communityId, setCommunityId] = useState<string | null>(() => searchParams.get("communityId"));
  const [page,     setPage]     = useState(1);
  const pageSize = 20;

  const { data, isLoading } = useQuery({
    queryKey:        ["m-news", category, communityId, page],
    queryFn:         () => getNewsPosts(page, pageSize, category || undefined, undefined, communityId || undefined),
    placeholderData: (prev) => prev,
  });

  const posts      = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      <PageHeader title="News & announcements" description="The people, progress, and opportunities shaping our alumni community." />

      {/* ── Category filter ── */}
      <div className="flex flex-wrap gap-2">
        {CATEGORIES.map(c => {
          const active = category === (c === "All" ? "" : c);
          return (
            <button
              key={c}
              onClick={() => { setCategory(c === "All" ? "" : c); setPage(1); }}
              className={cn(
                "px-3.5 py-1.5 text-[12.5px] font-semibold border transition-colors",
                active
                  ? "bg-primary text-primary-foreground border-transparent"
                  : "border-border text-muted-foreground hover:border-primary/40",
              )}
            >
              {c}
            </button>
          );
        })}
        <SourceFilterChips value={communityId} onChange={(v) => { setCommunityId(v); setPage(1); }} />
      </div>

      {/* ── Grid ── */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : posts.length === 0 ? (
        <EmptyState
          icon={<Newspaper size={40} />}
          title="No posts yet"
          description="News and announcements will appear here when published."
        />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {posts.map(p => (
            <Link
              key={p.id}
              href={`/news/${p.id}`}
              className="group rounded-2xl border overflow-hidden flex flex-col transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md"
              style={{ borderColor: "var(--border)", background: "var(--background)" }}
            >
              {/* Banner */}
              <div className="relative shrink-0 overflow-hidden" style={{ height: 180 }}>
                {p.imageUrls?.[0] ? (
                  <img
                    src={p.imageUrls[0]}
                    alt={p.title}
                    className="absolute inset-0 w-full h-full object-cover transition-transform duration-500 group-hover:scale-[1.03]"
                    loading="lazy"
                  />
                ) : (
                  <div
                    className="absolute inset-0 flex items-center justify-center"
                    style={{ background: "var(--secondary)" }}
                  >
                    <Newspaper size={36} style={{ color: "var(--muted-foreground)", opacity: 0.2 }} />
                  </div>
                )}
                <div className="absolute inset-0" style={{ background: "linear-gradient(to top, rgba(0,0,0,0.4) 0%, transparent 55%)" }} />

                {/* Badges over image */}
                <div className="absolute top-3 left-3 flex items-center gap-1.5">
                  {p.isPinned && (
                    <span
                      className="inline-flex items-center gap-1 px-2 py-0.5 text-[10px] font-semibold backdrop-blur-sm"
                      style={{ background: "var(--warning)", color: "var(--warning-foreground)" }}
                    >
                      <Pin size={9} /> Pinned
                    </span>
                  )}
                  <span
                    className="inline-flex items-center px-2 py-0.5 text-[10px] font-semibold"
                    style={{
                      background: "rgba(0,0,0,0.35)",
                      backdropFilter: "blur(4px)",
                      color: "rgba(255,255,255,0.9)",
                    }}
                  >
                    {p.category}
                  </span>
                </div>

                {/* Date over image */}
                <div className="absolute bottom-2.5 left-3 flex items-center gap-1.5 text-white/80 text-[11.5px] font-medium">
                  <Calendar size={11} />
                  {p.publishedAt ? formatDate(p.publishedAt) : "Draft"}
                </div>
              </div>

              {/* Body */}
              <div className="flex flex-col flex-1 p-4 gap-3">
                <SourceBadge communityId={p.communityId} communityName={p.communityName} className="self-start" />
                <h3
                  className="text-[14.5px] font-semibold leading-snug line-clamp-3 transition-colors duration-200 group-hover:text-primary"
                  style={{ color: "var(--foreground)" }}
                >
                  {p.title}
                </h3>

                <div
                  className="flex items-center justify-between mt-auto pt-3 border-t"
                  style={{ borderColor: "var(--border)" }}
                >
                  <CategoryPill category={p.category} />
                  <span
                    className="text-[12.5px] font-semibold transition-colors duration-200 group-hover:text-primary"
                    style={{ color: "var(--muted-foreground)" }}
                  >
                    Read →
                  </span>
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
    </div>
  );
}
