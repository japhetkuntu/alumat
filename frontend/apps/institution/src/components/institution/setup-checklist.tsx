"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { GetStartedChecklist, type ChecklistItem } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { getBatches, getCommunities, getEvents, getInstitutionActivation, getInstitutionProfile, getNewsPosts } from "@/lib/institution-api";

/**
 * The setup steps a new institution's SuperAdmin needs to finish, each derived from real
 * data so it ticks itself off. Branding, payouts, members, first payment and team activity
 * come from the platform's activation evaluation (Institution.Api me/activation), so this
 * list agrees with the platform's scorecard and the weekly setup reminders. Branding and
 * payouts are SuperAdmin-only settings, so scoped admins never see this.
 */
export function InstitutionSetupChecklist() {
  const { user } = useAuth();
  const router = useRouter();
  const isSuperAdmin = user?.role === "SuperAdmin";

  const profile = useQuery({ queryKey: ["institution-profile"], queryFn: getInstitutionProfile, enabled: isSuperAdmin });
  // Nothing is requested for a feature that's switched off (its API answers 403), and the
  // list below only offers steps for what this institution actually uses.
  const profileReady = isSuperAdmin && !!profile.data;
  const featureOn = (key: string) => profileReady && !profile.data!.disabledFeatures?.includes(key);
  const isCommunityOrg = profile.data?.organizationType === "Community";
  const newsEnabled = featureOn("News");
  const eventsOn = featureOn("Events");
  const communitiesOn = featureOn("Communities") && isCommunityOrg;
  const batchesOn = profileReady && !isCommunityOrg;
  const moneyOn = featureOn("Contributions") || featureOn("Store") || featureOn("Services");
  const news = useQuery({ queryKey: ["setup-published-news"], queryFn: () => getNewsPosts(1, 1, undefined, "Published"), enabled: newsEnabled });
  const activation = useQuery({ queryKey: ["institution-activation"], queryFn: getInstitutionActivation, enabled: isSuperAdmin });
  const batches = useQuery({ queryKey: ["dash-batches"], queryFn: getBatches, enabled: batchesOn });
  const communities = useQuery({ queryKey: ["setup-communities"], queryFn: getCommunities, enabled: communitiesOn });
  const events = useQuery({ queryKey: ["dash-events"], queryFn: () => getEvents(1, 1), enabled: eventsOn });

  if (!isSuperAdmin) return null;
  // Activation failing (e.g. mid-deploy) degrades to the profile-derived steps rather than hiding the whole list.
  if (!profile.data) return null;

  const institution = profile.data;
  const isCommunity = institution.organizationType === "Community";
  const cohortLabel = institution.cohortLabel?.trim() || "Batch";
  const hasActivation = !!activation.data;
  const criterion = (key: string) => activation.data?.criteria.find((c) => c.key === key);
  const branding = criterion("branding");
  const payouts = criterion("payouts");
  const members = criterion("members");
  const payments = criterion("payments");
  const staff = criterion("staff");

  const groupsDone = isCommunity ? (communities.data?.length ?? 0) > 0 : (batches.data?.length ?? 0) > 0;
  const payoutPending = institution.payoutStatus === "Pending";

  const items: ChecklistItem[] = [
    {
      id: "branding",
      title: "Brand your portal",
      description: `Add your logo, colours and a welcome headline. You can add real community photos whenever you have them.${branding ? ` ${branding.detail}.` : ""}`,
      done: hasActivation ? !!branding?.met : !!institution.logoUrl,
      actionLabel: "Open settings",
      onAction: () => router.push("/settings"),
    },
    ...(!moneyOn ? [] : [{
      id: "payouts",
      title: "Set up payouts",
      description: payoutPending
        ? "Your bank details are with the platform team for review. Online payments go live once they're approved."
        : "Dues and contributions can't settle to your bank account until this is done.",
      done: hasActivation ? !!payouts?.met : institution.payoutStatus === "Approved",
      actionLabel: payoutPending ? "View status" : "Add bank details",
      onAction: () => router.push("/settings"),
    }]),
    ...((isCommunity ? communities.isSuccess : batches.isSuccess) ? [isCommunity
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
        }] : []),
    ...(newsEnabled && news.isSuccess ? [{
      id: "welcome",
      title: "Publish your first update",
      description: "A welcome message is a good first update. Choose institution-wide visibility if you want it to appear on your public site.",
      done: (news.data?.totalCount ?? 0) > 0,
      actionLabel: "Open news",
      onAction: () => router.push("/news"),
    }] : []),
    ...(hasActivation ? [{
      id: "members",
      title: `Get ${activation.data!.minMembers.toLocaleString()} members on board`,
      description: `Import your roster and get at least 30% of members signed in. Now: ${members?.detail ?? "no members yet"}.`,
      done: !!members?.met,
      actionLabel: "Go to members",
      onAction: () => router.push("/members"),
    },
    {
      id: "payments",
      title: "Take your first online payment",
      description: `Launch a dues or fundraising campaign and share it with members. Now: ${payments?.detail ?? "none yet"}.`,
      done: !!payments?.met,
      actionLabel: "Create a campaign",
      onAction: () => router.push("/campaigns"),
    },
    {
      id: "staff",
      title: "Get your team using the portal weekly",
      description: `At least 2 staff signing in each week, three weeks running. Now: ${staff?.detail ?? "no activity yet"}.`,
      done: !!staff?.met,
      actionLabel: "Invite staff",
      onAction: () => router.push("/staff"),
    }] : []),
    ...(eventsOn && events.isSuccess
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

  const loading = profile.isLoading;

  return <GetStartedChecklist items={items.filter(item => item.id !== "payments" || !institution.disabledFeatures?.includes("Contributions"))} loading={loading} storageKey={`institution-get-started:${user?.id ?? "anon"}`} />;
}
