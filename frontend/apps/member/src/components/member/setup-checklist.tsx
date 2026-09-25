"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { GetStartedChecklist, type ChecklistItem } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { useDisabledFeatures } from "@/components/member/member-layout";
import { getCommunities, getCurrentMembershipCampaign, getMyCommunities, getMyProfile, getMyRsvps } from "@/lib/member-api";

/** First steps for a new member, each derived from real data so it ticks itself off. A step is left out entirely when it can't be completed here (no dues campaign, no communities yet, Events turned off). */
export function MemberSetupChecklist() {
  const { user } = useAuth();
  const router = useRouter();
  const disabledFeatures = useDisabledFeatures();
  const eventsEnabled = !disabledFeatures.has("Events");

  const profile = useQuery({ queryKey: ["m-profile"], queryFn: getMyProfile });
  const dues = useQuery({ queryKey: ["m-current-membership-campaign"], queryFn: getCurrentMembershipCampaign, staleTime: 5 * 60 * 1000 });
  const allCommunities = useQuery({ queryKey: ["m-communities"], queryFn: getCommunities, staleTime: 5 * 60 * 1000 });
  const myCommunities = useQuery({ queryKey: ["m-my-communities"], queryFn: getMyCommunities });
  const rsvps = useQuery({ queryKey: ["m-rsvps"], queryFn: () => getMyRsvps(), enabled: eventsEnabled });

  // A failed load isn't "not done yet": hide rather than tell a member to redo steps they may have finished.
  if (!profile.data || dues.data === undefined || !allCommunities.data || !myCommunities.data || (eventsEnabled && !rsvps.data)) return null;

  const items: ChecklistItem[] = [
    {
      id: "profile",
      title: "Add a photo and short bio",
      description: "Classmates find and recognize you in the directory by these.",
      done: !!profile.data?.profilePictureUrl && !!profile.data?.bio?.trim(),
      actionLabel: "Update your profile",
      onAction: () => router.push("/profile"),
    },
    ...(dues.data
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
    ...((allCommunities.data?.length ?? 0) > 0
      ? [{
          id: "community",
          title: "Join a community",
          description: "Communities are smaller groups with their own news, events, and discussions.",
          done: (myCommunities.data ?? []).some((c) => c.myStatus === "Approved" || c.myStatus === "Pending"),
          actionLabel: "Browse communities",
          onAction: () => router.push("/communities"),
        }]
      : []),
    ...(eventsEnabled
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

  const loading = profile.isLoading || dues.isLoading || allCommunities.isLoading || myCommunities.isLoading || (eventsEnabled && rsvps.isLoading);

  return (
    <GetStartedChecklist
      items={items}
      loading={loading}
      storageKey={`member-get-started:${user?.id ?? "anon"}`}
      className="bottom-[5.5rem] lg:bottom-4"
    />
  );
}
