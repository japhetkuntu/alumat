"use client";

import { useState, useEffect, useCallback } from "react";
import { X, ChevronLeft, ChevronRight, ZoomIn, ImageOff } from "lucide-react";
import { cn } from "@/lib/utils";
import { YouTubeGrid } from "@/components/ui/youtube-embed";

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
          className="relative aspect-[16/9] rounded-xl overflow-hidden cursor-pointer group border"
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
                className="relative aspect-[4/3] rounded-xl overflow-hidden cursor-pointer group border"
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
            className="absolute top-4 right-4 h-11 w-11 rounded-full bg-white/10 text-white flex items-center justify-center hover:bg-white/20 transition-colors duration-150 z-10"
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
                className="absolute left-4 top-1/2 -translate-y-1/2 h-11 w-11 rounded-full bg-white/10 text-white flex items-center justify-center hover:bg-white/20 transition-colors duration-150 z-10"
                onClick={(e) => { e.stopPropagation(); goPrev(); }}
                aria-label="Previous image"
              >
                <ChevronLeft size={22} />
              </button>
              <button
                className="absolute right-4 top-1/2 -translate-y-1/2 h-11 w-11 rounded-full bg-white/10 text-white flex items-center justify-center hover:bg-white/20 transition-colors duration-150 z-10"
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
            className="max-w-full max-h-[85vh] object-contain rounded-lg animate-scale-in"
            onClick={(e) => e.stopPropagation()}
          />

          {/* Dots */}
          {allImages.length > 1 && allImages.length <= 12 && (
            <div className="absolute bottom-5 left-1/2 -translate-x-1/2 flex gap-1.5 z-10">
              {allImages.map((_, i) => (
                <button
                  key={i}
                  className={cn(
                    "h-2 rounded-full transition-all duration-150",
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
