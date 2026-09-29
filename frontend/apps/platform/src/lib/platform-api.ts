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

// ─── Platform-wide settings ──────────────────────────────────────────────

export interface PlatformSettings {
  blockOverdueCampaignPayments: boolean;
}

export async function getPlatformSettings(): Promise<PlatformSettings> {
  const res = await platformClient.get<ApiResponse<PlatformSettings>>("/platform-settings");
  return res.data.data!;
}

export async function updatePlatformSettings(settings: PlatformSettings): Promise<PlatformSettings> {
  const res = await platformClient.patch<ApiResponse<PlatformSettings>>("/platform-settings", settings);
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
  /** Off by default — a self-registered member lands in "Pending" and an admin must approve them. On: they're created "Active" immediately, no approval step. */
  autoApproveMembers: boolean;
  disabledFeatures: string[];
  landingPageStories: LandingPageStory[];
  newsBanner: NewsBanner | null;
  /** Overrides the Member Portal landing page's hero photo(s), shown as a carousel — falls back to generic stock art when empty. */
  heroImageUrls: string[];
  /** Overrides the short headline overlaid on the hero photo. */
  heroHeadline?: string | null;
  /** "Alumni" (default) — year-based cohorts via Batches. "Community" — no graduation years; members organize via Communities instead. */
  organizationType: "Alumni" | "Community";
  /** Alumni-only relabeling of "Batch" (e.g. "Class", "Cohort"). Null falls back to "Batch". */
  cohortLabel?: string | null;
  /** Plural form of cohortLabel (e.g. "Classes"). Null falls back to "Batches". */
  cohortLabelPlural?: string | null;
  status: string;
  memberCount: number;
  onboardedAt: string;
  trialEndsAt?: string | null;
  platformFeePercentage: number;
  /** Optional tiered pricing — above this amount, platformFeeFlatAmount replaces the percentage. Set together or not at all. */
  platformFeeFlatThreshold?: number | null;
  platformFeeFlatAmount?: number | null;
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
  /** "Alumni" (default) or "Community" — see Institution.OrganizationType. Can be changed later via updateInstitutionOrganizationType. */
  organizationType?: "Alumni" | "Community";
  /** Trial length in days; the server defaults to 14. */
  trialDays?: number;
  portalName?: string;
  supportEmail?: string;
  primaryColorHex?: string;
  secondaryColorHex?: string;
  platformFeePercentage?: number;
  platformFeeFlatThreshold?: number;
  platformFeeFlatAmount?: number;
  settlementBankCode?: string;
  settlementBankName?: string;
  settlementAccountNumber?: string;
  settlementAccountName?: string;
  adminFirstName: string;
  adminLastName: string;
  adminEmail: string;
  /** Optional: when both are set, one Batch per year in this range is auto-created (name defaults to the year; the institution can rename any of them later). */
  batchStartYear?: number;
  batchEndYear?: number;
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

export async function updateInstitutionName(id: string, name: string) {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/name`, { name });
  return res.data.data!;
}

export async function updateInstitutionSlug(id: string, slug: string) {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/institution-slug`, { slug });
  return res.data.data!;
}

export async function deleteInstitution(id: string) {
  const res = await platformClient.delete<ApiResponse<{ id: string }>>(`/institutions/${id}`);
  return res.data.data!;
}

export async function updateInstitutionMemberPolicy(id: string, memberActivePolicy: "ApprovedOnly" | "DuesRequired") {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/member-policy`, { memberActivePolicy });
  return res.data.data!;
}

export async function updateInstitutionAutoApproveMembers(id: string, autoApproveMembers: boolean) {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/auto-approve-members`, { autoApproveMembers });
  return res.data.data!;
}

/** Alumni-vs-Community organization type + Alumni-only cohort label wording. */
export async function updateInstitutionOrganizationType(
  id: string, organizationType: "Alumni" | "Community", cohortLabel?: string | null, cohortLabelPlural?: string | null,
) {
  const res = await platformClient.patch<ApiResponse<InstitutionDetail>>(`/institutions/${id}/organization-type`, { organizationType, cohortLabel, cohortLabelPlural });
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
  /** Optional tiered pricing — above this amount, platformFeeFlatAmount replaces the percentage. Set together or not at all. */
  platformFeeFlatThreshold?: number | null;
  platformFeeFlatAmount?: number | null;
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
  organizationType: "Alumni" | "Community";
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

/** A batch whose payout setup (see Institution.Api's BatchesController.SubmitPayoutSetup) is awaiting review. */
export interface PendingBatchPayout {
  batchId: string;
  batchName: string;
  year: number;
  institutionId: string;
  institutionName: string;
  useInstitutionAccount: boolean;
  settlementBankName?: string | null;
  settlementAccountNumber?: string | null;
  settlementAccountName?: string | null;
  submittedAt: string;
}

export async function getPendingBatchPayouts(): Promise<PendingBatchPayout[]> {
  const res = await platformClient.get<ApiResponse<PendingBatchPayout[]>>("/batch-payouts/pending");
  return res.data.data ?? [];
}

export async function approveBatchPayout(batchId: string): Promise<void> {
  await platformClient.put(`/batch-payouts/${batchId}/approve`);
}

export async function rejectBatchPayout(batchId: string, notes?: string): Promise<void> {
  await platformClient.put(`/batch-payouts/${batchId}/reject`, { notes });
}

/** An institution whose own payout setup (see Institution.Api's InstitutionController.SubmitPayoutSetup) is awaiting review. */
export interface PendingInstitutionPayout {
  institutionId: string;
  institutionName: string;
  settlementBankName?: string | null;
  settlementAccountNumber?: string | null;
  settlementAccountName?: string | null;
  submittedAt: string;
}

export async function getPendingInstitutionPayouts(): Promise<PendingInstitutionPayout[]> {
  const res = await platformClient.get<ApiResponse<PendingInstitutionPayout[]>>("/institution-payouts/pending");
  return res.data.data ?? [];
}

export async function approveInstitutionPayout(institutionId: string): Promise<void> {
  await platformClient.put(`/institution-payouts/${institutionId}/approve`);
}

export async function rejectInstitutionPayout(institutionId: string, notes?: string): Promise<void> {
  await platformClient.put(`/institution-payouts/${institutionId}/reject`, { notes });
}

/** One payment, normalized across both payment sources — every status, not just Successful, so support staff can see the full picture. */
export interface PlatformPayment {
  id: string;
  source: "Contribution" | "StoreOrder" | "ServiceRequest";
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
  source: "Contribution" | "StoreOrder" | "ServiceRequest";
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

export interface PlatformRevenueMonth { year: number; month: number; contributions: number; store: number; services: number }
export interface PlatformRevenueTrend {
  /** The latest calendar months, oldest first, current month last. Empty months are included as zeros. */
  months: PlatformRevenueMonth[];
  /** How many payments (all time, every institution) sit in each status. */
  statusCounts: Record<string, number>;
}

/** Paid revenue by month and source across every institution, totalled on the server so it is exact at any volume. */
export async function getRevenueTrend(months = 6): Promise<PlatformRevenueTrend> {
  const res = await platformClient.get<ApiResponse<PlatformRevenueTrend>>("/dashboard/revenue-trend", { params: { months } });
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

// ─── Broadcast (to one institution's own members) ──────────────────────────
// The platform-side equivalent of the institution admin's own Broadcast page —
// for Support/SuperAdmin outreach to a specific institution's members. Same
// engagement segments as the institution side; see PlatformBroadcastService.

export type PlatformEngagementSegment = "" | "Dormant" | "NoContributionsEver" | "NoContributionToActiveFundraiser";

export interface PlatformBroadcastFilter {
  institutionId: string;
  status?: string;
  departmentId?: string;
  graduationYearFrom?: number;
  graduationYearTo?: number;
  engagementSegment?: PlatformEngagementSegment;
}

export interface SendPlatformBroadcastBody extends PlatformBroadcastFilter {
  title?: string;
  message: string;
  channels: string[];
  imageUrl?: string;
}

export interface PlatformBroadcastResult {
  recipientCount: number;
  channels: string[];
}

export async function getPlatformBroadcastRecipientCount(filter: PlatformBroadcastFilter): Promise<number> {
  const res = await platformClient.get<ApiResponse<number>>("/broadcast/recipient-count", { params: filter });
  return res.data.data ?? 0;
}

export async function sendPlatformBroadcast(body: SendPlatformBroadcastBody): Promise<PlatformBroadcastResult> {
  const res = await platformClient.post<ApiResponse<PlatformBroadcastResult>>("/broadcast", body);
  return res.data.data!;
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
  suspendedCount: number;
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

export type NotificationChannel = "InApp" | "Email" | "Sms";

export interface AnnouncementItem {
  id: string;
  title: string;
  body: string;
  audience: string;
  sentAt: string;
  seenByAdmins: number;
  totalAdmins: number;
  channels: NotificationChannel[];
  emailSent: number;
  smsSent: number;
  smsSkippedNoPhone: number;
}

export interface StaffDirectoryEntry {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  hasPhone: boolean;
  institutionId: string;
  institutionName: string;
}

export async function getAnnouncements() {
  const res = await platformClient.get<ApiResponse<AnnouncementItem[]>>("/announcements");
  return res.data.data!;
}

export async function searchStaffDirectory(search?: string, institutionId?: string) {
  const res = await platformClient.get<ApiResponse<StaffDirectoryEntry[]>>("/announcements/staff-directory", {
    params: { search: search || undefined, institutionId: institutionId || undefined },
  });
  return res.data.data ?? [];
}

export async function sendAnnouncement(req: {
  title: string;
  body: string;
  channels: NotificationChannel[];
  recipientStaffIds?: string[];
  institutionId?: string;
}) {
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

// ── Cross-institution members ────────────────────────────────────────────────

export interface PlatformMemberItem {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  institutionId: string;
  institutionName: string;
  organizationType: "Alumni" | "Community";
  graduationYear: number;
  status: string;
  lastLoginAt?: string | null;
  /** Logged in within the last 7 days. */
  isActive: boolean;
  createdAt: string;
}

export async function getPlatformMembers(params: {
  page?: number; pageSize?: number; search?: string; institutionId?: string; status?: string; activeOnly?: boolean;
}) {
  const res = await platformClient.get<ApiResponse<PagedResult<PlatformMemberItem>>>("/members", { params });
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
  organizationType?: string;
  contactRole?: string;
  primaryGoals: string[];
  currentMemberManagement?: string;
  dataImportStatus?: string;
  preferredContactChannel?: string;
  preferredContactTime?: string;
  timeZone?: string;
  website?: string;
  message?: string;
  status: OnboardingLeadStatus;
  assigneeStaffId?: string;
  assigneeName?: string;
  internalNote?: string;
  approvedInstitutionId?: string;
  ageHours: number;
  agreementVersion?: string;
  agreementAcceptedAt?: string;
  agreementAcceptedByName?: string;
  agreementAcceptedByTitle?: string;
  agreementAcceptedIp?: string;
  source?: string;
  createdAt?: string;
  contactedAt?: string;
  demoBookedAt?: string;
  trialStartedAt?: string;
  approvedAt?: string;
  nextFollowUpAt?: string | null;
  institutionTrialEndsAt?: string | null;
}

export type OnboardingLeadStatus = "New" | "Contacted" | "DemoBooked" | "Trial" | "Approved" | "Rejected";

export interface CreateStaffOnboardingLeadRequest {
  institutionName: string;
  contactName: string;
  contactEmail?: string;
  contactPhone?: string;
  contactRole?: string;
  organizationType?: string;
  estimatedMemberCount?: string;
  source: string;
  status?: OnboardingLeadStatus;
  note?: string;
  nextFollowUpAt?: string | null;
}

export interface UpdateOnboardingLeadRequest {
  institutionName: string;
  contactName: string;
  contactEmail?: string | null;
  contactPhone?: string | null;
  contactRole?: string | null;
  organizationType?: string | null;
  estimatedMemberCount?: string | null;
  source?: string | null;
  assigneeStaffId?: string | null;
  nextFollowUpAt?: string | null;
}

export interface ImportOnboardingLeadsResult {
  created: number;
  skipped: { row: number; institutionName: string; reason: string }[];
}

export interface LeadAssignee {
  id: string;
  name: string;
  role: string;
}

export async function updateOnboardingLead(id: string, req: UpdateOnboardingLeadRequest) {
  const res = await platformClient.put<ApiResponse<OnboardingLead>>(`/onboardingleads/${id}`, req);
  return res.data.data!;
}

export async function importOnboardingLeads(rows: CreateStaffOnboardingLeadRequest[]) {
  const res = await platformClient.post<ApiResponse<ImportOnboardingLeadsResult>>("/onboardingleads/import", { rows });
  return res.data.data!;
}

export async function getLeadAssignees() {
  const res = await platformClient.get<ApiResponse<LeadAssignee[]>>("/onboardingleads/assignees");
  return res.data.data!;
}

export async function createOnboardingLead(req: CreateStaffOnboardingLeadRequest) {
  const res = await platformClient.post<ApiResponse<OnboardingLead>>("/onboardingleads", req);
  return res.data.data!;
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

// ─── Activation ──────────────────────────────────────────────────────────

export interface ActivationCriterion {
  key: "branding" | "payouts" | "members" | "payments" | "staff";
  label: string;
  met: boolean;
  detail: string;
}

export interface ActivationScorecardItem {
  institutionId: string;
  name: string;
  slug: string;
  onboardedAt: string;
  activatedAt?: string | null;
  daysLive: number;
  criteria: ActivationCriterion[];
  metCount: number;
  nextStep?: string | null;
  isStalled: boolean;
  isActivated: boolean;
  isOverdue: boolean;
  minMembers: number;
  trialEndsAt?: string | null;
  setupNudgesEnabled: boolean;
}

export interface ActivationMilestone {
  date: string;
  liveTarget?: number | null;
  activatedTarget?: number | null;
}

export interface MilestoneProgress extends ActivationMilestone {
  liveActual: number;
  activatedActual: number;
  status: "met" | "missed" | "open";
}

export interface ActivationScorecard {
  liveCount: number;
  activatedCount: number;
  stalledCount: number;
  overdueCount: number;
  targetCount?: number | null;
  targetDate?: string | null;
  milestones: MilestoneProgress[];
  items: ActivationScorecardItem[];
}

export interface FunnelStage {
  key: string;
  label: string;
  count: number;
}

export interface FunnelWeek {
  weekStart: string;
  leads: number;
  contacted: number;
  live: number;
}

export interface ActivationFunnel {
  stages: FunnelStage[];
  weeks: FunnelWeek[];
}

export async function getActivationScorecard() {
  const res = await platformClient.get<ApiResponse<ActivationScorecard>>("/activation/scorecard");
  return res.data.data!;
}

export async function getActivationFunnel(weeks = 9) {
  const res = await platformClient.get<ApiResponse<ActivationFunnel>>("/activation/funnel", { params: { weeks } });
  return res.data.data!;
}

export async function updateActivationTarget(req: {
  targetCount: number | null;
  targetDate: string | null;
  milestones: ActivationMilestone[];
}) {
  const res = await platformClient.put<ApiResponse<ActivationScorecard>>("/activation/target", req);
  return res.data.data!;
}

export async function getInstitutionActivation(institutionId: string) {
  const res = await platformClient.get<ApiResponse<ActivationScorecardItem>>(`/activation/institutions/${institutionId}`);
  return res.data.data!;
}

export async function updateInstitutionActivationSettings(
  institutionId: string,
  req: { activationMinMembers: number | null; setupNudgesEnabled: boolean },
) {
  const res = await platformClient.put<ApiResponse<ActivationScorecardItem>>(`/activation/institutions/${institutionId}/settings`, req);
  return res.data.data!;
}

export type { AuthTokens };

// ─── Activation work: targets and tasks ───────────────────────────────────────
// A target is a time-boxed goal with one owner; tasks sit under it, each with one assignee. Progress is measured
// from real platform data. Super Admins create targets and assign work; everyone can work their own tasks.

export type TargetMetric = "LiveInstitutions" | "ActivatedInstitutions" | "TotalMembers" | "OnboardingLeads" | "PaymentVolume" | "Custom";
export type TargetStatus = "Active" | "Achieved" | "Missed" | "Cancelled";
export type TargetHealth = "Achieved" | "OnTrack" | "Behind" | "Missed" | "Cancelled";
export type WorkTaskStatus = "Todo" | "InProgress" | "Blocked" | "Done";
export type WorkTaskPriority = "Low" | "Normal" | "High";

export interface TargetProgress {
  current: number;
  baseline: number;
  goal: number;
  /** Where the target should be by today to finish on time. */
  expected: number;
  percent: number;
  health: TargetHealth;
}

export interface WorkTarget {
  id: string;
  title: string;
  description?: string | null;
  metric: TargetMetric;
  metricLabel: string;
  /** Counts what happened since the start, rather than reading a current level. */
  isFlow: boolean;
  goalValue: number;
  baselineValue: number;
  startDate: string;
  dueDate: string;
  ownerId: string;
  ownerName: string;
  status: TargetStatus;
  manualValue?: number | null;
  closedAt?: string | null;
  progress: TargetProgress;
  openTasks: number;
  doneTasks: number;
  overdueTasks: number;
  createdAt: string;
}

export interface WorkTask {
  id: string;
  targetId: string;
  targetTitle: string;
  title: string;
  description?: string | null;
  assigneeId: string;
  assigneeName: string;
  dueDate?: string | null;
  priority: WorkTaskPriority;
  status: WorkTaskStatus;
  blockedReason?: string | null;
  institutionId?: string | null;
  institutionName?: string | null;
  leadId?: string | null;
  leadName?: string | null;
  createdById: string;
  createdByName: string;
  completedAt?: string | null;
  isOverdue: boolean;
  createdAt: string;
  canEdit: boolean;
  canUpdateStatus: boolean;
}

export interface WorkTaskNote { id: string; authorId: string; authorName: string; text: string; createdAt: string }

export const TARGET_METRICS: { value: TargetMetric; label: string; hint: string }[] = [
  { value: "LiveInstitutions", label: "Live institutions", hint: "Institutions that are live right now." },
  { value: "ActivatedInstitutions", label: "Activated institutions", hint: "Live institutions that have reached activation." },
  { value: "TotalMembers", label: "Active members", hint: "Active members across every institution right now." },
  { value: "OnboardingLeads", label: "Onboarding requests", hint: "Requests received from the start date onwards." },
  { value: "PaymentVolume", label: "Payments collected (GHS)", hint: "Successful payments collected from the start date onwards." },
  { value: "Custom", label: "Custom (updated by hand)", hint: "For a goal the platform can't measure. Someone updates the figure." },
];

export async function getWorkTargets(status?: TargetStatus) {
  const res = await platformClient.get<ApiResponse<WorkTarget[]>>("/work/targets", { params: { status } });
  return res.data.data!;
}

export async function getWorkTarget(id: string) {
  const res = await platformClient.get<ApiResponse<{ target: WorkTarget; tasks: WorkTask[] }>>(`/work/targets/${id}`);
  return res.data.data!;
}

export async function createWorkTarget(req: { title: string; description?: string; metric: TargetMetric; goalValue: number; dueDate: string; ownerId: string; manualValue?: number }) {
  const res = await platformClient.post<ApiResponse<WorkTarget>>("/work/targets", req);
  return res.data.data!;
}

export async function updateWorkTarget(id: string, req: { title: string; description?: string; goalValue: number; dueDate: string; ownerId: string; manualValue?: number }) {
  const res = await platformClient.put<ApiResponse<WorkTarget>>(`/work/targets/${id}`, req);
  return res.data.data!;
}

export async function cancelWorkTarget(id: string) {
  const res = await platformClient.post<ApiResponse<WorkTarget>>(`/work/targets/${id}/cancel`);
  return res.data.data!;
}

export async function getWorkTasks(params: { mine?: boolean; targetId?: string; status?: WorkTaskStatus; assigneeId?: string; institutionId?: string; leadId?: string } = {}) {
  const res = await platformClient.get<ApiResponse<WorkTask[]>>("/work/tasks", { params });
  return res.data.data!;
}

export async function getWorkTask(id: string) {
  const res = await platformClient.get<ApiResponse<{ task: WorkTask; notes: WorkTaskNote[] }>>(`/work/tasks/${id}`);
  return res.data.data!;
}

export interface WorkTaskInput {
  title: string;
  description?: string;
  assigneeId?: string;
  dueDate?: string | null;
  priority?: WorkTaskPriority;
  institutionId?: string | null;
  leadId?: string | null;
}

export async function createWorkTask(req: WorkTaskInput & { targetId: string }) {
  const res = await platformClient.post<ApiResponse<WorkTask>>("/work/tasks", req);
  return res.data.data!;
}

export async function updateWorkTask(id: string, req: WorkTaskInput) {
  const res = await platformClient.put<ApiResponse<WorkTask>>(`/work/tasks/${id}`, req);
  return res.data.data!;
}

export async function updateWorkTaskStatus(id: string, req: { status: WorkTaskStatus; blockedReason?: string }) {
  const res = await platformClient.patch<ApiResponse<WorkTask>>(`/work/tasks/${id}/status`, req);
  return res.data.data!;
}

export async function deleteWorkTask(id: string) {
  await platformClient.delete(`/work/tasks/${id}`);
}

export async function addWorkTaskNote(id: string, text: string) {
  const res = await platformClient.post<ApiResponse<WorkTaskNote>>(`/work/tasks/${id}/notes`, { text });
  return res.data.data!;
}
