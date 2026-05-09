"use client";

import { useQuery } from "@tanstack/react-query";
import { Trophy, Medal, TrendingUp } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { formatCurrency } from "@/lib/utils";
import { getLeaderboard } from "@/lib/member-api";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { useAuth } from "@/hooks/use-auth";

export default function LeaderboardPage() {
  const { user } = useAuth();
  const { data: entries, isLoading } = useQuery({
    queryKey: ["m-leaderboard"],
    queryFn: getLeaderboard,
  });

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-6 sm:space-y-8 lg:space-y-10 selection:bg-primary/20">
      <header className="space-y-2 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <h1 className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tight text-foreground">
          Year Group Leaderboard
        </h1>
        <p className="text-muted-foreground text-sm sm:text-base lg:text-lg font-medium leading-relaxed max-w-2xl">
          See how your graduating class ranks against others. Compete for the top spot through membership and contributions!
        </p>
      </header>

      {isLoading ? (
        <div className="space-y-4">
          {Array.from({ length: 5 }).map((_, i) => (
            <CardSkeleton key={i} />
          ))}
        </div>
      ) : !entries?.length ? (
        <EmptyState icon={<Trophy size={48} />} title="No leaderboard data yet" description="Leaderboard will populate as members make contributions." />
      ) : (
        <div className="space-y-3">
          {entries.map((entry, index) => {
            const isMyYear = user?.graduationYear === entry.yearGroup;
            return (
              <Card
                key={entry.yearGroup}
                className={`transition-all hover:shadow-md ${isMyYear ? "ring-2 ring-primary/50 bg-primary/5" : ""}`}
              >
                <CardContent className="flex items-center gap-4 p-4 sm:p-5">
                  <div className="flex items-center justify-center w-10 h-10 sm:w-12 sm:h-12 rounded-full bg-muted font-bold text-lg shrink-0">
                    {index === 0 ? (
                      <Trophy className="w-5 h-5 sm:w-6 sm:h-6 text-yellow-500" />
                    ) : index === 1 ? (
                      <Medal className="w-5 h-5 sm:w-6 sm:h-6 text-gray-400" />
                    ) : index === 2 ? (
                      <Medal className="w-5 h-5 sm:w-6 sm:h-6 text-amber-600" />
                    ) : (
                      <span className="text-muted-foreground">{index + 1}</span>
                    )}
                  </div>

                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <h3 className="font-bold text-base sm:text-lg">Class of {entry.yearGroup}</h3>
                      {isMyYear && <Badge variant="info">Your Class</Badge>}
                    </div>
                    <p className="text-sm text-muted-foreground">
                      {entry.totalMembers} members
                    </p>
                  </div>

                  <div className="hidden sm:flex items-center gap-6 text-sm">
                    <div className="text-center">
                      <p className="font-semibold text-foreground">{Math.round(entry.membershipRate)}%</p>
                      <p className="text-muted-foreground text-xs">Membership</p>
                    </div>
                    <div className="text-center">
                      <p className="font-semibold text-foreground">{formatCurrency(entry.totalContributed)}</p>
                      <p className="text-muted-foreground text-xs">Contributed</p>
                    </div>
                    <div className="text-center">
                      <p className="font-semibold text-foreground">{entry.eventAttendanceCount}</p>
                      <p className="text-muted-foreground text-xs">Events</p>
                    </div>
                  </div>

                  <div className="sm:hidden flex flex-col items-end gap-0.5">
                    <div className="flex items-center gap-1 text-sm font-bold text-primary">
                      <TrendingUp className="w-3.5 h-3.5" />
                      {Math.round(entry.membershipRate)}%
                    </div>
                    <p className="text-[11px] text-muted-foreground">{formatCurrency(entry.totalContributed)}</p>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
