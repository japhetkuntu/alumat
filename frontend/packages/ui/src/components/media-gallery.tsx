"use client";

import { useState, useEffect, useCallback } from "react";
import { X, ChevronLeft, ChevronRight, ZoomIn, ImageOff } from "./icons";
import { cn } from "../lib/utils";
import { YouTubeGrid } from "./youtube-embed";

interface MediaGalleryProps {
  bannerUrl?: string | null;
  imageUrls?: string[] | null;
  youtubeUrls?: string[] | null;
  className?: string;
}

function GalleryImage({ src, alt = "", className, ...props }: React.ImgHTMLAttributes<HTMLImageElement>) {
  const [error, setError] = useState(false);
  if (error) {
    return (
      <div className={cn("w-full h-full flex items-center justify-center bg-muted/50", className)}>
        <ImageOff size={20} className="text-muted-foreground/40" />
      </div>
    );
  }
  return <img src={src} alt={alt} className={className} loading="lazy" onError={() => setError(true)} {...props} />;
}

export function MediaGallery({ bannerUrl, imageUrls, youtubeUrls, className }: MediaGalleryProps) {
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null);

  const allImages = [
    ...(bannerUrl ? [bannerUrl] : []),
    ...(imageUrls ?? []),
  ];
  const hasImages = allImages.length > 0;
  const hasVideos = youtubeUrls && youtubeUrls.length > 0;

  // Keyboard navigation & body scroll lock
  const closeLightbox = useCallback(() => setLightboxIndex(null), []);
  const goNext = useCallback(() => {
    setLightboxIndex((prev) => prev !== null ? (prev + 1) % allImages.length : null);
  }, [allImages.length]);
  const goPrev = useCallback(() => {
    setLightboxIndex((prev) => prev !== null ? (prev - 1 + allImages.length) % allImages.length : null);
  }, [allImages.length]);

  useEffect(() => {
    if (lightboxIndex === null) return;

    // Lock body scroll
    const original = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") closeLightbox();
      else if (e.key === "ArrowRight") goNext();
      else if (e.key === "ArrowLeft") goPrev();
    };
    window.addEventListener("keydown", handleKeyDown);

    return () => {
      document.body.style.overflow = original;
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [lightboxIndex, closeLightbox, goNext, goPrev]);

  if (!hasImages && !hasVideos) return null;

  return (
    <div className={cn("space-y-4", className)}>
      {/* Banner */}
      {bannerUrl && (
        <div
          className="relative aspect-[16/9] overflow-hidden cursor-pointer group border"
          onClick={() => setLightboxIndex(0)}
        >
          <GalleryImage src={bannerUrl} alt="Banner" className="w-full h-full object-cover" />
          <div className="absolute inset-0 bg-black/0 group-hover:bg-black/20 transition-colors duration-150 flex items-center justify-center">
            <ZoomIn size={24} className="text-white opacity-0 group-hover:opacity-100 transition-opacity duration-150 drop-shadow-lg" />
          </div>
        </div>
      )}

      {/* Image Grid */}
      {imageUrls && imageUrls.length > 0 && (
        <div className={cn(
          "grid gap-2",
          imageUrls.length === 1 ? "grid-cols-1" :
          imageUrls.length === 2 ? "grid-cols-2" :
          "grid-cols-2 md:grid-cols-3"
        )}>
          {imageUrls.map((url, i) => {
            const lightboxIdx = bannerUrl ? i + 1 : i;
            return (
              <div
                key={i}
                className="relative aspect-[4/3] overflow-hidden cursor-pointer group border"
                onClick={() => setLightboxIndex(lightboxIdx)}
              >
                <GalleryImage src={url} alt="" className="w-full h-full object-cover" />
                <div className="absolute inset-0 bg-black/0 group-hover:bg-black/20 transition-colors duration-150 flex items-center justify-center">
                  <ZoomIn size={18} className="text-white opacity-0 group-hover:opacity-100 transition-opacity duration-150 drop-shadow-lg" />
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* YouTube Videos */}
      {hasVideos && (
        <div className="space-y-2">
          <h4 className="text-sm font-medium text-muted-foreground">Videos</h4>
          <YouTubeGrid urls={youtubeUrls!} />
        </div>
      )}

      {/* Lightbox */}
      {lightboxIndex !== null && (
        <div
          className="fixed inset-0 z-[9999] bg-black/90 backdrop-blur-sm flex items-center justify-center p-4 animate-fade-in"
          onClick={closeLightbox}
          role="dialog"
          aria-modal="true"
          aria-label={`Image ${lightboxIndex + 1} of ${allImages.length}`}
        >
          {/* Close button */}
          <button
            className="absolute top-4 right-4 h-11 w-11 bg-white/10 text-white flex items-center justify-center hover:bg-white/20 transition-colors duration-150 z-10"
            onClick={closeLightbox}
            aria-label="Close lightbox"
          >
            <X size={20} />
          </button>

          {/* Image counter */}
          {allImages.length > 1 && (
            <div className="absolute top-5 left-1/2 -translate-x-1/2 text-white/70 text-sm font-medium tabular-nums z-10">
              {lightboxIndex + 1} / {allImages.length}
            </div>
          )}

          {/* Nav buttons */}
          {allImages.length > 1 && (
            <>
              <button
                className="absolute left-4 top-1/2 -translate-y-1/2 h-11 w-11 bg-white/10 text-white flex items-center justify-center hover:bg-white/20 transition-colors duration-150 z-10"
                onClick={(e) => { e.stopPropagation(); goPrev(); }}
                aria-label="Previous image"
              >
                <ChevronLeft size={22} />
              </button>
              <button
                className="absolute right-4 top-1/2 -translate-y-1/2 h-11 w-11 bg-white/10 text-white flex items-center justify-center hover:bg-white/20 transition-colors duration-150 z-10"
                onClick={(e) => { e.stopPropagation(); goNext(); }}
                aria-label="Next image"
              >
                <ChevronRight size={22} />
              </button>
            </>
          )}

          {/* Main image */}
          <img
            src={allImages[lightboxIndex]}
            alt={`Image ${lightboxIndex + 1}`}
            className="max-w-full max-h-[85vh] object-contain animate-scale-in"
            onClick={(e) => e.stopPropagation()}
          />

          {/* Dots */}
          {allImages.length > 1 && allImages.length <= 12 && (
            <div className="absolute bottom-5 left-1/2 -translate-x-1/2 flex gap-1.5 z-10">
              {allImages.map((_, i) => (
                <button
                  key={i}
                  className={cn(
                    "h-2 transition-all duration-150",
                    i === lightboxIndex ? "bg-white w-4" : "bg-white/40 w-2 hover:bg-white/60"
                  )}
                  onClick={(e) => { e.stopPropagation(); setLightboxIndex(i); }}
                  aria-label={`Go to image ${i + 1}`}
                />
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PHOTO ALBUM GALLERY — a sibling of MediaGallery purpose-built for a large,
   server-paginated photo album: a pure photo grid (no banner/YouTube), a
   "Load more" trigger for the next page, and a lightbox that transparently
   fetches the next page when the viewer navigates past the last loaded
   photo instead of wrapping around or dead-ending.
   ───────────────────────────────────────────────────────────────────────── */

interface AlbumLightboxPhoto {
  id: string;
  url: string;
  caption?: string | null;
}

interface PhotoAlbumGalleryProps {
  photos: AlbumLightboxPhoto[];
  /** Whether more pages remain beyond what's currently loaded in `photos`. */
  hasMore?: boolean;
  isLoadingMore?: boolean;
  onLoadMore?: () => void;
  className?: string;
}

export function PhotoAlbumGallery({ photos, hasMore = false, isLoadingMore = false, onLoadMore, className }: PhotoAlbumGalleryProps) {
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null);
  // Set when the viewer hits the last loaded photo and more exist — the next
  // page is being fetched and we should auto-advance the instant it lands,
  // rather than making them click again once loading finishes.
  const [awaitingNext, setAwaitingNext] = useState(false);

  const closeLightbox = useCallback(() => {
    setLightboxIndex(null);
    setAwaitingNext(false);
  }, []);

  const goNext = useCallback(() => {
    setLightboxIndex((prev) => {
      if (prev === null) return prev;
      if (prev + 1 < photos.length) return prev + 1;
      if (hasMore) {
        setAwaitingNext(true);
        onLoadMore?.();
      }
      return prev;
    });
  }, [photos.length, hasMore, onLoadMore]);

  const goPrev = useCallback(() => {
    setLightboxIndex((prev) => (prev !== null && prev > 0 ? prev - 1 : prev));
  }, []);

  // Once the awaited page lands, step forward into it automatically.
  useEffect(() => {
    if (awaitingNext && lightboxIndex !== null && lightboxIndex + 1 < photos.length) {
      setLightboxIndex(lightboxIndex + 1);
      setAwaitingNext(false);
    }
  }, [photos.length, awaitingNext, lightboxIndex]);

  useEffect(() => {
    if (lightboxIndex === null) return;

    const original = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") closeLightbox();
      else if (e.key === "ArrowRight") goNext();
      else if (e.key === "ArrowLeft") goPrev();
    };
    window.addEventListener("keydown", handleKeyDown);

    return () => {
      document.body.style.overflow = original;
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [lightboxIndex, closeLightbox, goNext, goPrev]);

  if (photos.length === 0) return null;

  const current = lightboxIndex !== null ? photos[lightboxIndex] : null;
  const isLastLoaded = lightboxIndex !== null && lightboxIndex === photos.length - 1;
  const nextDisabled = isLastLoaded && !hasMore;

  return (
    <div className={cn("space-y-5", className)}>
      {/* Photo grid */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-2 sm:gap-3">
        {photos.map((photo, i) => (
          <button
            key={photo.id}
            type="button"
            className="relative aspect-square overflow-hidden group border border-border/40 bg-muted/20"
            onClick={() => setLightboxIndex(i)}
          >
            <GalleryImage src={photo.url} alt={photo.caption ?? ""} className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-110" />
            <div className="absolute inset-0 bg-black/0 group-hover:bg-black/25 transition-colors duration-200 flex items-center justify-center">
              <ZoomIn size={18} className="text-white opacity-0 group-hover:opacity-100 transition-opacity duration-200 drop-shadow-lg" />
            </div>
            {photo.caption && (
              <div className="absolute bottom-0 left-0 right-0 px-2 py-1.5 bg-gradient-to-t from-black/70 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-200">
                <p className="text-[10px] text-white truncate">{photo.caption}</p>
              </div>
            )}
          </button>
        ))}

        {/* Skeleton placeholders while the next page streams in — keeps the grid from jumping abruptly */}
        {isLoadingMore && Array.from({ length: Math.min(4, photos.length) }).map((_, i) => (
          <div key={`skeleton-${i}`} className="aspect-square skeleton" />
        ))}
      </div>

      {hasMore && (
        <div className="flex justify-center pt-1">
          <button
            type="button"
            onClick={() => onLoadMore?.()}
            disabled={isLoadingMore}
            className="px-5 py-2.5 text-[12.5px] font-semibold border border-border hover:border-primary/40 hover:bg-muted/40 transition-all duration-200 disabled:opacity-60 disabled:cursor-not-allowed flex items-center gap-2"
          >
            {isLoadingMore ? (
              <>
                <span className="h-3.5 w-3.5 rounded-full border-2 border-current border-t-transparent animate-spin" />
                Loading more photos…
              </>
            ) : (
              "Load more photos"
            )}
          </button>
        </div>
      )}

      {/* Lightbox */}
      {current && lightboxIndex !== null && (
        <div
          className="fixed inset-0 z-[9999] bg-black/90 backdrop-blur-sm flex items-center justify-center p-4 animate-fade-in"
          onClick={closeLightbox}
          role="dialog"
          aria-modal="true"
          aria-label={`Photo ${lightboxIndex + 1} of ${photos.length}${hasMore ? "+" : ""}`}
        >
          <button
            className="absolute top-4 right-4 h-11 w-11 bg-white/10 text-white flex items-center justify-center hover:bg-white/20 transition-colors duration-150 z-10"
            onClick={closeLightbox}
            aria-label="Close lightbox"
          >
            <X size={20} />
          </button>

          <div className="absolute top-5 left-1/2 -translate-x-1/2 text-white/70 text-sm font-medium tabular-nums z-10">
            {lightboxIndex + 1} / {photos.length}{hasMore ? "+" : ""}
          </div>

          <button
            className="absolute left-4 top-1/2 -translate-y-1/2 h-11 w-11 bg-white/10 text-white flex items-center justify-center hover:bg-white/20 transition-colors duration-150 z-10 disabled:opacity-30 disabled:cursor-not-allowed"
            onClick={(e) => { e.stopPropagation(); goPrev(); }}
            disabled={lightboxIndex === 0}
            aria-label="Previous photo"
          >
            <ChevronLeft size={22} />
          </button>
          <button
            className="absolute right-4 top-1/2 -translate-y-1/2 h-11 w-11 bg-white/10 text-white flex items-center justify-center hover:bg-white/20 transition-colors duration-150 z-10 disabled:opacity-30 disabled:cursor-not-allowed"
            onClick={(e) => { e.stopPropagation(); goNext(); }}
            disabled={nextDisabled}
            aria-label="Next photo"
          >
            {awaitingNext ? <span className="h-4 w-4 rounded-full border-2 border-current border-t-transparent animate-spin" /> : <ChevronRight size={22} />}
          </button>

          <div className="flex flex-col items-center gap-3 max-w-full max-h-full" onClick={(e) => e.stopPropagation()}>
            {/* Keying on the index forces a remount, replaying the fade/zoom-in on every navigation for a smooth crossfade rather than a hard cut. */}
            <img
              key={lightboxIndex}
              src={current.url}
              alt={current.caption ?? `Photo ${lightboxIndex + 1}`}
              className="max-w-full max-h-[78vh] object-contain animate-in fade-in zoom-in-95 duration-300"
            />
            {current.caption && (
              <p key={`caption-${lightboxIndex}`} className="text-white/80 text-sm text-center max-w-2xl px-4 animate-in fade-in duration-300">
                {current.caption}
              </p>
            )}
            {photos.length > 1 && photos.length <= 12 && (
              <div className="flex gap-1.5">
                {photos.map((_, i) => (
                  <button
                    key={i}
                    className={cn(
                      "h-2 transition-all duration-150",
                      i === lightboxIndex ? "bg-white w-4" : "bg-white/40 w-2 hover:bg-white/60"
                    )}
                    onClick={(e) => { e.stopPropagation(); setLightboxIndex(i); }}
                    aria-label={`Go to photo ${i + 1}`}
                  />
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
