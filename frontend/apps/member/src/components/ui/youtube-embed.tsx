"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";
import { Play } from "lucide-react";

function extractVideoId(url: string): string | null {
  const patterns = [
    /(?:youtube\.com\/watch\?v=|youtu\.be\/|youtube\.com\/embed\/|youtube\.com\/v\/)([a-zA-Z0-9_-]{11})/,
    /^([a-zA-Z0-9_-]{11})$/,
  ];
  for (const p of patterns) {
    const m = url.match(p);
    if (m) return m[1];
  }
  return null;
}

interface YouTubeEmbedProps {
  url: string;
  className?: string;
}

/** Click-to-play YouTube embed — shows thumbnail first, loads iframe on click for better performance */
export function YouTubeEmbed({ url, className }: YouTubeEmbedProps) {
  const [playing, setPlaying] = useState(false);
  const videoId = extractVideoId(url.trim());
  if (!videoId) return null;

  const thumbnail = `https://img.youtube.com/vi/${videoId}/hqdefault.jpg`;

  return (
    <div className={cn("relative aspect-video rounded-xl overflow-hidden bg-black shadow-lg group", className)}>
      {playing ? (
        <iframe
          src={`https://www.youtube-nocookie.com/embed/${videoId}?rel=0&autoplay=1`}
          className="absolute inset-0 w-full h-full"
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
          allowFullScreen
          title="YouTube video"
        />
      ) : (
        <button
          type="button"
          className="absolute inset-0 w-full h-full cursor-pointer"
          onClick={() => setPlaying(true)}
          aria-label="Play video"
        >
          <img
            src={thumbnail}
            alt="Video thumbnail"
            className="w-full h-full object-cover"
            loading="lazy"
          />
          <div className="absolute inset-0 bg-black/10 group-hover:bg-black/20 transition-colors duration-150 flex items-center justify-center">
            <div className="h-14 w-14 rounded-full bg-red-600 flex items-center justify-center shadow-xl group-hover:scale-105 transition-transform duration-150">
              <Play size={22} className="text-white ml-0.5" fill="white" />
            </div>
          </div>
        </button>
      )}
    </div>
  );
}

interface YouTubePreviewProps {
  url: string;
  className?: string;
}

/** Lightweight thumbnail preview for forms — shows thumbnail + play icon without loading iframe */
export function YouTubePreview({ url, className }: YouTubePreviewProps) {
  const videoId = extractVideoId(url.trim());
  if (!videoId) return null;

  const thumbnail = `https://img.youtube.com/vi/${videoId}/mqdefault.jpg`;

  return (
    <div className={cn("relative aspect-video rounded-xl overflow-hidden bg-black/5 border", className)}>
      <img src={thumbnail} alt="Video thumbnail" className="w-full h-full object-cover" loading="lazy" />
      <div className="absolute inset-0 flex items-center justify-center">
        <div className="h-10 w-10 rounded-full bg-red-600 flex items-center justify-center shadow-lg">
          <Play size={18} className="text-white ml-0.5" fill="white" />
        </div>
      </div>
    </div>
  );
}

interface YouTubeGridProps {
  urls: string[];
  className?: string;
}

/** Grid of click-to-play YouTube embeds for detail views */
export function YouTubeGrid({ urls, className }: YouTubeGridProps) {
  const validUrls = urls.filter((u) => extractVideoId(u.trim()));
  if (validUrls.length === 0) return null;

  return (
    <div className={cn("grid gap-4", validUrls.length === 1 ? "grid-cols-1" : "grid-cols-1 md:grid-cols-2", className)}>
      {validUrls.map((url, i) => (
        <YouTubeEmbed key={i} url={url} />
      ))}
    </div>
  );
}
