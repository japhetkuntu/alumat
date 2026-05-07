"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { Download, Link2, FileText, FolderOpen, ExternalLink, ArrowRight } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { SearchModal } from "@/components/ui/search-modal";
import { Input } from "@/components/ui/input";
import { FormSelect } from "@/components/ui/select";
import { formatDate } from "@/lib/utils";
import { getResources, trackResourceDownload } from "@/lib/member-api";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";

const categories = ["All", "Career", "Professional", "Scholarship", "Technical", "General", "Other"];

const categoryColor: Record<string, string> = {
  Career: "bg-blue-500/10 text-blue-600 dark:text-blue-400",
  Professional: "bg-violet-500/10 text-violet-600 dark:text-violet-400",
  Scholarship: "bg-amber-500/10 text-amber-600 dark:text-amber-400",
  Technical: "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400",
  General: "bg-slate-500/10 text-slate-600 dark:text-slate-400",
  Other: "bg-muted text-muted-foreground",
};

export default function MemberResourcesPage() {
  const [categoryFilter, setCategoryFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [addedAfter, setAddedAfter] = useState("");
  const [addedBefore, setAddedBefore] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 18;

  const { data, isLoading } = useQuery({
    queryKey: ["m-resources", categoryFilter, typeFilter, addedAfter, addedBefore, search, page],
    queryFn: () => getResources(
      page,
      pageSize,
      categoryFilter || undefined,
      search || undefined,
      typeFilter || undefined,
      addedAfter || undefined,
      addedBefore || undefined,
    ),
    placeholderData: (prev) => prev,
  });

  const resources = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  const queryClient = useQueryClient();
  const downloadMutation = useMutation({
    mutationFn: trackResourceDownload,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["m-resources"] }),
    onError: () => {
      // Optional: add toast/error handling here
    },
  });

  const handleResourceDownload = async (resourceId: string, href: string) => {
    try {
      await downloadMutation.mutateAsync(resourceId);
    } catch {
      // ignore tracking errors
    }
    window.open(href, "_blank", "noopener,noreferrer");
  };

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-6 sm:space-y-8 lg:space-y-10 selection:bg-primary/20">
      <header className="space-y-2 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <h1 className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tight">Resources</h1>
        <p className="text-muted-foreground text-sm sm:text-base lg:text-lg font-medium leading-relaxed max-w-2xl">
          Guides, articles, and tools curated by the UMaT Alumni team — fuel your career and professional growth.
        </p>
      </header>

      {/* Filter bar */}
      <div className="space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
        <div className="relative max-w-sm">
          <SearchModal
            title="Search resources"
            value={search}
            onChange={(value) => { setSearch(value); setPage(1); }}
            placeholder="Search resources..."
          >
            {isLoading ? (
              <p className="text-sm text-muted-foreground">Searching...</p>
            ) : resources.length === 0 ? (
              <p className="text-sm text-muted-foreground">No results match your search.</p>
            ) : (
              <div className="space-y-2 max-h-72 overflow-y-auto">
                {resources.slice(0, 5).map((r) => (
                  <div key={r.id} className="flex items-center justify-between gap-3 rounded-lg border border-border/40 bg-muted/30 px-3 py-2">
                    <div className="min-w-0">
                      <p className="text-sm font-semibold truncate">{r.title}</p>
                      <p className="text-xs text-muted-foreground truncate">{r.category} · {r.type}</p>
                    </div>
                    <span className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">{formatDate(r.createdAt)}</span>
                  </div>
                ))}
                {resources.length > 5 && (
                  <p className="text-xs text-muted-foreground">Showing {Math.min(5, resources.length)} of {resources.length} results. Close to view the full list.</p>
                )}
              </div>
            )}
          </SearchModal>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-3 max-w-4xl">
          <FormSelect
            value={typeFilter || "__all__"}
            onValueChange={(v) => { setTypeFilter(v === "__all__" ? "" : v); setPage(1); }}
            options={[
              { value: "__all__", label: "All types" },
              { value: "PDF", label: "PDF" },
              { value: "Video", label: "Video" },
              { value: "Link", label: "Link" },
              { value: "Document", label: "Document" },
              { value: "Image", label: "Image" },
              { value: "File", label: "File" },
            ]}
          />
          <Input type="date" value={addedAfter} onChange={(e) => { setAddedAfter(e.target.value); setPage(1); }} />
          <Input type="date" value={addedBefore} onChange={(e) => { setAddedBefore(e.target.value); setPage(1); }} />
        </div>

        <div className="flex gap-2 flex-wrap">
          {categories.map((c) => {
            const active = categoryFilter === (c === "All" ? "" : c);
            return (
              <button
                key={c}
                onClick={() => setCategoryFilter(c === "All" ? "" : c)}
                className={`px-4 py-1.5 rounded-full text-[12px] font-bold transition-all duration-200 border ${
                  active
                    ? "bg-primary text-primary-foreground border-primary shadow-lg shadow-primary/20"
                    : "bg-muted/30 text-muted-foreground border-border/50 hover:border-primary/30 hover:text-foreground hover:bg-muted/60"
                }`}
              >
                {c}
              </button>
            );
          })}
        </div>
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : resources.length === 0 ? (
        <EmptyState icon={<FolderOpen size={48} />} title="No resources found" description="Try adjusting your search or check back later." />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {resources.map((r, i) => {
            const isFile = r.type === "File";
            const href = r.externalUrl ?? r.fileUrl;
            const colorCls = categoryColor[r.category] ?? "bg-muted text-muted-foreground";

            return (
              <Card
                key={r.id}
                className="group flex flex-col overflow-hidden border-border/40 hover:border-primary/30 transition-all duration-500 hover:shadow-2xl hover:shadow-primary/5 animate-in fade-in slide-in-from-bottom-6 duration-700"
                style={{ animationDelay: `${i * 80}ms` }}
              >
                {/* Visual top section */}
                <div className="relative h-40 overflow-hidden flex-shrink-0">
                  {r.bannerImageUrl ? (
                    <img src={r.bannerImageUrl} alt={r.title} className="w-full h-full object-cover transition-transform duration-1000 group-hover:scale-110" loading="lazy" />
                  ) : (
                    <div className={`w-full h-full flex items-center justify-center bg-gradient-to-br from-muted/50 to-muted/20`}>
                      <div className="w-16 h-16 rounded-2xl bg-background/60 backdrop-blur-sm border border-border/50 flex items-center justify-center shadow-lg">
                        {isFile
                          ? <FileText size={28} className="text-primary/50" />
                          : <Link2 size={28} className="text-primary/50" />}
                      </div>
                    </div>
                  )}
                  <div className="absolute inset-0 bg-gradient-to-t from-black/40 via-transparent to-transparent" />
                  <div className="absolute top-3 left-3 flex gap-1.5">
                    <span className={`px-2 py-0.5 rounded-full text-[9px] font-black uppercase tracking-widest ${colorCls}`}>
                      {r.category}
                    </span>
                    <span className="px-2 py-0.5 rounded-full text-[9px] font-black uppercase tracking-widest bg-white/10 backdrop-blur-sm text-white border border-white/10">
                      {r.type}
                    </span>
                  </div>
                </div>

                <CardContent className="flex-1 flex flex-col p-5 space-y-4">
                  <div className="flex-1 space-y-1">
                    <h3 className="text-sm font-bold leading-snug group-hover:text-primary transition-colors line-clamp-2">{r.title}</h3>
                    {r.description && (
                      <p className="text-[12px] text-muted-foreground line-clamp-2">{r.description}</p>
                    )}
                  </div>

                  <div className="flex items-center justify-between text-[11px] text-muted-foreground">
                    <span className="flex items-center gap-1">
                      <Download size={11} />
                      {r.downloadCount ?? 0} downloads
                    </span>
                    <span>Added {formatDate(r.createdAt)}</span>
                  </div>

                  {href ? (
                    <div className="flex items-center gap-2">
                      <Link href={`/resources/${r.id}`} className="flex-1">
                        <Button size="sm" variant="outline" className="w-full h-10 font-bold gap-1 hover:bg-muted/60 transition-all">
                          <ArrowRight size={13} />View Details
                        </Button>
                      </Link>
                      <Button
                        size="icon"
                        variant="outline"
                        className="h-10 w-10"
                        title={isFile ? "Download" : "Open link"}
                        onClick={() => handleResourceDownload(r.id, href)}
                      >
                        {isFile ? <Download size={14} /> : <ExternalLink size={14} />}
                      </Button>
                    </div>
                  ) : (
                    <Button size="sm" variant="outline" className="w-full h-10 font-bold" disabled>
                      Unavailable
                    </Button>
                  )}
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      <div className="pt-4">
        <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
      </div>
    </div>
  );
}
