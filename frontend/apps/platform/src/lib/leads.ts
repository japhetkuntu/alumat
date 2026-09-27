import { parseCsv } from "./csv";
import type { CreateStaffOnboardingLeadRequest, OnboardingLeadStatus } from "./platform-api";

type LeadField = keyof CreateStaffOnboardingLeadRequest;

/** Accepted column headings (lower-cased, punctuation stripped) for each lead field. */
const HEADER_ALIASES: Record<string, LeadField> = {
  institution: "institutionName",
  institutionname: "institutionName",
  association: "institutionName",
  school: "institutionName",
  name: "institutionName",
  contact: "contactName",
  contactname: "contactName",
  contactperson: "contactName",
  email: "contactEmail",
  contactemail: "contactEmail",
  phone: "contactPhone",
  phonenumber: "contactPhone",
  contactphone: "contactPhone",
  role: "contactRole",
  contactrole: "contactRole",
  position: "contactRole",
  type: "organizationType",
  organizationtype: "organizationType",
  members: "estimatedMemberCount",
  estimatedmembers: "estimatedMemberCount",
  estimatedmembercount: "estimatedMemberCount",
  source: "source",
  stage: "status",
  status: "status",
  note: "note",
  notes: "note",
  followup: "nextFollowUpAt",
  nextfollowup: "nextFollowUpAt",
  followupdate: "nextFollowUpAt",
};

const STAGE_ALIASES: Record<string, OnboardingLeadStatus> = {
  new: "New",
  contacted: "Contacted",
  demo: "DemoBooked",
  demobooked: "DemoBooked",
  trial: "Trial",
};

const normalise = (value: string) => value.toLowerCase().replace(/[^a-z]/g, "");

export interface ParsedLeadImport {
  rows: CreateStaffOnboardingLeadRequest[];
  /** Headings that didn't match any field — shown so staff know a column was ignored. */
  ignoredHeaders: string[];
  /** Set when the file can't be imported at all (e.g. no institution column). */
  error?: string;
}

/**
 * Turns an outreach spreadsheet (CSV) into lead rows. The server does the
 * real validation and de-duplication; this only maps columns and tidies
 * values. Unrecognised stages are passed through so the server can report them
 * against their row number.
 */
export function parseLeadCsv(text: string, defaultSource: string): ParsedLeadImport {
  const [header, ...body] = parseCsv(text);
  if (!header) return { rows: [], ignoredHeaders: [], error: "The file is empty." };

  const columns = header.map((h) => HEADER_ALIASES[normalise(h)]);
  const ignoredHeaders = header.filter((_, i) => !columns[i]).map((h) => h.trim()).filter(Boolean);
  if (!columns.includes("institutionName")) {
    return { rows: [], ignoredHeaders, error: 'No institution column found. Add a column headed "Institution".' };
  }

  const rows = body.map((cells) => {
    const row: CreateStaffOnboardingLeadRequest = { institutionName: "", contactName: "", source: defaultSource };
    columns.forEach((field, i) => {
      const value = (cells[i] ?? "").trim();
      if (!field || !value) return;
      if (field === "status") {
        row.status = STAGE_ALIASES[normalise(value)] ?? (value as OnboardingLeadStatus);
      } else if (field === "nextFollowUpAt") {
        const date = new Date(value);
        if (!Number.isNaN(date.getTime())) row.nextFollowUpAt = date.toISOString().slice(0, 10);
      } else {
        (row as unknown as Record<string, string>)[field] = value;
      }
    });
    return row;
  });

  return { rows, ignoredHeaders };
}

export type FollowUpState = "overdue" | "today" | "upcoming";

/** Where a lead's follow-up date sits relative to today (calendar days, local time). */
export function followUpState(nextFollowUpAt: string | null | undefined, now = new Date()): FollowUpState | null {
  if (!nextFollowUpAt) return null;
  const due = nextFollowUpAt.slice(0, 10);
  const today = [now.getFullYear(), String(now.getMonth() + 1).padStart(2, "0"), String(now.getDate()).padStart(2, "0")].join("-");
  if (due < today) return "overdue";
  if (due === today) return "today";
  return "upcoming";
}
