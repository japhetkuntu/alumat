"use client";

import { useState } from "react";
import { ImageUpload, Input, MultiImageUpload, YouTubePreview } from "@alumni/ui";
import { Field } from "@/components/member/manage-ui";
import type { GroupMediaInput } from "@/lib/member-api";

const splitList = (text: string) => text.split(/[,\s]+/).map((t) => t.trim()).filter(Boolean);

/**
 * The pictures and videos of one item being made or edited, kept together so every form handles them the same way. A new picture is a
 * file; a picture the item already has is an address, and taking it away just removes it from the list the form sends back.
 */
export function useMedia(initial: { banner?: string | null; images?: string[] | null; videos?: string[] | null } = {}) {
  const [banner, setBanner] = useState<File | null>(null);
  const [bannerUrl, setBannerUrl] = useState(initial.banner ?? "");
  const [images, setImages] = useState<File[]>([]);
  const [imageUrls, setImageUrls] = useState<string[]>(initial.images ?? []);
  const [videos, setVideos] = useState((initial.videos ?? []).join(", "));

  const input = (): GroupMediaInput => ({
    bannerImage: banner,
    removeBanner: !banner && !bannerUrl && !!initial.banner,
    images,
    existingImageUrls: imageUrls,
    youtubeVideoUrls: splitList(videos),
  });
  return { banner, setBanner, bannerUrl, setBannerUrl, images, setImages, imageUrls, setImageUrls, videos, setVideos, input };
}
export type Media = ReturnType<typeof useMedia>;

export function BannerField({ media, id, label = "Banner image", hint }: { media: Media; id: string; label?: string; hint?: string }) {
  return (
    <Field id={id} label={label} optional hint={hint}>
      <ImageUpload file={media.banner} existingUrl={media.bannerUrl} onChange={media.setBanner} onClearExisting={() => media.setBannerUrl("")} label="Add a picture" />
    </Field>
  );
}

export function GalleryField({ media, id, label = "Photos" }: { media: Media; id: string; label?: string }) {
  return (
    <Field id={id} label={label} optional hint="Up to 8 photos, 5 MB each.">
      <MultiImageUpload files={media.images} existingUrls={media.imageUrls}
        onAddFile={(f) => media.setImages((prev) => [...prev, f])}
        onRemoveFile={(i) => media.setImages((prev) => prev.filter((_, idx) => idx !== i))}
        onRemoveExisting={(i) => media.setImageUrls((prev) => prev.filter((_, idx) => idx !== i))}
        label="Add a photo" />
    </Field>
  );
}

export function VideoField({ media, id, label = "YouTube links", single }: { media: Media; id: string; label?: string; single?: boolean }) {
  const urls = splitList(media.videos).slice(0, single ? 1 : 5);
  return (
    <Field id={id} label={label} optional hint={single ? "A link to a YouTube video." : "Separate several links with commas."}>
      <Input id={id} type="text" inputMode="url" placeholder="https://youtube.com/…" value={media.videos} onChange={(e) => media.setVideos(e.target.value)} />
      {urls.length > 0 && <div className="grid grid-cols-2 gap-2">{urls.map((u, i) => <YouTubePreview key={`${u}-${i}`} url={u} />)}</div>}
    </Field>
  );
}

/** A small picture beside a list row, when the item has one. Fixed in size so the list does not jump as pictures arrive. */
export function Thumb({ src, alt }: { src?: string | null; alt: string }) {
  if (!src) return null;
  // eslint-disable-next-line @next/next/no-img-element
  return <img src={src} alt={alt} loading="lazy" decoding="async" className="size-14 shrink-0 rounded-[12px] border border-border bg-muted object-cover" />;
}
