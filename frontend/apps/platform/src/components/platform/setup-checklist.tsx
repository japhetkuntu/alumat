"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { GetStartedChecklist, type ChecklistItem } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { getAnnouncements, getInstitutions, getPlatformStaff } from "@/lib/platform-api";

/** Platform-level setup, SuperAdmin only (Support staff can't add institutions or staff). Each step ticks itself off from real data. */
export function PlatformSetupChecklist() {
  const { user, isSuperAdmin } = useAuth();
  const router = useRouter();

  const institutions = useQuery({ queryKey: ["get-started-institutions"], queryFn: () => getInstitutions({ page: 1, pageSize: 1 }), enabled: isSuperAdmin });
  const staff = useQuery({ queryKey: ["get-started-staff"], queryFn: () => getPlatformStaff({ page: 1, pageSize: 2 }), enabled: isSuperAdmin });
  const announcements = useQuery({ queryKey: ["get-started-announcements"], queryFn: getAnnouncements, enabled: isSuperAdmin });

  if (!isSuperAdmin) return null;

  if (!institutions.data || !staff.data || !announcements.data) return null;

  const items: ChecklistItem[] = [
    {
      id: "institution",
      title: "Add your first institution",
      description: "An institution gets its own branded portal and admin team.",
      done: (institutions.data?.totalCount ?? 0) > 0,
      actionLabel: "Add an institution",
      onAction: () => router.push("/institutions/new"),
    },
    {
      id: "staff",
      title: "Add a teammate",
      description: "A second platform admin means onboarding never waits on one person.",
      done: (staff.data?.totalCount ?? 0) > 1,
      actionLabel: "Add a staff member",
      onAction: () => router.push("/staff"),
    },
    {
      id: "announcement",
      title: "Send your first announcement",
      description: "Tell every institution admin about a change or a new feature.",
      done: (announcements.data?.length ?? 0) > 0,
      actionLabel: "Write an announcement",
      onAction: () => router.push("/announcements"),
    },
  ];

  const loading = institutions.isLoading || staff.isLoading || announcements.isLoading;

  return <GetStartedChecklist items={items} loading={loading} storageKey={`platform-get-started:${user?.id ?? "anon"}`} />;
}
