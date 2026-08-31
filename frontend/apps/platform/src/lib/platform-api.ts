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
  logoUrl?: string | null;
  status: string;
  memberCount: number;
  onboardedAt: string;
  platformFeePercentage: number;
  revenue: number;
  memberPortalUrl: string;
  institutionPortalUrl: string;
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
  /** "ApprovedOnly" (default) — any approved member is active regardless of dues. "DuesRequired" — a member must also have paid dues. */
  memberActivePolicy: "ApprovedOnly" | "DuesRequired";
  disabledFeatures: string[];
  landingPageStories: LandingPageStory[];
  newsBanner: NewsBanner | null;
  /** Overrides the Member Portal landing page's hero photo(s), shown as a carousel — falls back to generic stock art when empty. */
  heroImageUrls: string[];
  /** Overrides the short headline overlaid on the hero photo. */
  heroHeadline?: string | null;
  status: string;
  memberCount: number;
  onboardedAt: string;
  trialEndsAt?: string | null;
  platformFeePercentage: number;
  paystackSubaccountCode?: string | null;
  settlementBankCode?: string | null;
  settlementBankName?: string | null;
  settlementAccountNumber?: string | null;
  settlementAccountName?: string | null;
  revenue: number;
  memberPortalUrl: string;
  institutionPortalUrl: string;
}

export interface CreateInstitutionRequest {
  name: string;
  slug: string;
  contactName: string;
  contactEmail: string;
  memberActivePolicy?: "ApprovedOnly" | "DuesRequired";
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

export interface BaseDomains {
  memberBaseDomain: string;
  adminBaseDomain: string;
}

export async function getBaseDomains() {
  const res = await platformClient.get<ApiResponse<BaseDomains>>("/institutions/base-domains");
  return res.data.data!;
}

export interface BankOption {
  name: string;
  code: string;
}

/** type: "ghipss" for real banks, "mobile_money" for mobile money providers — both from Paystack directly. */
export async function getBanks(type: "ghipss" | "mobile_money") {
  const res = await platformClient.get<ApiResponse<BankOption[]>>("/institutions/banks", { params: { type } });
  return res.data.data ?? [];
}

export interface ResolvedAccount {
  accountNumber: string;
  accountName: string;
}

export async function resolveAccount(accountNumber: string, bankCode: string) {
  const res = await platformClient.get<ApiResponse<ResolvedAccount>>("/institutions/resolve-account", {
    params: { accountNumber, bankCode },
  });
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

export async function updateInstitutionMemberPolicy(id: string, memberActivePolicy: "ApprovedOnly" | "DuesRequired") {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/member-policy`, { memberActivePolicy });
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

/** One settlement window's figure — an estimate from confirmed transactions, not a Paystack-confirmed settlement. Same shape and formula institutions see on their own side. */
export interface PayoutWindow {
  date: string;
  amount: number;
  transactionCount: number;
}

export interface InstitutionPayoutForecast {
  institutionId: string;
  institutionName: string;
  payoutsConfigured: boolean;
  lastPayout: PayoutWindow;
  nextPayout: PayoutWindow;
}

export interface PlatformPayoutForecast {
  totals: { lastPayout: PayoutWindow; nextPayout: PayoutWindow };
  institutions: InstitutionPayoutForecast[];
}

/** SuperAdmin/Billing only — estimated last and next Paystack settlement, totaled and broken out per institution. */
export async function getPayoutForecast(): Promise<PlatformPayoutForecast> {
  const res = await platformClient.get<ApiResponse<PlatformPayoutForecast>>("/payouts/forecast");
  return res.data.data!;
}

/** One payment, normalized across both payment sources — every status, not just Successful, so support staff can see the full picture. */
export interface PlatformPayment {
  id: string;
  source: "Contribution" | "StoreOrder";
  institutionId: string;
  payerName?: string | null;
  payerEmail?: string | null;
  description: string;
  amount: number;
  status: string;
  paymentMethod: string;
  transactionRef?: string | null;
  createdAt: string;
  confirmedAt?: string | null;
  platformFeeAmount: number;
  gatewayFeeAmount: number;
}

export async function getInstitutionPayments(id: string, page = 1, pageSize = 20, status?: string, source?: string) {
  const res = await platformClient.get<ApiResponse<PagedResult<PlatformPayment>>>(`/institutions/${id}/payments`, {
    params: { page, pageSize, status: status || undefined, source: source || undefined },
  });
  return res.data.data!;
}

/** One line item within a StoreOrder-sourced payment's detail view. */
export interface PaymentDetailItem {
  productName: string;
  variantOptions?: Record<string, string> | null;
  quantity: number;
  unitPrice: number;
}

/** Full detail for one payment (Contribution or StoreOrder) — fee breakdown, gateway channel/response, and line items where applicable. */
export interface PaymentDetail {
  id: string;
  source: "Contribution" | "StoreOrder";
  institutionId: string;
  payerName?: string | null;
  payerEmail?: string | null;
  memberNumber?: string | null;
  campaignTitle?: string | null;
  items?: PaymentDetailItem[] | null;
  amount: number;
  platformFeeAmount: number;
  gatewayFeeAmount: number;
  transactionChargeAmount: number;
  grossChargeAmount: number;
  status: string;
  createdAt: string;
  confirmedAt?: string | null;
  paymentMethod: string;
  channel?: string | null;
  gatewayResponse?: string | null;
  transactionRef?: string | null;
}

export async function getPaymentDetail(institutionId: string, paymentId: string, source?: string) {
  const res = await platformClient.get<ApiResponse<PaymentDetail>>(`/institutions/${institutionId}/payments/${paymentId}`, {
    params: { source: source || undefined },
  });
  return res.data.data!;
}

/** Every payment across every institution — used for platform-wide analytics. */
export async function getAllPayments(page = 1, pageSize = 20, status?: string, source?: string) {
  const res = await platformClient.get<ApiResponse<PagedResult<PlatformPayment>>>("/dashboard/payments", {
    params: { page, pageSize, status: status || undefined, source: source || undefined },
  });
  return res.data.data!;
}

// ─── Institution admins ─────────────────────────────────────────────────────

export interface InstitutionStaffMember {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  isDisabled: boolean;
  lastLoginAt?: string;
  createdAt: string;
}

export async function getInstitutionStaff(id: string) {
  const res = await platformClient.get<ApiResponse<InstitutionStaffMember[]>>(`/institutions/${id}/admins`);
  return res.data.data ?? [];
}

export async function inviteInstitutionStaff(id: string, req: { firstName: string; lastName: string; email: string; role: string }) {
  const res = await platformClient.post<ApiResponse<InstitutionStaffMember>>(`/institutions/${id}/admins`, req);
  return res.data.data!;
}

export async function setInstitutionStaffDisabled(id: string, staffId: string, isDisabled: boolean) {
  const res = await platformClient.patch<ApiResponse<InstitutionStaffMember>>(`/institutions/${id}/admins/${staffId}/disabled?isDisabled=${isDisabled}`);
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

export async function updateInstitutionLandingContent(
  id: string,
  landingPageStories: LandingPageStory[],
  newsBanner: NewsBanner | null,
  heroImageUrls: string[],
  heroHeadline?: string,
) {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/landing-content`, { landingPageStories, newsBanner, heroImageUrls, heroHeadline });
  return res.data.data!;
}

// ─── Uploads ─────────────────────────────────────────────────────────────

/** Upload a logo/icon image (max 5MB) and get back its public URL — used instead of hand-pasting a hosted URL. Pass institutionSlug when the image belongs to one institution (e.g. editing its branding), so it's filed under that institution's own storage subfolder. */
export async function uploadPlatformImage(file: File, institutionSlug?: string): Promise<string> {
  const formData = new FormData();
  formData.append("file", file);
  if (institutionSlug) formData.append("institutionSlug", institutionSlug);
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
  revenue: number;
  growthLast6Months: number[];
  growthMonthLabels: string[];
}

export async function getDashboardSummary() {
  const res = await platformClient.get<ApiResponse<DashboardSummary>>("/dashboard/summary");
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

// ── In-app Notifications (platform staff) ───────────────────────────────────
// Currently only raised when an institution opens a support ticket.

export interface PlatformNotificationItem {
  id: string;
  title: string;
  body: string;
  type: string;
  isRead: boolean;
  readAt?: string | null;
  relatedEntityId?: string | null;
  relatedEntityType?: string | null;
  actionUrl?: string | null;
  createdAt: string;
}

export async function getNotifications(page = 1, pageSize = 20): Promise<PagedResult<PlatformNotificationItem>> {
  const res = await platformClient.get<ApiResponse<PagedResult<PlatformNotificationItem>>>("/notifications", { params: { page, pageSize } });
  return res.data.data!;
}

export async function getUnreadNotificationCount(): Promise<number> {
  const res = await platformClient.get<ApiResponse<number>>("/notifications/unread-count");
  return res.data.data ?? 0;
}

export async function markNotificationRead(id: string): Promise<void> {
  await platformClient.put(`/notifications/${id}/read`);
}

export async function markAllNotificationsRead(): Promise<void> {
  await platformClient.put("/notifications/read-all");
}

// ─── Onboarding leads ────────────────────────────────────────────────────

export interface OnboardingLead {
  id: string;
  institutionName: string;
  contactName: string;
  contactEmail: string;
  contactPhone?: string;
  country?: string;
  estimatedMemberCount?: string;
  message?: string;
  status: "New" | "Contacted" | "Approved" | "Rejected";
  assigneeStaffId?: string;
  assigneeName?: string;
  internalNote?: string;
  approvedInstitutionId?: string;
  ageHours: number;
}

export async function getOnboardingLeads(status?: string) {
  const res = await platformClient.get<ApiResponse<OnboardingLead[]>>("/onboardingleads", { params: { status } });
  return res.data.data!;
}

export async function getOnboardingLead(id: string) {
  const res = await platformClient.get<ApiResponse<OnboardingLead>>(`/onboardingleads/${id}`);
  return res.data.data!;
}

export async function updateOnboardingLeadStatus(id: string, req: { status: string; approvedInstitutionId?: string }) {
  const res = await platformClient.patch<ApiResponse<OnboardingLead>>(`/onboardingleads/${id}/status`, req);
  return res.data.data!;
}

export async function addOnboardingLeadNote(id: string, note: string) {
  const res = await platformClient.post<ApiResponse<OnboardingLead>>(`/onboardingleads/${id}/notes`, { note });
  return res.data.data!;
}

export type { AuthTokens };
