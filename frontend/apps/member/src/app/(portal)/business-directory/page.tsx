"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Building2, MapPin, Search, Store, Settings2 } from "lucide-react";
import { Pagination } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { getBusinessListings, getMyBusinessListing } from "@/lib/member-api";

export default function BusinessDirectoryPage() {
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 12;

  const { data, isLoading } = useQuery({
    queryKey: ["m-business-listings", search, page],
    queryFn: () => getBusinessListings(page, pageSize, search || undefined),
    placeholderData: (prev) => prev,
  });

  const { data: myListing } = useQuery({
    queryKey: ["m-my-business-listing"],
    queryFn: getMyBusinessListing,
  });

  const listings = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSearch(searchInput.trim());
    setPage(1);
  };

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-6 sm:space-y-8 lg:space-y-10 selection:bg-primary/20">
      <PageHeader
        eyebrow="Community"
        title="Business Directory"
        description="Discover and support businesses run by fellow alumni."
      >
        <Link href="/business-directory/mine">
          <Button size="sm" className="font-semibold gap-1.5">
            {myListing ? <><Settings2 size={14} />Manage my listing</> : <><Store size={14} />List your business</>}
          </Button>
        </Link>
      </PageHeader>

      <form onSubmit={handleSearchSubmit} className="relative max-w-sm animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
        <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
        <Input
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          placeholder="Search businesses by name..."
          className="pl-9"
        />
      </form>

      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : listings.length === 0 ? (
        search ? (
          <EmptyState icon={<Building2 size={48} />} title="No businesses found" description="Try a different search term." />
        ) : (
          <EmptyState icon={<Building2 size={48} />} title="No businesses listed yet" description="Be the first alumnus to list your business here." />
        )
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-150">
          {listings.map((biz) => (
            <Link key={biz.id} href={`/business-directory/${biz.id}`}>
              <Card className="group flex flex-col overflow-hidden hover:-translate-y-1 hover:shadow-lg transition-all duration-300 h-full">
                <div className="relative h-32 overflow-hidden flex-shrink-0 bg-muted/30">
                  {biz.bannerUrl ? (
                    <img src={biz.bannerUrl} alt="" className="w-full h-full object-cover transition-transform duration-700 group-hover:scale-110" loading="lazy" />
                  ) : (
                    <div className="w-full h-full bg-gradient-to-br from-primary/10 to-muted/20" />
                  )}
                  <div className="absolute inset-0 bg-gradient-to-t from-black/30 via-transparent to-transparent" />
                  <div className="absolute -bottom-6 left-4 h-14 w-14 rounded-xl border-2 border-background bg-background shadow-md overflow-hidden flex items-center justify-center">
                    {biz.logoUrl ? (
                      <img src={biz.logoUrl} alt={biz.businessName} className="w-full h-full object-cover" loading="lazy" />
                    ) : (
                      <Building2 size={22} className="text-primary/50" />
                    )}
                  </div>
                </div>

                <CardContent className="flex-1 flex flex-col p-5 pt-8 space-y-2">
                  <h3 className="font-[family-name:var(--font-display)] text-[15px] font-semibold leading-snug group-hover:text-primary transition-colors line-clamp-1">
                    {biz.businessName}
                  </h3>
                  <p className="text-[12px] text-muted-foreground line-clamp-2 flex-1">{biz.description}</p>
                  <div className="flex items-center gap-1 text-[11px] text-muted-foreground pt-1">
                    <MapPin size={11} className="shrink-0" />
                    <span className="truncate">{biz.location}</span>
                  </div>
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
