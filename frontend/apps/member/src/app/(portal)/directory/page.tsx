"use client";

import { MeetYourLeaders, useLeaderTitles } from "@/components/member/leaders";
import { IntroduceButton } from "@/components/member/introduce-button";
import { useCallback, useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import { createPortal } from "react-dom";
import { LoadError } from "@alumni/ui";
import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { Search, Linkedin, X, Building2, MapPin, User } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { SearchableSelect } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { searchDirectory, getMyProfile, getDirectorySuggestions, getBatches } from "@/lib/member-api";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { ensureAbsoluteUrl } from "@alumni/ui";
import type { Member } from "@/types";
import { DirectoryMemberCard } from "@/components/member/directory-member-card";
import { useVisualViewportHeight } from "@/hooks/use-visual-viewport-height";
import { useNavTheme, useCohortPhrase, useFeatures } from "@/components/member/member-layout";

const currentYear    = new Date().getFullYear();
const GRAD_YEAR_START = 1952;
const years = Array.from(
  { length: currentYear - GRAD_YEAR_START + 1 },
  (_, i) => currentYear - i,
);

/** An invisible marker under the grid: when it scrolls into view (a screen early) the next page is requested. */
function LoadMore({ hasMore, loading, failed, onMore }: { hasMore: boolean; loading: boolean; failed: boolean; onMore: () => void }) {
  const marker = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const el = marker.current;
    if (!el || !hasMore || loading || failed) return;
    const watch = new IntersectionObserver(([e]) => { if (e.isIntersecting) onMore(); }, { rootMargin: "600px 0px" });
    watch.observe(el);
    return () => watch.disconnect();
  }, [hasMore, loading, failed, onMore]);
  if (!hasMore) return null;
  return (
    <div ref={marker} className="flex min-h-16 items-center justify-center" aria-live="polite">
      {loading && <span className="h-5 w-5 animate-spin rounded-full border-2 border-muted-foreground/30 border-t-muted-foreground" role="status" aria-label="Loading more members" />}
      {failed && <Button variant="outline" size="sm" className="font-semibold" onClick={onMore}>Couldn&apos;t load more. Try again</Button>}
    </div>
  );
}

export default function MemberDirectoryPage() {
  const searchParams = useSearchParams();
  const [typed,      setTyped]      = useState(() => searchParams.get("search") ?? "");
  const [search,     setSearch]     = useState(() => searchParams.get("search") ?? "");
  const [yearFilter, setYearFilter] = useState(() => searchParams.get("year") ?? "");
  const [selected,   setSelected]   = useState<Member | null>(null);
  const pageSize = 24;
  const vvHeight = useVisualViewportHeight();
  const { data: navTheme } = useNavTheme();
  const cohort = useCohortPhrase();
  const introductionsOn = useFeatures().enabled("Introductions");
  const isCommunity = navTheme?.organizationType === "Community";
  const { data: me } = useQuery({ queryKey: ["m-profile"], queryFn: getMyProfile, staleTime: 5 * 60_000 });
  const myYear = me?.graduationYear && me.graduationYear > 0 ? String(me.graduationYear) : "";

  // Typing is sent to the server after a short pause, so a name is one request, not one per letter.
  useEffect(() => { const t = window.setTimeout(() => setSearch(typed.trim()), 250); return () => window.clearTimeout(t); }, [typed]);

  // The directory is read a page at a time: the first 24 are quick however many members there are, and the next 24 arrive as the
  // reader nears the bottom. Changing the search or year starts again from the top.
  const list = useInfiniteQuery({
    queryKey:         ["member-directory", search, yearFilter],
    queryFn:          ({ pageParam }) => searchDirectory({
      search:         search || undefined,
      graduationYear: yearFilter ? Number(yearFilter) : undefined,
      page:           pageParam,
      pageSize,
    }),
    initialPageParam: 1,
    getNextPageParam: (last, all) => (all.length < last.totalPages ? all.length + 1 : undefined),
    placeholderData:  (prev) => prev,
    staleTime:        60_000,
    refetchOnWindowFocus: false,
  });
  const { data, isLoading, isFetching, isFetchingNextPage, isFetchNextPageError, isError, refetch } = list;
  const loadMore = useCallback(() => { if (list.hasNextPage && !list.isFetchingNextPage) void list.fetchNextPage(); }, [list]);

  const suggestions = useQuery({ queryKey: ["member-directory-suggestions"], queryFn: getDirectorySuggestions, staleTime: 5 * 60_000, retry: false });
  // A member can shift between pages while someone scrolls (a new sign-up), so the same person is never listed twice.
  const seen = new Set<string>();
  const members = (data?.pages ?? []).flatMap((p) => p.results).filter((m) => (seen.has(m.id) ? false : (seen.add(m.id), true)));
  const total = data?.pages[0]?.totalCount ?? 0;
  const leaderTitles = useLeaderTitles(members.map((m) => m.id));
  // A year group has its own leaders; when the list is narrowed to one year, they are introduced above the results.
  const { data: yearGroups = [] } = useQuery({ queryKey: ["m-batches"], queryFn: getBatches, staleTime: 10 * 60_000, enabled: !isCommunity && !!yearFilter });
  const yearGroup = yearFilter ? yearGroups.find((b) => String(b.year) === yearFilter) : undefined;
  const hasFilters = !!(typed || search || yearFilter);

  function clearFilters() { setTyped(""); setSearch(""); setYearFilter(""); }

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      <PageHeader
        title={isCommunity ? "Directory" : "Member directory"}
        description={isCommunity
          ? "Find any member in seconds: search by name or location, from local chapters to the diaspora."
          : "Find any member in seconds: search by name, graduation year, or location, from local chapters to the diaspora."}
      />

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
            value={typed}
            onChange={e => setTyped(e.target.value)}
            className="h-11 pl-9 text-[14px]"
          />
        </div>
        {!isCommunity && (
          <SearchableSelect
            value={yearFilter || "__all__"}
            onValueChange={v => setYearFilter(v === "__all__" ? "" : v)}
            placeholder="All years"
            searchPlaceholder="Type a year"
            className="sm:w-56"
            options={[
              { value: "__all__", label: "All graduation years" },
              ...years.filter(y => y > 0).map(y => ({ value: String(y), label: cohort.of(y) })),
            ]}
          />
        )}
        {hasFilters && (
          <button
            onClick={clearFilters}
            className="flex items-center gap-1.5 self-center px-3.5 py-2 text-[12.5px] font-semibold border transition-colors whitespace-nowrap"
            style={{ borderColor: "var(--destructive)", color: "var(--destructive)", background: "var(--background)" }}
          >
            <X size={12} /> Clear
          </button>
        )}
      </div>

      {yearGroup && <MeetYourLeaders scopeType="Batch" scopeId={yearGroup.id} noun={cohort.word.toLowerCase()} />}

      {/* One tap to the people they most likely know. */}
      {!isCommunity && myYear && (
        <div className="flex flex-wrap items-center gap-2" role="group" aria-label="Quick filters">
          <button
            type="button"
            aria-pressed={yearFilter === myYear}
            onClick={() => setYearFilter(yearFilter === myYear ? "" : myYear)}
            className={`rounded-full border px-3.5 py-1.5 text-[13px] font-medium transition-colors ${yearFilter === myYear ? "border-primary bg-primary text-primary-foreground" : "border-border text-muted-foreground hover:text-foreground"}`}
          >
            My class, {myYear}
          </button>
        </div>
      )}

      {/* Subtle "updating" indicator */}
      {isFetching && !isLoading && !isFetchingNextPage && (
        <p className="text-[12.5px] font-medium text-center animate-pulse" style={{ color: "var(--primary)" }}>
          Updating…
        </p>
      )}

      {/* ── People you may know: only on the plain view, before any search or filter ── */}
      {!hasFilters && (suggestions.data?.length ?? 0) > 0 && (
        <section aria-labelledby="suggested-heading" className="space-y-3">
          <h2 id="suggested-heading" className="text-[15px] font-bold">People you may know</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
            {suggestions.data!.map(({ member: m, reasons }) => (
              <div key={m.id} className="flex flex-col gap-1.5">
                <DirectoryMemberCard member={m} titles={leaderTitles.get(m.id)} selected={selected?.id === m.id} isCommunity={isCommunity} canConnect={introductionsOn && m.id !== me?.id}
                  onSelect={() => setSelected(selected?.id === m.id ? null : m)} />
                <p className="px-1 text-[12.5px] text-muted-foreground">{reasons.slice(0, 2).join(" · ")}</p>
              </div>
            ))}
          </div>
          <h2 className="pt-2 text-[15px] font-bold">Everyone</h2>
        </section>
      )}

      {/* ── Grid ── */}
      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {Array.from({ length: 8 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : isError || !data ? (
        <LoadError onRetry={() => refetch()} />
      ) : members.length === 0 ? (
        <EmptyState
          icon={<Search size={40} />}
          title={hasFilters ? "No members found" : "Find and reconnect with your people"}
          description={hasFilters ? "Try adjusting your search or year filter." : "The directory lists members you can search by name, class year, company or location. It fills in as members join and complete their profiles."}
          action={hasFilters ? <Button variant="outline" size="sm" className="font-semibold" onClick={clearFilters}>Clear filters</Button> : undefined}
        />
      ) : (
        <>
          <p className="text-[12.5px] font-medium text-muted-foreground" aria-live="polite">
            {total.toLocaleString()} {total === 1 ? "member" : "members"}{hasFilters ? " match" : ""}
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
            {members.map(m => (
              <DirectoryMemberCard key={m.id} member={m} titles={leaderTitles.get(m.id)} selected={selected?.id === m.id} isCommunity={isCommunity} canConnect={introductionsOn && m.id !== me?.id}
                onSelect={() => setSelected(selected?.id === m.id ? null : m)} />
            ))}
          </div>
          <LoadMore hasMore={!!list.hasNextPage} loading={isFetchingNextPage} failed={isFetchNextPageError} onMore={loadMore} />
          {!list.hasNextPage && members.length > pageSize && (
            <p className="pb-2 text-center text-[13px] text-muted-foreground">You&apos;ve seen everyone{hasFilters ? " who matches" : ""}.</p>
          )}
        </>
      )}

      {/* ── Profile drawer ── */}
      {selected && typeof document !== "undefined" && createPortal(
        <div
          className="fixed mobile-sheet-overlay z-[60] flex items-end sm:items-center justify-center sm:justify-end"
          style={vvHeight ? { height: vvHeight } : undefined}
          onClick={() => setSelected(null)}
        >
          {/* Backdrop */}
          <div
            className="absolute inset-0"
            style={{ background: "rgba(0,0,0,0.25)", backdropFilter: "blur(3px)" }}
          />

          {/* Panel */}
          <div
            className="relative z-[61] w-full sm:w-[360px] sm:m-4 bottom-sheet-scroll rounded-t-2xl sm:rounded-2xl border animate-in slide-in-from-bottom-6 sm:slide-in-from-right-6 duration-300"
            style={{
              background: "var(--background)", borderColor: "var(--border)", boxShadow: "0 8px 40px rgba(0,0,0,0.15)",
              ...(vvHeight ? { maxHeight: vvHeight * 0.85 } : {}),
            }}
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
                  className="text-[12px] font-semibold tracking-[0.1em] uppercase"
                  style={{ color: "var(--muted-foreground)" }}
                >
                  {isCommunity ? "Member profile" : "Member profile"}
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
                    {!isCommunity && selected.graduationYear > 0 && (
                      <Badge variant="secondary" className="text-[12px] font-semibold">
                        {cohort.of(selected.graduationYear)}
                      </Badge>
                    )}
                    {selected.departmentName && (
                      <Badge variant="outline" className="text-[12px] font-semibold">
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
                  This member hasn&apos;t added more details to their profile yet.
                </p>
              )}

              {/* LinkedIn */}
              {selected.linkedInUrl && (
                <a
                  href={ensureAbsoluteUrl(selected.linkedInUrl)}
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

              <IntroduceButton memberId={selected.id} firstName={selected.firstName}
                person={{ name: `${selected.firstName} ${selected.lastName}`.trim(), photoUrl: selected.profilePictureUrl, subtitle: [selected.jobTitle, selected.company].filter(Boolean).join(" at ") || (selected.graduationYear > 0 ? `Class of ${selected.graduationYear}` : null) }} />
            </div>
          </div>
        </div>
        , document.body)}
    </div>
  );
}
