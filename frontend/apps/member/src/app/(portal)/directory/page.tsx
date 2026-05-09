"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Search, Linkedin, X, GraduationCap, Building2, MapPin, User } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { UserAvatar } from "@/components/ui/user-avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { FormSelect } from "@/components/ui/select";
import { SearchModal } from "@/components/ui/search-modal";
import { searchDirectory } from "@/lib/member-api";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import type { Member } from "@/types";

export default function MemberDirectoryPage() {
  const [search, setSearch] = useState("");
  const [yearFilter, setYearFilter] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<Member | null>(null);
  const pageSize = 24;

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ["member-directory", search, yearFilter, page],
    queryFn: () => searchDirectory({
      search: search || undefined,
      graduationYear: yearFilter ? Number(yearFilter) : undefined,
      page,
      pageSize,
    }),
    placeholderData: (prev) => prev,
  });

  const members = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;
  const currentYear = new Date().getFullYear();
  const GRAD_YEAR_START = 1952;
  const years = Array.from({ length: currentYear - GRAD_YEAR_START + 1 }, (_, i) => currentYear - i);

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-6 sm:space-y-8 lg:space-y-10 selection:bg-primary/20">
      <header className="space-y-2 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <h1 className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tight">Alumni Directory</h1>
        <p className="text-muted-foreground text-sm sm:text-base lg:text-lg font-medium leading-relaxed max-w-2xl">
          Connect with fellow UMaT graduates from across the world — search by name, company, or location.
        </p>
      </header>

      <div className="flex flex-col sm:flex-row gap-3 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
        <div className="flex-1 min-w-0">
          <SearchModal
            title="Search alumni"
            value={search}
            onChange={(value) => { setSearch(value); setPage(1); }}
            placeholder="Search by name, company, or location..."
          >
            {isLoading ? (
              <p className="text-sm text-muted-foreground">Searching...</p>
            ) : members.length === 0 ? (
              <p className="text-sm text-muted-foreground">No results found. Try a different search.</p>
            ) : (
              <div className="space-y-2 max-h-72 overflow-y-auto">
                {members.slice(0, 5).map((m) => (
                  <div key={m.id} className="flex items-center justify-between gap-3 rounded-lg border border-border/40 bg-muted/30 px-3 py-2">
                    <div className="min-w-0">
                      <p className="text-sm font-semibold truncate">{m.firstName} {m.lastName}</p>
                      <p className="text-xs text-muted-foreground truncate">
                        {m.jobTitle ?? "No title"}{m.company ? ` · ${m.company}` : ""}
                      </p>
                    </div>
                    <Badge variant="secondary" className="text-[10px] font-bold uppercase">
                      Class of {m.graduationYear}
                    </Badge>
                  </div>
                ))}
                {members.length > 5 && (
                  <p className="text-xs text-muted-foreground">Showing {Math.min(5, members.length)} of {members.length} results. Close to view the full list.</p>
                )}
              </div>
            )}
          </SearchModal>
        </div>
        <FormSelect
          value={yearFilter || "__all__"}
          onValueChange={(v) => { setYearFilter(v === "__all__" ? "" : v); setPage(1); }}
          placeholder="All years"
          className="w-full sm:w-44"
          options={[
            { value: "__all__", label: "All graduation years" },
            ...years.map((y) => ({ value: String(y), label: `Class of ${y}` })),
          ]}
        />
        {(search || yearFilter) && (
          <button
            onClick={() => { setSearch(""); setYearFilter(""); setPage(1); }}
            className="flex items-center gap-1 self-center px-3 py-2 rounded-full text-[11px] font-black uppercase tracking-widest border border-destructive/40 text-destructive hover:bg-destructive/10 transition-all whitespace-nowrap"
          >
            <X size={11} /> Clear
          </button>
        )}
      </div>

      {isFetching && !isLoading && (
        <p className="text-[12px] text-primary font-bold animate-pulse text-center -mt-4">Updating...</p>
      )}

      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : members.length === 0 ? (
        <EmptyState icon={<Search size={48} />} title="No alumni found" description="Try adjusting your search or filter criteria." />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {members.map((m, i) => {
            const fullName = `${m.firstName} ${m.lastName}`;
            const isSelected = selected?.id === m.id;
            return (
              <Card
                key={m.id}
                onClick={() => setSelected(isSelected ? null : m)}
                role="button"
                tabIndex={0}
                onKeyDown={(e) => e.key === "Enter" && setSelected(isSelected ? null : m)}
                className={`group overflow-hidden border-border/40 hover:border-primary/30 transition-all duration-500 hover:shadow-xl hover:shadow-primary/5 animate-in fade-in slide-in-from-bottom-4 duration-700 cursor-pointer ${isSelected ? "border-primary/50 ring-1 ring-primary/20 shadow-lg shadow-primary/10" : ""}`}
                style={{ animationDelay: `${i * 50}ms` }}
              >
                <CardContent className="p-6">
                  <div className="flex items-start gap-4">
                    <UserAvatar
                      src={m.profilePictureUrl}
                      name={`${m.firstName} ${m.lastName}`}
                      size="lg"
                    />
                    <div className="flex-1 min-w-0 space-y-1">
                      <p className="font-bold text-[15px] truncate group-hover:text-primary transition-colors">{fullName}</p>
                      {m.jobTitle && (
                        <p className="text-[12px] text-muted-foreground font-medium truncate">
                          {m.jobTitle}{m.company ? ` · ${m.company}` : ""}
                        </p>
                      )}
                      {m.location && (
                        <p className="text-[12px] text-muted-foreground truncate">{m.location}</p>
                      )}
                    </div>
                  </div>

                  {isSelected && m.bio && (
                    <p className="mt-3 text-[12px] text-muted-foreground leading-relaxed border-t border-border/40 pt-3 line-clamp-3">{m.bio}</p>
                  )}

                  <div className="flex items-center gap-2 mt-4 pt-4 border-t border-border/40">
                    <Badge variant="secondary" className="text-[10px] font-bold">Class of {m.graduationYear}</Badge>
                    {m.departmentName && (
                      <Badge variant="outline" className="text-[10px] font-bold truncate max-w-[120px]">{m.departmentName}</Badge>
                    )}
                    {m.linkedInUrl && (
                      <a href={m.linkedInUrl} target="_blank" rel="noopener noreferrer" className="ml-auto" onClick={(e) => e.stopPropagation()}>
                        <Button size="icon" variant="ghost" className="h-8 w-8 rounded-lg hover:bg-blue-500/10 hover:text-blue-600" aria-label="LinkedIn profile">
                          <Linkedin size={15} />
                        </Button>
                      </a>
                    )}
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      {selected && (
        <div className="fixed inset-0 z-40 flex items-end sm:items-center justify-center sm:justify-end" onClick={() => setSelected(null)}>
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm" />
          <div
            className="relative z-50 bg-background border border-border/50 shadow-2xl rounded-t-2xl sm:rounded-2xl w-full sm:w-[360px] sm:m-4 sm:max-h-[90vh] overflow-y-auto animate-in slide-in-from-bottom-8 sm:slide-in-from-right-8 duration-300"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="p-5 space-y-4">
              <div className="flex items-center justify-between">
                <p className="text-[11px] font-black uppercase tracking-widest text-muted-foreground">Alumni Profile</p>
                <button onClick={() => setSelected(null)} className="rounded-full p-1 hover:bg-muted/60 transition-colors">
                  <X size={16} />
                </button>
              </div>
              <div className="flex items-start gap-4">
                <UserAvatar src={selected.profilePictureUrl} name={`${selected.firstName} ${selected.lastName}`} size="lg" />
                <div className="flex-1 min-w-0">
                  <p className="font-bold text-base">{selected.firstName} {selected.lastName}</p>
                  {selected.jobTitle && <p className="text-[12px] text-muted-foreground">{selected.jobTitle}</p>}
                  <div className="flex gap-1.5 mt-1.5 flex-wrap">
                    <Badge variant="secondary" className="text-[10px] font-bold">Class of {selected.graduationYear}</Badge>
                    {selected.departmentName && <Badge variant="outline" className="text-[10px] font-bold">{selected.departmentName}</Badge>}
                  </div>
                </div>
              </div>
              <div className="space-y-2">
                {selected.company && (
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                    <Building2 size={14} className="shrink-0 text-primary/60" />
                    <span>{selected.company}</span>
                  </div>
                )}
                {selected.location && (
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                    <MapPin size={14} className="shrink-0 text-primary/60" />
                    <span>{selected.location}</span>
                  </div>
                )}
                {selected.bio && (
                  <div className="flex items-start gap-2 text-sm text-muted-foreground">
                    <User size={14} className="shrink-0 text-primary/60 mt-0.5" />
                    <p className="leading-relaxed">{selected.bio}</p>
                  </div>
                )}
              </div>
              {selected.linkedInUrl && (
                <a href={selected.linkedInUrl} target="_blank" rel="noopener noreferrer" className="flex items-center gap-2 w-full">
                  <Button className="w-full font-bold gap-2" variant="outline">
                    <Linkedin size={15} className="text-blue-600" /> View LinkedIn Profile
                  </Button>
                </a>
              )}
            </div>
          </div>
        </div>
      )}

      <div className="pt-4">
        <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
      </div>
    </div>
  );
}
