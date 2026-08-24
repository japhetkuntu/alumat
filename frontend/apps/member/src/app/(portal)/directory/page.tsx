"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Search, Linkedin, X, Building2, MapPin, User } from "lucide-react";
import { Pagination } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { searchDirectory } from "@/lib/member-api";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { cn } from "@alumni/ui";
import type { Member } from "@/types";

const currentYear    = new Date().getFullYear();
const GRAD_YEAR_START = 1952;
const years = Array.from(
  { length: currentYear - GRAD_YEAR_START + 1 },
  (_, i) => currentYear - i,
);

export default function MemberDirectoryPage() {
  const [search,     setSearch]     = useState("");
  const [yearFilter, setYearFilter] = useState("");
  const [page,       setPage]       = useState(1);
  const [selected,   setSelected]   = useState<Member | null>(null);
  const pageSize = 24;

  const { data, isLoading, isFetching } = useQuery({
    queryKey:        ["member-directory", search, yearFilter, page],
    queryFn:         () => searchDirectory({
      search:         search || undefined,
      graduationYear: yearFilter ? Number(yearFilter) : undefined,
      page,
      pageSize,
    }),
    placeholderData: (prev) => prev,
  });

  const members    = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;
  const hasFilters = !!(search || yearFilter);

  function clearFilters() { setSearch(""); setYearFilter(""); setPage(1); }

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      <PageHeader title="Alumni directory" description="Find the people and stories that make our community feel like home." />

      {/* ── Filters ── */}
      <div className="flex flex-col sm:flex-row gap-3 max-w-2xl">
        <div className="relative flex-1">
          <Search
            size={15}
            className="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
            style={{ color: "var(--muted-foreground)" }}
          />
          <Input
            placeholder="Search by name, company, location…"
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1); }}
            className="h-11 pl-9 text-[14px]"
          />
        </div>
        <FormSelect
          value={yearFilter || "__all__"}
          onValueChange={v => { setYearFilter(v === "__all__" ? "" : v); setPage(1); }}
          placeholder="All years"
          className="w-full sm:w-48"
          options={[
            { value: "__all__", label: "All graduation years" },
            ...years.map(y => ({ value: String(y), label: `Class of ${y}` })),
          ]}
        />
        {hasFilters && (
          <button
            onClick={clearFilters}
            className="flex items-center gap-1.5 self-center px-3.5 py-2 rounded-full text-[12.5px] font-semibold border transition-colors whitespace-nowrap"
            style={{ borderColor: "var(--destructive)", color: "var(--destructive)", background: "var(--background)" }}
          >
            <X size={12} /> Clear
          </button>
        )}
      </div>

      {/* Subtle "updating" indicator */}
      {isFetching && !isLoading && (
        <p className="text-[12.5px] font-medium text-center animate-pulse" style={{ color: "var(--primary)" }}>
          Updating…
        </p>
      )}

      {/* ── Grid ── */}
      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {Array.from({ length: 8 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : members.length === 0 ? (
        <EmptyState
          icon={<Search size={40} />}
          title="No alumni found"
          description={hasFilters ? "Try adjusting your search or year filter." : "The directory is empty."}
        />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {members.map(m => {
            const isSelected = selected?.id === m.id;
            return (
              <button
                key={m.id}
                onClick={() => setSelected(isSelected ? null : m)}
                className={cn(
                  "text-left rounded-2xl border p-5 transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
                  isSelected && "ring-2 ring-primary/30",
                )}
                style={{
                  borderColor: isSelected ? "var(--primary)" : "var(--border)",
                  background:  "var(--background)",
                }}
              >
                {/* Avatar + name */}
                <div className="flex items-start gap-3 mb-4">
                  <UserAvatar
                    src={m.profilePictureUrl}
                    name={`${m.firstName} ${m.lastName}`}
                    size="lg"
                  />
                  <div className="flex-1 min-w-0">
                    <p
                      className="text-[14.5px] font-semibold leading-snug truncate"
                      style={{ color: "var(--foreground)" }}
                    >
                      {m.firstName} {m.lastName}
                    </p>
                    {m.jobTitle && (
                      <p className="text-[12.5px] mt-0.5 truncate" style={{ color: "var(--muted-foreground)" }}>
                        {m.jobTitle}
                        {m.company ? ` · ${m.company}` : ""}
                      </p>
                    )}
                    {m.location && (
                      <p className="text-[12px] mt-0.5 truncate" style={{ color: "var(--muted-foreground)" }}>
                        {m.location}
                      </p>
                    )}
                  </div>
                </div>

                {/* Badges */}
                <div className="flex flex-wrap items-center gap-1.5 pt-3 border-t" style={{ borderColor: "var(--border)" }}>
                  <Badge variant="secondary" className="text-[10.5px] font-semibold">
                    Class of {m.graduationYear}
                  </Badge>
                  {m.departmentName && (
                    <Badge variant="outline" className="text-[10.5px] font-semibold truncate max-w-[110px]">
                      {m.departmentName}
                    </Badge>
                  )}
                  {m.linkedInUrl && (
                    <a
                      href={m.linkedInUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="ml-auto shrink-0 w-7 h-7 rounded-lg flex items-center justify-center transition-colors hover:bg-blue-50"
                      style={{ color: "#2563eb" }}
                      onClick={e => e.stopPropagation()}
                      aria-label={`${m.firstName} ${m.lastName} on LinkedIn`}
                    >
                      <Linkedin size={14} />
                    </a>
                  )}
                </div>
              </button>
            );
          })}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      {/* ── Profile drawer ── */}
      {selected && (
        <div
          className="fixed inset-0 z-[60] flex items-end sm:items-center justify-center sm:justify-end"
          onClick={() => setSelected(null)}
        >
          {/* Backdrop */}
          <div
            className="absolute inset-0"
            style={{ background: "rgba(0,0,0,0.25)", backdropFilter: "blur(3px)" }}
          />

          {/* Panel */}
          <div
            className="relative z-[61] w-full sm:w-[360px] sm:m-4 max-h-[85vh] sm:max-h-[90vh] overflow-y-auto rounded-t-2xl sm:rounded-2xl border animate-in slide-in-from-bottom-6 sm:slide-in-from-right-6 duration-300"
            style={{ background: "var(--background)", borderColor: "var(--border)", boxShadow: "0 8px 40px rgba(0,0,0,0.15)" }}
            onClick={e => e.stopPropagation()}
          >
            {/* Handle bar — mobile */}
            <div className="flex justify-center pt-3 pb-1 sm:hidden">
              <div className="w-10 h-1 rounded-full" style={{ background: "var(--border)" }} />
            </div>

            <div className="p-5 space-y-5" style={{ paddingBottom: "max(1.25rem, env(safe-area-inset-bottom))" }}>

              {/* Header */}
              <div className="flex items-center justify-between">
                <p
                  className="text-[11px] font-semibold tracking-[0.1em] uppercase"
                  style={{ color: "var(--muted-foreground)" }}
                >
                  Alumni profile
                </p>
                <button
                  onClick={() => setSelected(null)}
                  className="w-7 h-7 rounded-full flex items-center justify-center transition-colors hover:bg-secondary"
                  style={{ color: "var(--muted-foreground)" }}
                  aria-label="Close"
                >
                  <X size={15} />
                </button>
              </div>

              {/* Identity */}
              <div className="flex items-start gap-4">
                <UserAvatar
                  src={selected.profilePictureUrl}
                  name={`${selected.firstName} ${selected.lastName}`}
                  size="lg"
                />
                <div className="flex-1 min-w-0">
                  <p className="text-[16px] font-semibold leading-snug" style={{ color: "var(--foreground)" }}>
                    {selected.firstName} {selected.lastName}
                  </p>
                  {selected.jobTitle && (
                    <p className="text-[13px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
                      {selected.jobTitle}
                    </p>
                  )}
                  <div className="flex flex-wrap gap-1.5 mt-2">
                    <Badge variant="secondary" className="text-[10.5px] font-semibold">
                      Class of {selected.graduationYear}
                    </Badge>
                    {selected.departmentName && (
                      <Badge variant="outline" className="text-[10.5px] font-semibold">
                        {selected.departmentName}
                      </Badge>
                    )}
                  </div>
                </div>
              </div>

              {/* Details */}
              {(selected.company || selected.location || selected.bio) ? (
                <div
                  className="space-y-3 pt-4 border-t"
                  style={{ borderColor: "var(--border)" }}
                >
                  {selected.company && (
                    <div className="flex items-center gap-2.5 text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
                      <Building2 size={14} style={{ color: "var(--primary)" }} className="shrink-0" />
                      {selected.company}
                    </div>
                  )}
                  {selected.location && (
                    <div className="flex items-center gap-2.5 text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
                      <MapPin size={14} style={{ color: "var(--primary)" }} className="shrink-0" />
                      {selected.location}
                    </div>
                  )}
                  {selected.bio && (
                    <div className="flex items-start gap-2.5">
                      <User size={14} style={{ color: "var(--primary)" }} className="shrink-0 mt-0.5" />
                      <p className="text-[13.5px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
                        {selected.bio}
                      </p>
                    </div>
                  )}
                </div>
              ) : (
                <p
                  className="text-[13px] pt-4 border-t"
                  style={{ borderColor: "var(--border)", color: "var(--muted-foreground)" }}
                >
                  This alumnus hasn&apos;t added more details to their profile yet.
                </p>
              )}

              {/* LinkedIn */}
              {selected.linkedInUrl && (
                <a
                  href={selected.linkedInUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="block"
                >
                  <Button
                    variant="outline"
                    className="w-full font-semibold gap-2 text-[13.5px]"
                    style={{ height: 42, borderColor: "#bfdbfe", color: "#2563eb" }}
                  >
                    <Linkedin size={15} />
                    View LinkedIn profile
                  </Button>
                </a>
              )}

            </div>
          </div>
        </div>
      )}
    </div>
  );
}
