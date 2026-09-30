"use client";

import { useSyncExternalStore } from "react";
import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { GetStartedChecklist, type ChecklistItem } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { useDisabledFeatures } from "@/components/member/member-layout";
import { getCommunities, getEvents, getCurrentMembershipCampaign, getMyCommunities, getMyProfile, getMyRsvps } from "@/lib/member-api";

function subscribeDirectoryVisits(notify: () => void) {
  window.addEventListener("storage", notify);
  window.addEventListener("member-directory-visited", notify);
  return () => { window.removeEventListener("storage", notify); window.removeEventListener("member-directory-visited", notify); };
}

function directoryVisitedKey(userId: string) {
  return `member-visited-directory:${userId}`;
}

/** First steps for a new member, each derived from real data so it ticks itself off. A step is left out entirely when it can't be completed here (no dues campaign, no communities yet, Events turned off). The one exception is "browse the directory" — there's no server-side signal for having looked, so a local visited flag stands in for it. */
export function MemberSetupChecklist() {
  const { user } = useAuth();
  const router = useRouter();
  const disabledFeatures = useDisabledFeatures();
  const eventsEnabled = !disabledFeatures.has("Events");
  const contributionsEnabled = !disabledFeatures.has("Contributions");
  const directoryEnabled = !disabledFeatures.has("Directory");
  const directoryVisited = useSyncExternalStore(subscribeDirectoryVisits, () => {
    if (!user?.id) return false;
    try { return localStorage.getItem(directoryVisitedKey(user.id)) === "1"; } catch { return false; }
  }, () => false);

  const profile = useQuery({ queryKey: ["m-profile"], queryFn: getMyProfile });
  const dues = useQuery({ queryKey: ["m-current-membership-campaign"], queryFn: getCurrentMembershipCampaign, enabled: contributionsEnabled, staleTime: 5 * 60 * 1000 });
  const allCommunities = useQuery({ queryKey: ["m-communities"], queryFn: getCommunities, staleTime: 5 * 60 * 1000 });
  const myCommunities = useQuery({ queryKey: ["m-my-communities"], queryFn: getMyCommunities });
  const rsvps = useQuery({ queryKey: ["m-rsvps"], queryFn: () => getMyRsvps(), enabled: eventsEnabled });

  const upcoming = useQuery({ queryKey: ["m-setup-upcoming-events"], queryFn: () => getEvents(1, 10, "Upcoming"), enabled: eventsEnabled, staleTime: 5 * 60 * 1000 });

  // A failed load isn't "not done yet": hide rather than tell a member to redo steps they may have finished.
  if (!profile.data) return null;

  const items: ChecklistItem[] = [
    {
      id: "profile",
      title: "Add a photo and short bio",
      description: "Help other members recognize you and get to know your interests.",
      done: !!profile.data?.profilePictureUrl && !!profile.data?.bio?.trim(),
      actionLabel: "Update your profile",
      onAction: () => router.push("/profile"),
    },
    ...(contributionsEnabled && dues.isSuccess && dues.data
      ? [{
          id: "dues",
          title: "Activate your membership",
          description: "Pay your dues to unlock member-only benefits and your certificate.",
          done: !!profile.data?.isMembershipActive,
          actionLabel: "Pay your dues",
          onAction: () => router.push(`/contributions/${dues.data!.id}`),
        }]
      : []),
    ...(!disabledFeatures.has("AlumniMap")
      ? [{
          id: "location",
          title: "Add your location",
          description: "Appear on the community map so members near you can find you.",
          done: !!profile.data?.showOnAlumniMap || !!profile.data?.location,
          actionLabel: "Add your location",
          onAction: () => router.push("/profile"),
        }]
      : []),
    ...(directoryEnabled
      ? [{
          id: "directory",
          title: profile.data.graduationYear ? "Find someone from your year" : "Meet your community",
          description: profile.data?.graduationYear
            ? `See who else graduated in ${profile.data.graduationYear} and is on AlumUnion.`
            : "Browse the directory to see who else is already here.",
          done: directoryVisited,
          actionLabel: "Browse the directory",
          onAction: () => {
            if (user?.id) {
              try { localStorage.setItem(directoryVisitedKey(user.id), "1"); } catch { /* ignore */ }
            }
            window.dispatchEvent(new Event("member-directory-visited"));
            router.push(
              profile.data?.graduationYear ? `/directory?year=${profile.data.graduationYear}` : "/directory",
            );
          },
        }]
      : []),
    ...(allCommunities.isSuccess && myCommunities.isSuccess && (allCommunities.data?.length ?? 0) > 0
      ? [{
          id: "community",
          title: "Join a community",
          description: "Communities are smaller groups with their own news, events, and discussions.",
          done: (myCommunities.data ?? []).some((c) => c.myStatus === "Approved" || c.myStatus === "Pending"),
          actionLabel: "Browse communities",
          onAction: () => router.push("/communities"),
        }]
      : []),
    ...(eventsEnabled && upcoming.isSuccess && rsvps.isSuccess && ((upcoming.data?.results.length ?? 0) > 0 || (rsvps.data?.length ?? 0) > 0)
      ? [{
          id: "event",
          title: "RSVP to an event",
          description: "See who else is going and put the date in your calendar.",
          done: (rsvps.data?.length ?? 0) > 0,
          actionLabel: "See upcoming events",
          onAction: () => router.push("/events"),
        }]
      : []),
  ];

  const loading = profile.isLoading;

  return (
    <GetStartedChecklist
      items={items}
      loading={loading}
      storageKey={`member-get-started:${user?.id ?? "anon"}`}
      className="bottom-[5.5rem] lg:bottom-4"
    />
  );
}
