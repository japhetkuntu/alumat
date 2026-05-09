"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, FileText, Link2, Download, ExternalLink, Pencil, ChevronRight } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { CardSkeleton } from "@/components/ui/skeleton";
import { formatDate } from "@/lib/utils";
import { getResource, getResources } from "@/lib/admin-api";
import { EmptyState } from "@/components/ui/empty-state";
import { YouTubeEmbed } from "@/components/ui/youtube-embed";

const categoryColor: Record<string, string> = {
  Career: "bg-blue-500/10 text-blue-600 dark:text-blue-400",
  Professional: "bg-violet-500/10 text-violet-600 dark:text-violet-400",
  Scholarship: "bg-amber-500/10 text-amber-600 dark:text-amber-400",
  Technical: "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400",
  General: "bg-slate-500/10 text-slate-600 dark:text-slate-400",
  Other: "bg-muted text-muted-foreground",
};

export default function AdminResourceDetailPage() {
  const { id } = useParams<{ id: string }>();

  const { data: resource, isLoading } = useQuery({
    queryKey: ["admin-resource", id],
    queryFn: () => getResource(id),
  });

  const { data: relatedData } = useQuery({
    queryKey: ["admin-resource-related", id, resource?.category],
    queryFn: () => getResources(1, 6, resource?.category),
    enabled: !!resource?.category,
  });

  const href = resource?.externalUrl ?? resource?.fileUrl;
  const isFile = resource?.type === "File";
  const isPdf = !!href && /\.pdf(\?|$)/i.test(href);
  const isImageLink = !!href && /\.(png|jpe?g|gif|webp|svg)(\?|$)/i.test(href);
  const isYouTube = !!href && /(youtube\.com|youtu\.be)/i.test(href);
  const related = (relatedData?.results ?? []).filter((r) => r.id !== id).slice(0, 3);

  const hostName = (() => {
    if (!resource?.externalUrl) return null;
    try {
      return new URL(resource.externalUrl).host;
    } catch {
      return null;
    }
  })();

  if (isLoading) {
    return (
      <div className="p-8 lg:p-12 space-y-6 max-w-4xl mx-auto">
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  if (!resource) {
    return (
      <div className="p-8 lg:p-12 max-w-4xl mx-auto">
        <Link href="/resources">
          <Button variant="ghost" size="sm" className="mb-6"><ArrowLeft size={14} />Back to Resources</Button>
        </Link>
        <EmptyState icon={<FileText size={48} />} title="Resource not found" description="This resource may have been removed or the link is incorrect." />
      </div>
    );
  }

  const colorCls = categoryColor[resource.category] ?? "bg-muted text-muted-foreground";

  return (
    <div className="p-8 lg:p-12 space-y-8 max-w-4xl mx-auto">
      {/* Breadcrumb */}
      <nav className="flex items-center justify-between gap-3 animate-in fade-in slide-in-from-top-4 duration-500">
        <div className="flex items-center gap-1.5 text-sm">
          <Link href="/resources">
            <Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg font-bold group">
              <ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />
              Resources
            </Button>
          </Link>
          <ChevronRight size={14} className="text-muted-foreground/50" />
          <span className="text-[13px] font-semibold text-foreground/70 truncate max-w-[200px] sm:max-w-xs">{resource.title}</span>
        </div>
        <div className="flex items-center gap-2">
          {href && (
            <a href={href} target="_blank" rel="noopener noreferrer">
              <Button variant="outline" size="sm" className="font-bold"><ExternalLink size={13} />Open</Button>
            </a>
          )}
          <Link href={`/resources/${id}/edit`}>
            <Button variant="outline" size="sm" className="font-bold"><Pencil size={13} />Edit</Button>
          </Link>
        </div>
      </nav>
      {/* Header */}
      <div className="space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <div>
          <div className="flex items-center gap-2 flex-wrap mb-2">
            <span className={`px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-widest ${colorCls}`}>
              {resource.category}
            </span>
            <Badge variant="secondary" className="text-[10px] font-black uppercase tracking-widest">{resource.type}</Badge>
          </div>
          <h1 className="text-3xl font-black tracking-tight leading-tight">{resource.title}</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Added {formatDate(resource.createdAt)} · {resource.downloadCount ?? 0} downloads
          </p>
        </div>
      </div>

      {/* Banner Image */}
      {resource.bannerImageUrl && (
        <div className="rounded-2xl overflow-hidden shadow-xl">
          <img
            src={resource.bannerImageUrl}
            alt={resource.title}
            className="w-full max-h-80 object-cover"
            loading="lazy"
          />
        </div>
      )}

      {/* Main Content Card */}
      <Card className="border-border/40">
        <CardContent className="p-6 lg:p-8 space-y-6">
          {/* Icon + Type */}
          <div className="flex items-center gap-4">
            <div className={`h-14 w-14 rounded-2xl flex items-center justify-center ${colorCls}`}>
              {isFile
                ? <FileText size={26} />
                : <Link2 size={26} />}
            </div>
            <div>
              <p className="font-bold text-lg">{resource.title}</p>
              <p className="text-sm text-muted-foreground">{resource.category} · {resource.type}</p>
            </div>
          </div>

          {/* Description */}
          {resource.description && (
            <div>
              <p className="text-[11px] font-black uppercase tracking-widest text-muted-foreground/50 mb-2">Description</p>
              <p className="text-sm leading-relaxed text-foreground/90">{resource.description}</p>
            </div>
          )}

          {href && (
            <div className="space-y-2">
              <p className="text-[11px] font-black uppercase tracking-widest text-muted-foreground/50">Preview</p>
              <div className="rounded-xl border border-border/40 overflow-hidden bg-muted/20">
                {isYouTube ? (
                  <YouTubeEmbed url={href} />
                ) : isPdf ? (
                  <iframe src={href} className="w-full h-[360px]" title="PDF Preview" />
                ) : isImageLink ? (
                  <img src={href} alt={resource.title} className="w-full max-h-[420px] object-contain bg-background" loading="lazy" />
                ) : resource.externalUrl ? (
                  <div className="p-4 space-y-1">
                    <p className="text-sm font-bold line-clamp-1">{resource.title}</p>
                    <p className="text-xs text-muted-foreground line-clamp-2">{resource.description ?? "External resource link"}</p>
                    {hostName && <p className="text-[11px] text-primary font-semibold">{hostName}</p>}
                  </div>
                ) : (
                  <div className="p-4 text-sm text-muted-foreground">Preview unavailable for this resource type.</div>
                )}
              </div>
            </div>
          )}

          {/* Stats */}
          <div className="grid grid-cols-2 gap-4 py-4 border-y border-border/40">
            <div>
              <p className="text-[9px] font-black uppercase tracking-widest text-muted-foreground/50 mb-1">Downloads</p>
              <p className="text-xl font-black">{resource.downloadCount ?? 0}</p>
            </div>
            <div>
              <p className="text-[9px] font-black uppercase tracking-widest text-muted-foreground/50 mb-1">Date Added</p>
              <p className="text-sm font-bold">{formatDate(resource.createdAt)}</p>
            </div>
          </div>

          {/* CTA */}
          {href ? (
            <a href={href} target="_blank" rel="noopener noreferrer">
              <Button className="w-full h-11 font-bold shadow-lg shadow-primary/20" size="sm">
                {isFile
                  ? <><Download size={15} />Download Resource</>
                  : <><ExternalLink size={15} />Open External Link</>}
              </Button>
            </a>
          ) : (
            <Button className="w-full h-11 font-bold" disabled>No link available</Button>
          )}
        </CardContent>
      </Card>

      {related.length > 0 && (
        <Card className="border-border/40">
          <CardContent className="p-6 space-y-4">
            <p className="text-[11px] font-black uppercase tracking-widest text-muted-foreground/50">Related Resources</p>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              {related.map((item) => (
                <Link key={item.id} href={`/resources/${item.id}`}>
                  <div className="rounded-xl border border-border/40 p-3 hover:border-primary/30 transition-colors">
                    <p className="text-sm font-bold line-clamp-2">{item.title}</p>
                    <p className="text-[11px] text-muted-foreground mt-1">{item.category} · {item.type}</p>
                  </div>
                </Link>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
