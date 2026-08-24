"use client";

import { useQuery } from "@tanstack/react-query";
import { Trophy, Medal, TrendingUp, Users, CreditCard, Calendar } from "lucide-react";
import { Badge } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { getLeaderboard } from "@/lib/member-api";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { cn } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";

/* Medal colours */
const RANK_STYLE = [
  { bg: "rgba(234,179,8,0.12)",  border: "rgba(234,179,8,0.35)",  text: "#b45309" }, // 1st — gold
  { bg: "rgba(148,163,184,0.12)", border: "rgba(148,163,184,0.35)", text: "#64748b" }, // 2nd — silver
  { bg: "rgba(180,83,9,0.10)",   border: "rgba(180,83,9,0.3)",    text: "#92400e" }, // 3rd — bronze
];

function RankBadge({ index }: { index: number }) {
  if (index < 3) {
    const s = RANK_STYLE[index];
    const Icon = index === 0 ? Trophy : Medal;
    return (
      <div
        className="w-10 h-10 sm:w-11 sm:h-11 rounded-full flex items-center justify-center shrink-0"
        style={{ background: s.bg, border: `1.5px solid ${s.border}` }}
      >
        <Icon size={index === 0 ? 18 : 17} style={{ color: s.text }} />
      </div>
    );
  }
  return (
    <div
      className="w-10 h-10 sm:w-11 sm:h-11 rounded-full flex items-center justify-center shrink-0 text-[14px] font-bold tabular-nums"
      style={{ background: "var(--secondary)", color: "var(--muted-foreground)", border: "1px solid var(--border)" }}
    >
      {index + 1}
    </div>
  );
}

export default function LeaderboardPage() {
  const { user } = useAuth();

  const { data: entries, isLoading } = useQuery({
    queryKey: ["m-leaderboard"],
    queryFn:  getLeaderboard,
  });

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      {/* ── Header ── */}
      <PageHeader
        eyebrow="Community progress"
        title="Class leaderboard"
        description="See how your graduating class ranks against others through membership, contributions, and events."
      />

      {/* ── List ── */}
      {isLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 5 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : !entries?.length ? (
        <EmptyState
          icon={<Trophy size={40} />}
          title="No leaderboard data yet"
          description="Rankings will appear as members make contributions and attend events."
        />
      ) : (
        <div className="space-y-2.5">
          {entries.map((entry, index) => {
            const isMyYear = user?.graduationYear === entry.yearGroup;

            return (
              <div
                key={entry.yearGroup}
                className={cn(
                  "card transition-all duration-150",
                  isMyYear && "ring-2 ring-primary/25 bg-primary/5 border-primary/30",
                )}
              >
                <div className="flex items-center gap-3 sm:gap-4 p-4 sm:p-5">

                  {/* Rank circle */}
                  <RankBadge index={index} />

                  {/* Class + member count */}
                  <div className="flex-1 min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <h3
                        className="font-[family-name:var(--font-display)] font-bold"
                        style={{ fontSize: "1rem", color: "var(--foreground)" }}
                      >
                        Class of {entry.yearGroup}
                      </h3>
                      {isMyYear && (
                        <Badge variant="info" className="text-[10.5px] font-semibold">
                          Your class
                        </Badge>
                      )}
                    </div>
                    <p className="text-[12.5px] mt-0.5 flex items-center gap-1" style={{ color: "var(--muted-foreground)" }}>
                      <Users size={11} />
                      {entry.totalMembers} members
                    </p>
                  </div>

                  {/* Stats — 3 columns on sm+, condensed on mobile */}
                  <div className="hidden sm:flex items-center gap-6 sm:gap-8 shrink-0">
                    {[
                      { icon: TrendingUp, value: `${Math.round(entry.membershipRate)}%`, label: "Membership" },
                      { icon: CreditCard, value: formatCurrency(entry.totalContributed),  label: "Contributed" },
                      { icon: Calendar,   value: entry.eventAttendanceCount,              label: "Events"      },
                    ].map(({ icon: Icon, value, label }) => (
                      <div key={label} className="text-center">
                        <div className="flex items-center justify-center gap-1 mb-0.5">
                          <Icon size={12} style={{ color: "var(--primary)" }} />
                          <p className="text-[14px] font-bold tabular-nums" style={{ color: "var(--foreground)" }}>
                            {value}
                          </p>
                        </div>
                        <p className="text-[11px]" style={{ color: "var(--muted-foreground)" }}>{label}</p>
                      </div>
                    ))}
                  </div>

                  {/* Mobile — show just membership rate + contributions stacked */}
                  <div className="sm:hidden flex flex-col items-end gap-1 shrink-0">
                    <p
                      className="text-[14px] font-bold tabular-nums"
                      style={{ color: "var(--primary)" }}
                    >
                      {Math.round(entry.membershipRate)}%
                    </p>
                    <p className="text-[11.5px] font-medium tabular-nums" style={{ color: "var(--muted-foreground)" }}>
                      {formatCurrency(entry.totalContributed)}
                    </p>
                  </div>

                </div>

                {/* Mobile stat row — sits below the main row, only on xs */}
                <div
                  className="sm:hidden flex items-center justify-around px-4 pb-3 gap-4 border-t"
                  style={{ borderColor: "var(--border)" }}
                >
                  {[
                    { label: "Membership rate", value: `${Math.round(entry.membershipRate)}%` },
                    { label: "Contributed",     value: formatCurrency(entry.totalContributed)  },
                    { label: "Events",          value: entry.eventAttendanceCount              },
                  ].map(({ label, value }) => (
                    <div key={label} className="text-center pt-3">
                      <p className="text-[13px] font-bold tabular-nums" style={{ color: "var(--foreground)" }}>
                        {value}
                      </p>
                      <p className="text-[11px]" style={{ color: "var(--muted-foreground)" }}>{label}</p>
                    </div>
                  ))}
                </div>

              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
