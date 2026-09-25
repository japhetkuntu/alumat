"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { GetStartedChecklist, type ChecklistItem } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { getBatches, getCommunities, getEvents, getInstitutionProfile, getMembers } from "@/lib/institution-api";

const DEFAULT_PRIMARY_COLOR = "#2563eb";

/** The setup steps a new institution's SuperAdmin needs to finish, each derived from real data so it ticks itself off. Branding and payouts are SuperAdmin-only settings, so scoped admins never see this. */
export function InstitutionSetupChecklist() {
  const { user } = useAuth();
  const router = useRouter();
  const isSuperAdmin = user?.role === "SuperAdmin";

  const profile = useQuery({ queryKey: ["institution-profile"], queryFn: getInstitutionProfile, enabled: isSuperAdmin });
  const members = useQuery({ queryKey: ["dash-members-total"], queryFn: () => getMembers({ pageSize: 1 }), enabled: isSuperAdmin });
  const batches = useQuery({ queryKey: ["dash-batches"], queryFn: getBatches, enabled: isSuperAdmin });
  const communities = useQuery({ queryKey: ["setup-communities"], queryFn: getCommunities, enabled: isSuperAdmin });
  const events = useQuery({ queryKey: ["dash-events"], queryFn: () => getEvents(1, 1), enabled: isSuperAdmin });

  if (!isSuperAdmin) return null;

  if (!profile.data || !members.data || !batches.data || !communities.data) return null;
  if (!profile.data.disabledFeatures?.includes("Events") && !events.data) return null;

  const institution = profile.data;
  const isCommunity = institution?.organizationType === "Community";
  const cohortLabel = institution?.cohortLabel?.trim() || "Batch";

  const brandingDone = !!institution && (!!institution.logoUrl || (!!institution.primaryColorHex && institution.primaryColorHex.toLowerCase() !== DEFAULT_PRIMARY_COLOR));
  const payoutsDone = institution?.payoutStatus === "Pending" || institution?.payoutStatus === "Approved";
  const groupsDone = isCommunity ? (communities.data?.length ?? 0) > 0 : (batches.data?.length ?? 0) > 0;

  const eventsEnabled = !institution?.disabledFeatures?.includes("Events");

  const items: ChecklistItem[] = [
    {
      id: "branding",
      title: "Add your logo and colors",
      description: "Right now members see the platform default instead of your identity.",
      done: brandingDone,
      actionLabel: "Add your logo",
      onAction: () => router.push("/settings"),
    },
    {
      id: "payouts",
      title: "Set up payouts",
      description: "Dues and contributions can't settle to your bank account until this is done.",
      done: payoutsDone,
      actionLabel: "Add bank details",
      onAction: () => router.push("/settings"),
    },
    isCommunity
      ? {
          id: "groups",
          title: "Create your first community",
          description: "Communities are how your members organize into groups.",
          done: groupsDone,
          actionLabel: "Create a community",
          onAction: () => router.push("/communities"),
        }
      : {
          id: "groups",
          title: `Create your first ${cohortLabel.toLowerCase()}`,
          description: "Group members by graduation year so you can target announcements and events.",
          done: groupsDone,
          actionLabel: `Create a ${cohortLabel.toLowerCase()}`,
          onAction: () => router.push("/batches"),
        },
    {
      id: "members",
      title: "Add your members",
      description: "Import a list or add people one by one. Nothing else works without them.",
      done: (members.data?.totalCount ?? 0) > 0,
      actionLabel: "Add members",
      onAction: () => router.push("/members"),
    },
    ...(eventsEnabled
      ? [{
          id: "event",
          title: "Post your first event",
          description: "Give members a reason to log in and RSVP.",
          done: (events.data?.totalCount ?? 0) > 0,
          actionLabel: "Create an event",
          onAction: () => router.push("/events"),
        }]
      : []),
  ];

  const loading = profile.isLoading || members.isLoading || batches.isLoading || communities.isLoading || events.isLoading;

  return <GetStartedChecklist items={items} loading={loading} storageKey={`institution-get-started:${user?.id ?? "anon"}`} />;
}
