"use client";

import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { badgeTitle, getMyBadges } from "@/lib/member-api";

function seenKey(userId: string) { return `badges-seen:${userId}`; }
const QUIET_AFTER_DAYS = 7;

/**
 * Says so when a member has earned a badge. Looking at the badges is also what awards them, so this runs once when the home page
 * opens. A badge is announced once; ones earned long ago (before this existed, or while the member was away for weeks) are
 * marked as seen without a fuss, so nobody is greeted by a pile of old news.
 */
export function BadgeCelebration() {
  const { user } = useAuth();
  const router = useRouter();
  const { data } = useQuery({ queryKey: ["m-badges"], queryFn: getMyBadges, staleTime: 5 * 60_000, retry: false });

  useEffect(() => {
    if (!user?.id || !data) return;
    let seen: string[] = [];
    try { seen = JSON.parse(localStorage.getItem(seenKey(user.id)) ?? "[]"); } catch { /* start fresh */ }
    const fresh = data.filter((b) => !seen.includes(b.id));
    if (fresh.length === 0) return;
    const recent = fresh.filter((b) => Date.now() - new Date(b.earnedAt).getTime() < QUIET_AFTER_DAYS * 86_400_000);
    recent.slice(0, 3).forEach((b, i) => {
      setTimeout(() => toast.success(`You earned a badge: ${badgeTitle(b.badgeType)}`, {
        description: b.description,
        duration: 8000,
        action: { label: "See it", onClick: () => router.push("/profile") },
      }), 900 + i * 700);
    });
    try { localStorage.setItem(seenKey(user.id), JSON.stringify([...seen, ...fresh.map((b) => b.id)])); } catch { /* it may be announced again next time */ }
  }, [data, user?.id, router]);

  return null;
}
