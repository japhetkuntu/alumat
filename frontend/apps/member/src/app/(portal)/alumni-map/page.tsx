"use client";

import { useMemo, useState } from "react";
import dynamic from "next/dynamic";
import { useQuery } from "@tanstack/react-query";
import { Globe, Users, MapPin, Search } from "lucide-react";
import { PageHeader } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { getAlumniMap } from "@/lib/member-api";
import { countryLabel } from "@/lib/country-centroids";

const AlumniMapView = dynamic(
  () => import("./alumni-map-view").then((m) => m.AlumniMapView),
  { ssr: false, loading: () => <div className="h-full w-full animate-pulse bg-muted" /> }
);

function StatTile({ icon: Icon, value, label, tone }: { icon: typeof Globe; value: string | number; label: string; tone: "dark" | "brand" | "default" }) {
  const styles =
    tone === "dark" ? { background: "var(--foreground)", color: "var(--background)" } :
    tone === "brand" ? { background: "var(--primary)", color: "var(--primary-foreground)" } :
    { background: "var(--card)", color: "var(--foreground)", border: "1px solid var(--border)" };
  return (
    <div className="rounded-2xl p-5 flex flex-col items-center justify-center text-center gap-1.5" style={styles}>
      <Icon size={18} className="opacity-80 mb-1" />
      <p className="text-[22px] font-bold leading-none">{value}</p>
      <p className="text-[12px] opacity-75">{label}</p>
    </div>
  );
}

export default function AlumniMapPage() {
  const [search, setSearch] = useState("");

  const { data: members = [], isLoading } = useQuery({
    queryKey: ["alumni-map"],
    queryFn: getAlumniMap,
  });

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return members;
    return members.filter((m) =>
      `${m.firstName} ${m.lastName}`.toLowerCase().includes(q) ||
      (m.location ?? "").toLowerCase().includes(q)
    );
  }, [members, search]);

  const stats = useMemo(() => {
    const countries = new Map<string, number>();
    for (const m of filtered) {
      const country = countryLabel(m.location);
      if (!country) continue;
      countries.set(country, (countries.get(country) ?? 0) + 1);
    }
    let topCountry: string | null = null;
    let topCount = 0;
    for (const [country, count] of countries) {
      if (count > topCount) { topCountry = country; topCount = count; }
    }
    return {
      mapped: filtered.length,
      countryCount: countries.size,
      topCountry,
      topCount,
    };
  }, [filtered]);

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">
      <div className="flex flex-col sm:flex-row sm:items-end sm:justify-between gap-4">
        <PageHeader
          title="Alumni Map"
          description="Where fellow graduates live around the world — shown only for alumni who chose to appear here."
        />
        <div className="relative w-full sm:w-[280px]">
          <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground pointer-events-none" />
          <Input
            placeholder="Search name or location…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9"
          />
        </div>
      </div>

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <StatTile icon={Users} value={stats.mapped} label="Mapped alumni" tone="dark" />
        <StatTile icon={Globe} value={stats.countryCount} label="Countries" tone="brand" />
        <StatTile icon={MapPin} value={stats.topCountry ?? "—"} label="Top country" tone="default" />
        <StatTile icon={MapPin} value={stats.topCount || 0} label="In top country" tone="default" />
      </div>

      {isLoading ? (
        <div className="h-[360px] sm:h-[460px] lg:h-[560px] rounded-2xl bg-muted animate-pulse" />
      ) : members.length === 0 ? (
        <EmptyState
          icon={<Globe size={26} />}
          title="No alumni on the map yet"
          description="The map fills in as members opt in from their profile settings and share where they're based."
        />
      ) : (
        <div className="h-[360px] sm:h-[460px] lg:h-[560px] rounded-2xl overflow-hidden border shadow-sm" style={{ borderColor: "var(--border)" }}>
          <AlumniMapView members={filtered} />
        </div>
      )}
    </div>
  );
}
