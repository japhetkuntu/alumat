import type { ManageGroup } from "@/lib/member-api";

/** Short names for the privileges, for chips. The full wording comes from the server wherever a sentence is needed. */
export const PRIVILEGE_LABEL: Record<string, string> = {
  ManageMembers: "Members",
  MessageGroup: "Message",
  ManageEvents: "Events",
  PostNews: "News",
  PostJobs: "Jobs",
  ShareResources: "Links",
  ManageFundraisers: "Fundraisers",
  ViewGiving: "Giving",
  ViewReport: "Report",
};

export const groupHref = (g: Pick<ManageGroup, "scopeType" | "scopeId">) => `/manage/${g.scopeType === "Batch" ? "year-group" : "community"}/${encodeURIComponent(g.scopeId)}`;

export const scopeTypeFromSlug = (slug: string): ManageGroup["scopeType"] | null => (slug === "year-group" ? "Batch" : slug === "community" ? "Community" : null);
