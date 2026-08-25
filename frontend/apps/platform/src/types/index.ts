// ─── Common / API ────────────────────────────────────────────────────────────

export interface ApiResponse<T> {
  message: string;
  code: number;
  subCode: string;
  data: T | null;
  errors?: ErrorResponse[] | null;
}

export interface ErrorResponse {
  field: string;
  errorMessage: string;
}

export interface PagedResult<T> {
  pageIndex: number;
  pageSize: number;
  count: number;
  totalCount: number;
  totalPages: number;
  lowerBoundSize: number;
  upperBoundSize: number;
  results: T[];
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export type PlatformStaffRole = "SuperAdmin" | "Support" | "Billing" | "Sales";

export interface AuthData {
  id: string;
  email: string;
  name: string;
  role: PlatformStaffRole;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export type InstitutionStatus = "Trial" | "Active" | "Suspended" | "Cancelled";

export interface Institution {
  id: string;
  name: string;
  slug: string; // subdomain slug, e.g. "greenfield" -> greenfield.yourplatform.com
  customDomain?: string;
  contactName: string;
  contactEmail: string;
  plan: string;
  status: InstitutionStatus;
  memberCount: number;
  memberLimit: number;
  storageUsedGb: number;
  storageLimitGb: number;
  onboardedAt: string;
  lastActivityAt: string;
  featureOverrides: { feature: string; planDefault: boolean; enabled: boolean; note?: string }[];
}

export interface PlatformStaffMember {
  id: string;
  name: string;
  email: string;
  team: string;
  role: string;
  status: "Active" | "Pending invite" | "Disabled";
  mfa: "Enforced" | "Not enrolled";
  lastActiveAt: string;
}

export interface SupportCase {
  id: string;
  subject: string;
  institution: string;
  severity: "High" | "Medium" | "Low";
  status: "New" | "Investigating" | "Waiting on Internal Team" | "Resolved";
  assignee: string;
  ageHours: number;
  requester: string;
  message: string;
  internalNote?: string;
}

export interface Announcement {
  id: string;
  title: string;
  body: string;
  audience: string;
  sentAt: string;
  seenByAdmins: number;
  totalAdmins: number;
}

export interface AuditEntry {
  id: string;
  actor: string;
  action: string;
  target: string;
  timestamp: string;
}
