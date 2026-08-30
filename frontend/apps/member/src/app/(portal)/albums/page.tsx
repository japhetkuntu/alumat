"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Images, ImageOff } from "lucide-react";
import { Pagination } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { getAlbums } from "@/lib/member-api";

export default function MemberAlbumsPage() {
  const [page, setPage] = useState(1);
  const pageSize = 12;

  const { data, isLoading } = useQuery({
    queryKey: ["m-albums", page],
    queryFn: () => getAlbums(page, pageSize),
    placeholderData: (prev) => prev,
  });

  const albums = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-6 sm:space-y-8 lg:space-y-10 selection:bg-primary/20">
      <PageHeader
        eyebrow="Community"
        title="Photo Albums"
        description="Relive the moments — browse photo albums from events, gatherings, and reunions."
      />

      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : albums.length === 0 ? (
        <EmptyState icon={<Images size={48} />} title="No albums yet" description="Check back later for photo albums from alumni events and gatherings." />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
          {albums.map((album) => (
            <Link key={album.id} href={`/albums/${album.id}`}>
              <Card className="group flex flex-col overflow-hidden hover:-translate-y-1 hover:shadow-lg transition-all duration-300 h-full">
                <div className="relative aspect-[4/3] overflow-hidden flex-shrink-0 bg-muted/30">
                  {album.coverImageUrl ? (
                    <img
                      src={album.coverImageUrl}
                      alt={album.title}
                      className="w-full h-full object-cover transition-transform duration-700 group-hover:scale-110"
                      loading="lazy"
                    />
                  ) : (
                    <div className="w-full h-full flex items-center justify-center bg-gradient-to-br from-muted/50 to-muted/20">
                      <ImageOff size={32} className="text-primary/30" />
                    </div>
                  )}
                  <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-black/5 to-transparent" />
                  <div className="absolute bottom-3 left-3 flex items-center gap-1.5 px-2.5 py-1 bg-black/40 backdrop-blur-sm text-white text-[11px] font-bold">
                    <Images size={12} />
                    {album.photoCount} {album.photoCount === 1 ? "photo" : "photos"}
                  </div>
                </div>

                <CardContent className="flex-1 flex flex-col p-5 space-y-1.5">
                  <h3 className="font-[family-name:var(--font-display)] text-[15px] font-semibold leading-snug group-hover:text-primary transition-colors line-clamp-2">
                    {album.title}
                  </h3>
                  {album.description && (
                    <p className="text-[12px] text-muted-foreground line-clamp-2">{album.description}</p>
                  )}
                  <p className="text-[11px] text-muted-foreground/70 pt-1">Added {formatDate(album.createdAt)}</p>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}

      <div className="pt-4">
        <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
      </div>
    </div>
  );
}
