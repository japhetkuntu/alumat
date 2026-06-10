"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { MapPin, Clock, Briefcase, ArrowRight, X, Search } from "lucide-react";
import Link from "next/link";
import { Pagination } from "@/components/ui/pagination";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { formatDate } from "@/lib/utils";
import { getJobs } from "@/lib/member-api";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { cn } from "@/lib/utils";

/* Job type → subtle pill color */
const typeStyle: Record<string, { bg: string; text: string; border: string }> = {
  "Full-time": { bg: "rgba(59,130,246,0.08)", text: "#2563eb", border: "rgba(59,130,246,0.2)" },
  FullTime:    { bg: "rgba(59,130,246,0.08)", text: "#2563eb", border: "rgba(59,130,246,0.2)" },
  "Part-time": { bg: "rgba(139,92,246,0.08)", text: "#7c3aed", border: "rgba(139,92,246,0.2)" },
  PartTime:    { bg: "rgba(139,92,246,0.08)", text: "#7c3aed", border: "rgba(139,92,246,0.2)" },
  Contract:    { bg: "rgba(245,158,11,0.08)", text: "#d97706", border: "rgba(245,158,11,0.2)" },
  Internship:  { bg: "rgba(16,185,129,0.08)", text: "#059669", border: "rgba(16,185,129,0.2)" },
  Remote:      { bg: "rgba(6,182,212,0.08)",  text: "#0891b2", border: "rgba(6,182,212,0.2)"  },
};

const JOB_TYPES = ["", "Full-time", "Part-time", "Contract", "Internship"];

export default function MemberJobsPage() {
  const [search,         setSearch]         = useState("");
  const [typeFilter,     setTypeFilter]      = useState("");
  const [locationFilter, setLocationFilter]  = useState("");
  const [page,           setPage]            = useState(1);
  const pageSize = 18;

  const { data, isLoading } = useQuery({
    queryKey:        ["m-jobs", typeFilter, search, locationFilter, page],
    queryFn:         () => getJobs(page, pageSize, typeFilter || undefined, search || undefined, locationFilter || undefined),
    placeholderData: (prev) => prev,
  });

  const jobs       = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;
  const hasFilters = !!(search || locationFilter || typeFilter);

  function clearFilters() { setSearch(""); setLocationFilter(""); setTypeFilter(""); setPage(1); }

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      {/* ── Header ── */}
      <div>
        <h1
          className="font-[family-name:var(--font-display)] tracking-tight"
          style={{ fontSize: "clamp(1.5rem, 3vw, 2rem)", fontWeight: 700, color: "var(--foreground)" }}
        >
          Job board
        </h1>
        <p className="mt-1 text-[14px]" style={{ color: "var(--muted-foreground)" }}>
          Opportunities shared by the UMaT alumni network.
        </p>
      </div>

      {/* ── Filters ── */}
      <div className="space-y-3">
        {/* Search + location */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 max-w-2xl">
          <div className="relative">
            <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
              style={{ color: "var(--muted-foreground)" }} />
            <Input
              placeholder="Search by title, company…"
              value={search}
              onChange={e => { setSearch(e.target.value); setPage(1); }}
              className="h-11 pl-9 text-[14px]"
            />
          </div>
          <div className="relative">
            <MapPin size={15} className="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
              style={{ color: "var(--muted-foreground)" }} />
            <Input
              placeholder="Filter by location"
              value={locationFilter}
              onChange={e => { setLocationFilter(e.target.value); setPage(1); }}
              className="h-11 pl-9 text-[14px]"
            />
          </div>
        </div>

        {/* Type pills + clear */}
        <div className="flex flex-wrap items-center gap-2">
          {JOB_TYPES.map(t => (
            <button
              key={t}
              onClick={() => { setTypeFilter(t); setPage(1); }}
              className={cn(
                "px-3.5 py-1.5 rounded-full text-[12.5px] font-semibold border transition-colors",
                typeFilter === t
                  ? "text-white border-transparent"
                  : "border-border hover:border-primary/40",
              )}
              style={typeFilter === t
                ? { background: "var(--primary)", color: "white" }
                : { background: "var(--background)", color: "var(--muted-foreground)" }}
            >
              {t === "" ? "All" : t}
            </button>
          ))}
          {hasFilters && (
            <button
              onClick={clearFilters}
              className="flex items-center gap-1.5 px-3.5 py-1.5 rounded-full text-[12.5px] font-semibold border transition-colors"
              style={{ borderColor: "var(--destructive)", color: "var(--destructive)", background: "var(--background)" }}
            >
              <X size={12} /> Clear filters
            </button>
          )}
        </div>
      </div>

      {/* ── Grid ── */}
      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : jobs.length === 0 ? (
        <EmptyState
          icon={<Briefcase size={40} />}
          title="No jobs found"
          description={hasFilters ? "Try adjusting your filters." : "Check back later for new opportunities."}
        />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {jobs.map((j) => {
            const ts = typeStyle[j.type];
            const isExpired = j.deadline && new Date(j.deadline) < new Date();
            return (
              <Link
                key={j.id}
                href={`/jobs/${j.id}`}
                className="group block rounded-2xl border transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md overflow-hidden"
                style={{ borderColor: "var(--border)", background: "var(--background)" }}
              >
                <div className="p-5 flex flex-col gap-4 h-full">

                  {/* Top row: icon + type badge */}
                  <div className="flex items-start justify-between gap-3">
                    <div
                      className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0 transition-colors duration-200"
                      style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
                    >
                      <Briefcase size={17} style={{ color: "var(--primary)" }} />
                    </div>
                    {ts ? (
                      <span
                        className="text-[11px] font-semibold px-2.5 py-0.5 rounded-full shrink-0"
                        style={{ background: ts.bg, color: ts.text, border: `1px solid ${ts.border}` }}
                      >
                        {j.type}
                      </span>
                    ) : (
                      <span
                        className="text-[11px] font-semibold px-2.5 py-0.5 rounded-full shrink-0"
                        style={{ background: "var(--secondary)", color: "var(--muted-foreground)", border: "1px solid var(--border)" }}
                      >
                        {j.type}
                      </span>
                    )}
                  </div>

                  {/* Title + company */}
                  <div className="flex-1 min-w-0">
                    <h3
                      className="text-[15px] font-semibold leading-snug line-clamp-2 mb-1 transition-colors duration-200 group-hover:text-primary"
                      style={{ color: "var(--foreground)" }}
                    >
                      {j.title}
                    </h3>
                    <p className="text-[13.5px] font-medium" style={{ color: "var(--muted-foreground)" }}>
                      {j.company}
                    </p>
                  </div>

                  {/* Meta */}
                  <div className="flex flex-wrap gap-x-4 gap-y-1.5">
                    <span className="flex items-center gap-1.5 text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>
                      <MapPin size={12} /> {j.location}
                    </span>
                    {j.deadline && (
                      <span
                        className="flex items-center gap-1.5 text-[12.5px]"
                        style={{ color: isExpired ? "var(--destructive)" : "var(--muted-foreground)" }}
                      >
                        <Clock size={12} />
                        {isExpired ? "Closed" : formatDate(j.deadline)}
                      </span>
                    )}
                  </div>

                  {/* Description preview */}
                  {j.description && (
                    <p className="text-[12.5px] line-clamp-2 leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
                      {j.description}
                    </p>
                  )}

                  {/* CTA */}
                  <div
                    className="flex items-center justify-between pt-3 border-t"
                    style={{ borderColor: "var(--border)" }}
                  >
                    <span className="text-[13px] font-semibold transition-colors duration-200 group-hover:text-primary"
                      style={{ color: "var(--muted-foreground)" }}>
                      View details
                    </span>
                    <div
                      className="w-7 h-7 rounded-full flex items-center justify-center transition-colors duration-200 group-hover:bg-primary"
                      style={{ background: "var(--secondary)" }}
                    >
                      <ArrowRight size={13} className="transition-colors duration-200 group-hover:text-white"
                        style={{ color: "var(--muted-foreground)" }} />
                    </div>
                  </div>
                </div>
              </Link>
            );
          })}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
    </div>
  );
}
