import { platformClient } from "./api-client";
import {
  ApiResponse,
  AuthData,
  AuthTokens,
  LoginRequest,
  PagedResult,
} from "@/types";

// ─── Auth ──────────────────────────────────────────────────────────────────

interface PlatformTokenResponse {
  user: { id: string; email: string; name: string; role: string };
  tokens: { accessToken: string; refreshToken: string; expiresIn: number };
}

export async function loginPlatformStaff(req: LoginRequest) {
  const res = await platformClient.post<ApiResponse<PlatformTokenResponse>>("/auth/login", req);
  return res.data.data!;
}

export async function getCurrentStaff() {
  const res = await platformClient.get<AuthData>("/auth/me");
  return res.data;
}

export async function changePlatformPassword(currentPassword: string, newPassword: string) {
  const res = await platformClient.put<ApiResponse<PlatformTokenResponse>>("/auth/changepassword", {
    currentPassword,
    newPassword,
  });
  return res.data.data!;
}

// ─── Institutions ────────────────────────────────────────────────────────

export interface InstitutionListItem {
  id: string;
  name: string;
  slug: string;
  customDomain?: string | null;
  contactName: string;
  contactEmail: string;
  plan: string;
  status: string;
  memberCount: number;
  memberLimit: number;
  onboardedAt: string;
  mrr: number;
  platformFeePercentage: number;
  revenue: number;
}

export interface InstitutionDetail {
  id: string;
  name: string;
  slug: string;
  customDomain?: string | null;
  portalName: string;
  tagline?: string | null;
  contactName: string;
  contactEmail: string;
  supportEmail?: string | null;
  logoUrl?: string | null;
  iconUrl?: string | null;
  primaryColorHex: string;
  secondaryColorHex?: string | null;
  institutionPortalTitle?: string | null;
  institutionAuthHeadline?: string | null;
  institutionAuthSubtext?: string | null;
  memberPortalTitle?: string | null;
  memberAuthHeadline?: string | null;
  memberAuthSubtext?: string | null;
  requireStudentId: boolean;
  disabledFeatures: string[];
  landingPageStories: LandingPageStory[];
  newsBanner: NewsBanner | null;
  plan: string;
  status: string;
  memberCount: number;
  memberLimit: number;
  storageUsedGb: number;
  storageLimitGb: number;
  onboardedAt: string;
  trialEndsAt?: string | null;
  mrr: number;
  platformFeePercentage: number;
  paystackSubaccountCode?: string | null;
  settlementBankCode?: string | null;
  settlementBankName?: string | null;
  settlementAccountNumber?: string | null;
  settlementAccountName?: string | null;
  revenue: number;
}

export interface CreateInstitutionRequest {
  name: string;
  slug: string;
  contactName: string;
  contactEmail: string;
  plan?: string;
  portalName?: string;
  supportEmail?: string;
  primaryColorHex?: string;
  secondaryColorHex?: string;
  platformFeePercentage?: number;
  settlementBankCode?: string;
  settlementBankName?: string;
  settlementAccountNumber?: string;
  settlementAccountName?: string;
  adminFirstName: string;
  adminLastName: string;
  adminEmail: string;
  adminPassword: string;
}

export async function getInstitutions(params: { page?: number; pageSize?: number; search?: string; status?: string }) {
  const res = await platformClient.get<ApiResponse<PagedResult<InstitutionListItem>>>("/institutions", { params });
  return res.data.data!;
}

export async function getInstitution(id: string) {
  const res = await platformClient.get<ApiResponse<InstitutionDetail>>(`/institutions/${id}`);
  return res.data.data!;
}

export async function checkSlugAvailability(slug: string) {
  const res = await platformClient.get<ApiResponse<{ slug: string; available: boolean }>>("/institutions/check-slug", {
    params: { slug },
  });
  return res.data.data!;
}

export async function createInstitution(req: CreateInstitutionRequest) {
  const res = await platformClient.post<ApiResponse<InstitutionDetail>>("/institutions", req);
  return res.data.data!;
}

export async function updateInstitutionStatus(id: string, status: string) {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/status`, { status });
  return res.data.data!;
}

export async function updateInstitutionPlan(id: string, plan: string, memberLimit?: number, storageLimitGb?: number) {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/plan`, {
    plan,
    memberLimit,
    storageLimitGb,
  });
  return res.data.data!;
}

export interface UpdateInstitutionBrandingRequest {
  portalName: string;
  tagline?: string;
  contactEmail: string;
  supportEmail?: string;
  logoUrl?: string;
  iconUrl?: string;
  primaryColorHex: string;
  secondaryColorHex?: string;
  institutionPortalTitle?: string;
  institutionAuthHeadline?: string;
  institutionAuthSubtext?: string;
  memberPortalTitle?: string;
  memberAuthHeadline?: string;
  memberAuthSubtext?: string;
  requireStudentId: boolean;
}

export async function updateInstitutionBranding(id: string, req: UpdateInstitutionBrandingRequest) {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/branding`, req);
  return res.data.data!;
}

// ─── Payments & payouts ────────────────────────────────────────────────────

export interface UpdateInstitutionPaymentsRequest {
  platformFeePercentage: number;
  settlementBankCode: string;
  settlementBankName: string;
  settlementAccountNumber: string;
  settlementAccountName: string;
}

export async function updateInstitutionPayments(id: string, req: UpdateInstitutionPaymentsRequest) {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/payments`, req);
  return res.data.data!;
}

export interface InstitutionRevenue {
  institutionId: string;
  grossCollected: number;
  platformFeeTotal: number;
  netToInstitution: number;
  confirmedPaymentCount: number;
}

export async function getInstitutionRevenue(id: string) {
  const res = await platformClient.get<ApiResponse<InstitutionRevenue>>(`/institutions/${id}/revenue`);
  return res.data.data!;
}

// ─── Feature catalog ────────────────────────────────────────────────────────

export interface FeatureCatalogItem {
  key: string;
  label: string;
  description: string;
}

/** The single source of truth for gateable feature keys — fetched from the backend
 * so a new feature key shows up here automatically without a frontend code change. */
export async function getFeatureCatalog() {
  const res = await platformClient.get<ApiResponse<FeatureCatalogItem[]>>("/features/catalog");
  return res.data.data!;
}

export async function updateInstitutionFeatures(id: string, disabledFeatures: string[]) {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/features`, { disabledFeatures });
  return res.data.data!;
}

// Landing page content — Stories and the news banner shown on the Member
// Portal's public landing page. Editable here AND by the institution's own
// admins (Institution Portal settings) — see backend InstitutionController.
export interface LandingPageStory {
  icon: string;
  eyebrow: string;
  scenario: string;
  description: string;
  imageUrl?: string | null;
}

export interface NewsBanner {
  enabled: boolean;
  text: string;
  linkText?: string | null;
  linkUrl?: string | null;
}

export const STORY_ICON_OPTIONS = [
  "Briefcase", "Users", "CreditCard", "BookOpen", "Globe", "Heart", "Trophy",
  "Bell", "GraduationCap", "Shield", "MapPin", "Zap", "Star", "Award",
] as const;

export async function updateInstitutionLandingContent(id: string, landingPageStories: LandingPageStory[], newsBanner: NewsBanner | null) {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/landing-content`, { landingPageStories, newsBanner });
  return res.data.data!;
}

// ─── Uploads ─────────────────────────────────────────────────────────────

/** Upload a logo/icon image (max 5MB) and get back its public URL — used instead of hand-pasting a hosted URL. */
export async function uploadPlatformImage(file: File): Promise<string> {
  const formData = new FormData();
  formData.append("file", file);
  // No explicit Content-Type — letting axios set it (with the multipart
  // boundary) from the FormData body itself; a hardcoded "multipart/form-data"
  // header here would drop the boundary parameter and break parsing server-side.
  const res = await platformClient.post<ApiResponse<{ url: string }>>("/uploads/image", formData);
  return res.data.data!.url;
}

// ─── Platform staff ──────────────────────────────────────────────────────

export interface PlatformStaffItem {
  id: string;
  name: string;
  email: string;
  role: string;
  team?: string | null;
  mfa: boolean;
  isDisabled: boolean;
  lastActiveAt?: string | null;
}

export async function getPlatformStaff(params: { page?: number; pageSize?: number; search?: string }) {
  const res = await platformClient.get<ApiResponse<PagedResult<PlatformStaffItem>>>("/staff", { params });
  return res.data.data!;
}

export async function createPlatformStaff(req: { name: string; email: string; password: string; role?: string; team?: string }) {
  const res = await platformClient.post<ApiResponse<PlatformStaffItem>>("/staff", req);
  return res.data.data!;
}

export async function updatePlatformStaff(id: string, req: { name: string; role?: string; team?: string; isDisabled: boolean }) {
  const res = await platformClient.patch<ApiResponse<PlatformStaffItem>>(`/staff/${id}`, req);
  return res.data.data!;
}

// ─── Dashboard ───────────────────────────────────────────────────────────

export interface DashboardSummary {
  totalInstitutions: number;
  activeCount: number;
  trialCount: number;
  totalMembers: number;
  newInstitutionsThisMonth: number;
  mrr: number;
  revenue: number;
  growthLast6Months: number[];
  growthMonthLabels: string[];
}

export async function getDashboardSummary() {
  const res = await platformClient.get<ApiResponse<DashboardSummary>>("/dashboard/summary");
  return res.data.data!;
}

// ─── Plans ─────────────────────────────────────────────────────────────────

export interface PlanItem {
  id: string;
  name: string;
  // The backend serializer drops null fields entirely, so these arrive as
  // undefined (not null) when unset — always check with `== null` / `?.`.
  price?: number | null;
  billingInterval: string;
  memberLimit?: number | null;
  storageLimitGb?: number | null;
  modules: string[];
  supportLevel: string;
  isMostUsed: boolean;
  subscriberCount: number;
}

export async function getPlans() {
  const res = await platformClient.get<ApiResponse<PlanItem[]>>("/plans");
  return res.data.data!;
}

export async function createPlan(req: {
  name: string;
  price?: number;
  billingInterval?: string;
  memberLimit?: number;
  storageLimitGb?: number;
  modules?: string[];
  supportLevel?: string;
  isMostUsed?: boolean;
}) {
  const res = await platformClient.post<ApiResponse<PlanItem>>("/plans", req);
  return res.data.data!;
}

// ─── Support cases ───────────────────────────────────────────────────────

export interface SupportCaseItem {
  id: string;
  subject: string;
  institutionId: string | null;
  institutionName: string | null;
  severity: string;
  status: string;
  assigneeStaffId: string | null;
  assigneeName: string | null;
  ageHours: number;
  requester: string;
  requesterEmail: string | null;
  message: string;
  internalNote: string | null;
}

export async function getSupportCases(status?: string) {
  const res = await platformClient.get<ApiResponse<SupportCaseItem[]>>("/support-cases", { params: { status } });
  return res.data.data!;
}

export async function createSupportCase(req: {
  institutionId?: string;
  subject: string;
  severity?: string;
  requester: string;
  requesterEmail?: string;
  message: string;
}) {
  const res = await platformClient.post<ApiResponse<SupportCaseItem>>("/support-cases", req);
  return res.data.data!;
}

export async function updateSupportCaseStatus(id: string, status: string) {
  const res = await platformClient.patch<ApiResponse<SupportCaseItem>>(`/support-cases/${id}/status`, { status });
  return res.data.data!;
}

export async function addSupportCaseNote(id: string, note: string) {
  const res = await platformClient.post<ApiResponse<SupportCaseItem>>(`/support-cases/${id}/notes`, { note });
  return res.data.data!;
}

// ─── Announcements ───────────────────────────────────────────────────────

export interface AnnouncementItem {
  id: string;
  title: string;
  body: string;
  audience: string;
  sentAt: string;
  seenByAdmins: number;
  totalAdmins: number;
}

export async function getAnnouncements() {
  const res = await platformClient.get<ApiResponse<AnnouncementItem[]>>("/announcements");
  return res.data.data!;
}

export async function sendAnnouncement(req: { title: string; body: string; audience?: string }) {
  const res = await platformClient.post<ApiResponse<AnnouncementItem>>("/announcements", req);
  return res.data.data!;
}

// ─── Audit log ───────────────────────────────────────────────────────────

export interface AuditLogEntryItem {
  id: string;
  actor: string;
  action: string;
  target: string;
  timestamp: string;
}

export async function getAuditLog(params: { page?: number; pageSize?: number; search?: string }) {
  const res = await platformClient.get<ApiResponse<PagedResult<AuditLogEntryItem>>>("/audit-log", { params });
  return res.data.data!;
}

export type { AuthTokens };
