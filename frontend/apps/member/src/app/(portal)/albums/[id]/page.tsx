"use client";

import { useState, useCallback, useEffect } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, ChevronRight, Images, ImageOff } from "lucide-react";
import { Button } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { PhotoAlbumGallery } from "@alumni/ui";
import { getAlbum, getAlbumPhotos, type AlbumPhoto } from "@/lib/member-api";

const PAGE_SIZE = 30;

export default function MemberAlbumDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [pagesLoaded, setPagesLoaded] = useState(1);
  // Accumulate all pages fetched so far into one flat list. Each bump of
  // `pagesLoaded` fetches the next page below; an effect appends it once
  // it lands (deduped by id, so React StrictMode's double-invoke is safe).
  const [allPhotos, setAllPhotos] = useState<AlbumPhoto[]>([]);
  const [loadedForPage, setLoadedForPage] = useState(0);

  // Reset accumulated pagination state if the member navigates to a different album.
  useEffect(() => {
    setPagesLoaded(1);
    setAllPhotos([]);
    setLoadedForPage(0);
  }, [id]);

  const { data: album, isLoading: albumLoading } = useQuery({
    queryKey: ["m-album", id],
    queryFn: () => getAlbum(id),
  });

  const { data: lastPageResult, isFetching: isFetchingPage } = useQuery({
    queryKey: ["m-album-photos", id, pagesLoaded],
    queryFn: () => getAlbumPhotos(id, pagesLoaded, PAGE_SIZE),
    enabled: !!id,
    placeholderData: (prev) => prev,
  });

  useEffect(() => {
    if (!lastPageResult || loadedForPage === pagesLoaded) return;
    setLoadedForPage(pagesLoaded);
    setAllPhotos((prev) => {
      const seen = new Set(prev.map((p) => p.id));
      const fresh = lastPageResult.results.filter((p) => !seen.has(p.id));
      return [...prev, ...fresh];
    });
  }, [lastPageResult, pagesLoaded, loadedForPage]);

  const totalPages = lastPageResult?.totalPages ?? 1;
  const hasMore = pagesLoaded < totalPages;
  const isLoadingMore = isFetchingPage && pagesLoaded > 1;
  const isInitialLoading = isFetchingPage && pagesLoaded === 1 && allPhotos.length === 0;

  const loadMore = useCallback(() => {
    setPagesLoaded((p) => (p < totalPages ? p + 1 : p));
  }, [totalPages]);

  if (albumLoading) {
    return (
      <div className="p-8 lg:p-12 space-y-6 max-w-5xl mx-auto">
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  if (!album) {
    return (
      <div className="p-8 lg:p-12 max-w-5xl mx-auto">
        <Link href="/albums">
          <Button variant="ghost" size="sm" className="mb-6"><ArrowLeft size={14} />Back to Albums</Button>
        </Link>
        <EmptyState icon={<Images size={48} />} title="Album not found" description="This album may have been removed or the link is incorrect." />
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-8 lg:p-12 max-w-5xl mx-auto space-y-8">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-1.5 text-sm animate-in fade-in slide-in-from-top-4 duration-500">
        <Link href="/albums">
          <Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg font-semibold group -ml-2">
            <ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />
            Albums
          </Button>
        </Link>
        <ChevronRight size={14} className="text-muted-foreground/50" />
        <span className="text-[13px] font-semibold text-foreground/70 truncate max-w-[200px] sm:max-w-xs">{album.title}</span>
      </nav>

      {/* Hero */}
      <div className="relative rounded-2xl overflow-hidden animate-in fade-in duration-700">
        {album.coverImageUrl ? (
          <img src={album.coverImageUrl} alt={album.title} className="w-full max-h-72 object-cover" loading="eager" />
        ) : (
          <div className="w-full h-44 flex items-center justify-center bg-gradient-to-br from-muted/50 to-muted/20">
            <ImageOff size={40} className="text-primary/30" />
          </div>
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-black/50 via-transparent to-transparent" />
      </div>

      {/* Title + Meta */}
      <div className="space-y-2 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-150">
        <h1
          className="font-[family-name:var(--font-display)] leading-tight"
          style={{ fontSize: "clamp(1.5rem, 3vw, 2rem)", fontWeight: 700, letterSpacing: "-0.02em", color: "var(--foreground)" }}
        >
          {album.title}
        </h1>
        <p className="text-muted-foreground text-sm font-medium flex items-center gap-1.5">
          <Images size={13} />
          {album.photoCount} {album.photoCount === 1 ? "photo" : "photos"} · Added {formatDate(album.createdAt)}
        </p>
        {album.description && (
          <p className="text-[14.5px] leading-relaxed text-foreground/90 max-w-2xl pt-1">{album.description}</p>
        )}
      </div>

      {/* Photo grid + lightbox */}
      {isInitialLoading ? (
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-2 sm:gap-3">
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="aspect-square skeleton" />
          ))}
        </div>
      ) : allPhotos.length === 0 ? (
        <EmptyState icon={<Images size={40} />} title="No photos in this album yet" description="Check back later — photos may still be uploading." />
      ) : (
        <PhotoAlbumGallery
          photos={allPhotos.map((p) => ({ id: p.id, url: p.url, caption: p.caption }))}
          hasMore={hasMore}
          isLoadingMore={isLoadingMore}
          onLoadMore={loadMore}
        />
      )}
    </div>
  );
}
