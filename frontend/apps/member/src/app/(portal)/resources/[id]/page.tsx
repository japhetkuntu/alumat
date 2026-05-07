"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, FileText, Link2, Download, ExternalLink, Copy, Bookmark, BookmarkCheck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { CardSkeleton } from "@/components/ui/skeleton";
import { formatDate } from "@/lib/utils";
import { getResource, getResources, trackResourceDownload } from "@/lib/member-api";
import { YouTubeEmbed } from "@/components/ui/youtube-embed";
import { toast } from "sonner";

const categoryColor: Record<string, string> = {
  Career: "bg-blue-500/10 text-blue-600 dark:text-blue-400",
  Professional: "bg-violet-500/10 text-violet-600 dark:text-violet-400",
  Scholarship: "bg-amber-500/10 text-amber-600 dark:text-amber-400",
  Technical: "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400",
  General: "bg-slate-500/10 text-slate-600 dark:text-slate-400",
  Other: "bg-muted text-muted-foreground",
};

export default function MemberResourceDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [, setSavedVersion] = useState(0);

  const { data: resource, isLoading } = useQuery({
    queryKey: ["m-resource", id],
    queryFn: () => getResource(id),
  });

  const { data: relatedData } = useQuery({
    queryKey: ["m-resource-related", id, resource?.category],
    queryFn: () => getResources(1, 6, resource?.category),
    enabled: !!resource?.category,
  });

  const href = resource?.externalUrl ?? resource?.fileUrl;
  const isPdf = !!href && /\.pdf(\?|$)/i.test(href);
  const isImageLink = !!href && /\.(png|jpe?g|gif|webp|svg)(\?|$)/i.test(href);
  const isYouTube = !!href && /(youtube\.com|youtu\.be)/i.test(href);
  const related = (relatedData?.results ?? []).filter((r) => r.id !== id).slice(0, 3);

  const queryClient = useQueryClient();
  const downloadMutation = useMutation({
    mutationFn: trackResourceDownload,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["m-resource", id] }),
    onError: () => {},
  });

  const handleResourceDownload = async (resourceId: string, href: string) => {
    try {
      await downloadMutation.mutateAsync(resourceId);
    } catch {
      // tracking errors should not block the link
    }
    window.open(href, "_blank", "noopener,noreferrer");
  };

  const hostName = (() => {
    if (!resource?.externalUrl) return null;
    try {
      return new URL(resource.externalUrl).host;
    } catch {
      return null;
    }
  })();

  const saved = (() => {
    const savedIds = new Set<string>(JSON.parse(localStorage.getItem("memberSavedResources") ?? "[]"));
    return savedIds.has(id);
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
      <div className="p-8 text-center text-muted-foreground">
        <p className="text-lg font-semibold">Resource not found.</p>
        <Link href="/resources">
          <Button variant="ghost" className="mt-4"><ArrowLeft size={14} />Back to Resources</Button>
        </Link>
      </div>
    );
  }

  const isFile = resource.type === "File";
  const colorCls = categoryColor[resource.category] ?? "bg-muted text-muted-foreground";

  const toggleSave = () => {
    const savedIds = new Set<string>(JSON.parse(localStorage.getItem("memberSavedResources") ?? "[]"));
    if (savedIds.has(id)) {
      savedIds.delete(id);
      toast.success("Removed from saved resources");
    } else {
      savedIds.add(id);
      toast.success("Saved to your resources");
    }
    localStorage.setItem("memberSavedResources", JSON.stringify(Array.from(savedIds)));
    setSavedVersion((value) => value + 1);
  };

  const copyShareLink = async () => {
    const url = window.location.href;
    await navigator.clipboard.writeText(url);
    toast.success("Resource link copied");
  };

  return (
    <div className="p-8 lg:p-12 max-w-4xl mx-auto space-y-8">
      {/* Back */}
      <div className="animate-in fade-in slide-in-from-bottom-4 duration-700">
        <Link href="/resources">
          <Button variant="ghost" size="sm"><ArrowLeft size={14} />Back to Resources</Button>
        </Link>
      </div>

      {/* Hero Banner */}
      {resource.bannerImageUrl ? (
        <div className="rounded-2xl overflow-hidden shadow-2xl shadow-black/10 animate-in fade-in slide-in-from-bottom-6 duration-700 delay-100">
          <img
            src={resource.bannerImageUrl}
            alt={resource.title}
            className="w-full max-h-80 object-cover"
            loading="eager"
          />
        </div>
      ) : (
        <div className={`rounded-2xl h-44 flex items-center justify-center ${colorCls} animate-in fade-in duration-700`}>
          <div className="flex flex-col items-center gap-3">
            {isFile ? <FileText size={48} className="opacity-60" /> : <Link2 size={48} className="opacity-60" />}
            <span className="text-[11px] font-black uppercase tracking-widest opacity-60">{resource.type}</span>
          </div>
        </div>
      )}

      {/* Title + Meta */}
      <div className="space-y-3 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-150">
        <div className="flex items-center gap-2 flex-wrap">
          <span className={`px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-widest ${colorCls}`}>
            {resource.category}
          </span>
          <Badge variant="secondary" className="text-[10px] font-black uppercase tracking-widest">{resource.type}</Badge>
        </div>
        <h1 className="text-3xl font-black tracking-tight leading-tight">{resource.title}</h1>
        <p className="text-muted-foreground text-sm font-medium">
          Added {formatDate(resource.createdAt)}
          {resource.downloadCount ? ` · ${resource.downloadCount} downloads` : ""}
        </p>
      </div>

      {/* Content Card */}
      <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-200">
        <CardContent className="p-6 lg:p-8 space-y-6">
          <div className="flex items-center gap-2">
            <Button type="button" size="sm" variant="outline" onClick={copyShareLink}>
              <Copy size={14} />Share
            </Button>
            <Button type="button" size="sm" variant="outline" onClick={toggleSave}>
              {saved ? <BookmarkCheck size={14} /> : <Bookmark size={14} />}
              {saved ? "Saved" : "Save"}
            </Button>
          </div>

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

          {resource.description && (
            <div>
              <p className="text-[11px] font-black uppercase tracking-widest text-muted-foreground/50 mb-3">About</p>
              <p className="text-[15px] leading-relaxed text-foreground/90">{resource.description}</p>
            </div>
          )}

          {/* Stats */}
          <div className="grid grid-cols-2 gap-4 py-4 border-y border-border/40">
            <div>
              <p className="text-[9px] font-black uppercase tracking-widest text-muted-foreground/50 mb-1">Downloads</p>
              <p className="text-2xl font-black">{resource.downloadCount ?? 0}</p>
            </div>
            <div>
              <p className="text-[9px] font-black uppercase tracking-widest text-muted-foreground/50 mb-1">Published</p>
              <p className="text-sm font-bold">{formatDate(resource.createdAt)}</p>
            </div>
          </div>

          {/* Primary CTA */}
          {href ? (
            <Button
              className="w-full h-12 text-base font-bold shadow-lg shadow-primary/20 hover:shadow-xl hover:shadow-primary/25 transition-all"
              onClick={() => handleResourceDownload(resource.id, href)}
            >
              {isFile
                ? <><Download size={16} />Download Resource</>
                : <><ExternalLink size={16} />Open Resource Link</>}
            </Button>
          ) : (
            <Button className="w-full h-12 font-bold" disabled>
              Resource unavailable
            </Button>
          )}
        </CardContent>
      </Card>

      {related.length > 0 && (
        <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-250">
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
