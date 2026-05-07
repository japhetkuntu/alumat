"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { MapPin, Clock, Briefcase, ArrowRight } from "lucide-react";
import Link from "next/link";
import { Pagination } from "@/components/ui/pagination";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { SearchModal } from "@/components/ui/search-modal";
import { Input } from "@/components/ui/input";
import { formatDate } from "@/lib/utils";
import { getJobs } from "@/lib/member-api";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";

const typeColors: Record<string, string> = {
  "Full-time": "bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-200 dark:border-blue-800",
  FullTime: "bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-200 dark:border-blue-800",
  "Part-time": "bg-violet-500/10 text-violet-600 dark:text-violet-400 border-violet-200 dark:border-violet-800",
  PartTime: "bg-violet-500/10 text-violet-600 dark:text-violet-400 border-violet-200 dark:border-violet-800",
  Contract: "bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-200 dark:border-amber-800",
  Internship: "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800",
  Remote: "bg-cyan-500/10 text-cyan-600 dark:text-cyan-400 border-cyan-200 dark:border-cyan-800",
};

export default function MemberJobsPage() {
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [locationFilter, setLocationFilter] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 18;

  const { data, isLoading } = useQuery({
    queryKey: ["m-jobs", typeFilter, search, locationFilter, page],
    queryFn: () => getJobs(page, pageSize, typeFilter || undefined, search || undefined, locationFilter || undefined),
    placeholderData: (prev) => prev,
  });

  const jobs = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-8 selection:bg-primary/20">
      {/* Header */}
      <header className="space-y-2 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <h1 className="text-3xl font-black tracking-tight">Job Board</h1>
        <p className="text-muted-foreground text-sm font-medium max-w-xl">
          Opportunities shared by the UMaT alumni network — your next career move starts here.
        </p>
      </header>

      {/* Filters */}
      <div className="space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3 max-w-4xl">
          <div className="flex-1 min-w-0">
            <SearchModal
              title="Search jobs"
              value={search}
              onChange={(value) => { setSearch(value); setPage(1); }}
              placeholder="Search by title, company, or location..."
            >
              {isLoading ? (
                <p className="text-sm text-muted-foreground">Searching...</p>
              ) : jobs.length === 0 ? (
                <p className="text-sm text-muted-foreground">No results match your search.</p>
              ) : (
                <div className="space-y-2 max-h-72 overflow-y-auto">
                  {jobs.slice(0, 5).map((j) => (
                    <div key={j.id} className="flex items-center justify-between gap-3 rounded-lg border border-border/40 bg-muted/30 px-3 py-2">
                      <div className="min-w-0">
                        <p className="text-sm font-semibold truncate">{j.title}</p>
                        <p className="text-xs text-muted-foreground truncate">{j.company} · {j.location}</p>
                      </div>
                      <span className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">{j.status}</span>
                    </div>
                  ))}
                  {jobs.length > 5 && (
                    <p className="text-xs text-muted-foreground">Showing {Math.min(5, jobs.length)} of {jobs.length} results. Close to view the full list.</p>
                  )}
                </div>
              )}
            </SearchModal>
          </div>
          <Input placeholder="Filter by location" className="h-11" value={locationFilter} onChange={(e) => { setLocationFilter(e.target.value); setPage(1); }} />

        </div>
        <div className="flex gap-2 flex-wrap">
          {["", "Full-time", "Part-time", "Contract", "Internship"].map((t) => (
            <button
              key={t}
              onClick={() => { setTypeFilter(t); setPage(1); }}
              className={`px-4 py-1.5 rounded-full text-[11px] font-black uppercase tracking-widest transition-all border ${
                typeFilter === t
                  ? "bg-primary text-primary-foreground border-primary shadow-lg shadow-primary/20"
                  : "bg-muted/30 text-muted-foreground border-border/50 hover:border-primary/30 hover:text-foreground hover:bg-muted/60"
              }`}
            >
              {t === "" ? "All Types" : t}
            </button>
          ))}
        </div>
      </div>

      {/* Job Cards Grid */}
      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : jobs.length === 0 ? (
        <EmptyState icon={<Briefcase size={48} />} title="No jobs found" description="Check back later for new opportunities shared by the alumni network." />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
          {jobs.map((j, i) => (
            <Card
              key={j.id}
              className="group flex flex-col overflow-hidden border-border/40 hover:border-primary/30 transition-all duration-500 hover:shadow-xl hover:shadow-primary/5 animate-in fade-in slide-in-from-bottom-6 duration-700"
              style={{ animationDelay: `${i * 60}ms` }}
            >
              <CardContent className="flex-1 flex flex-col p-5 space-y-3">
                {/* Top: Icon + type badge */}
                <div className="flex items-start justify-between gap-2">
                  <div className="h-10 w-10 rounded-xl bg-muted/60 flex items-center justify-center shrink-0 group-hover:bg-primary/10 transition-colors">
                    <Briefcase size={18} className="text-muted-foreground group-hover:text-primary transition-colors" />
                  </div>
                  <span className={`px-2.5 py-0.5 rounded-full text-[9px] font-black uppercase tracking-widest border ${typeColors[j.type] ?? "bg-muted text-muted-foreground border-border"}`}>
                    {j.type}
                  </span>
                </div>

                {/* Title & company */}
                <div className="flex-1 space-y-1 min-w-0">
                  <h3 className="font-bold text-[15px] leading-snug line-clamp-2 group-hover:text-primary transition-colors">{j.title}</h3>
                  <p className="text-sm font-medium text-muted-foreground">{j.company}</p>
                </div>

                {/* Meta row */}
                <div className="flex items-center gap-3 text-[11px] text-muted-foreground flex-wrap">
                  <span className="flex items-center gap-1"><MapPin size={11} />{j.location}</span>
                  {j.deadline && <span className="flex items-center gap-1"><Clock size={11} />{formatDate(j.deadline)}</span>}
                </div>

                {j.description && (
                  <p className="text-[12px] text-muted-foreground line-clamp-2">{j.description}</p>
                )}

                <Link href={`/jobs/${j.id}`} className="mt-auto pt-1">
                  <Button size="sm" className="w-full h-10 font-bold gap-1.5 transition-all">
                    <ArrowRight size={13} />View Details
                  </Button>
                </Link>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <div className="pt-4">
        <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
      </div>
    </div>
  );
}
