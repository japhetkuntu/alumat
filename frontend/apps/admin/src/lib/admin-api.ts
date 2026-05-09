import { adminClient } from "./api-client";
import type {
  ApiResponse,
  PagedResult,
  Member,
  Campaign,
  Contribution,
  PaystackDisbursementSummary,
  ReportSummary,
  Job,
  AlumniEvent,
  EventRegistration,
  NewsPost,
  ForumCategory,
  ForumThread,
  MentorProfile,
  MentorshipRequest,
  Resource,
  AdminUser,
  CreateAdminRequest,
  UpdateAdminRequest,
  Spotlight,
  NotificationItem,
} from "@/types";

function toFormData(data: object): FormData {
  const fd = new FormData();
  for (const [key, value] of Object.entries(data)) {
    if (value === undefined || value === null) continue;
    if (value instanceof File) {
      fd.append(key, value);
    } else if (Array.isArray(value)) {
      value.forEach((item: unknown) => {
        if (item instanceof File) fd.append(key, item);
        else fd.append(key, String(item));
      });
    } else {
      fd.append(key, String(value));
    }
  }
  return fd;
}

// ─── Auth / Profile ───────────────────────────────────────────────────────────

export interface AdminProfileResponse {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  graduationYear?: number;
}

export async function getAdminProfile(): Promise<AdminProfileResponse> {
  const res = await adminClient.get<ApiResponse<AdminProfileResponse>>("/auth/me");
  const profile = res.data.data;
  if (!profile) {
    throw new Error("Admin profile response missing data");
  }
  return profile;
}

export async function changeAdminPassword(body: { currentPassword: string; newPassword: string }) {
  const res = await adminClient.put("/auth/changepassword", body);
  return res.data;
}

// ─── Members ─────────────────────────────────────────────────────────────────

export interface MemberListParams {
  page?: number;
  pageSize?: number;
  status?: string;
  search?: string;
}

export async function getMembers(params: MemberListParams = {}) {
  const res = await adminClient.get<ApiResponse<PagedResult<Member>>>("/members", {
    params: { page: 1, pageSize: 20, ...params },
  });
  return res.data.data!;
}

export async function getAdmins(page = 1, pageSize = 20, search?: string, role?: string, graduationYear?: number) {
  const res = await adminClient.get<ApiResponse<PagedResult<AdminUser>>>('/admins', {
    params: { page, pageSize, search, role, graduationYear },
  });
  return res.data.data!;
}

export async function createAdmin(body: CreateAdminRequest) {
  const res = await adminClient.post<ApiResponse<AdminUser>>('/admins', body);
  return res.data.data!;
}

export async function updateAdmin(adminId: string, body: UpdateAdminRequest) {
  const res = await adminClient.put<ApiResponse<AdminUser>>(`/admins/${adminId}`, body);
  return res.data.data!;
}

export async function approveMember(memberId: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/members/${memberId}/approve`);
  return res.data;
}

export async function rejectMember(memberId: string, reason?: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/members/${memberId}/reject`, { reason });
  return res.data;
}

export async function banMember(memberId: string, reason?: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/members/${memberId}/ban`, { memberId, reason });
  return res.data;
}

export async function unbanMember(memberId: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/members/${memberId}/unban`);
  return res.data;
}

export async function getMember(memberId: string) {
  const res = await adminClient.get<ApiResponse<Member>>(`/members/${memberId}`);
  return res.data.data!;
}

// ─── Member Import & Membership Activation ───────────────────────────────────

export interface ImportMemberItem {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  studentId?: string;
  graduationYear: number;
  departmentId?: string;
  paidMembershipYears?: number[];
}

export interface ImportMembersResult {
  imported: number;
  skipped: number;
  errors: string[];
}

export async function importMembers(members: ImportMemberItem[]) {
  const res = await adminClient.post<ApiResponse<ImportMembersResult>>("/members/import", { members });
  return res.data.data!;
}

export async function activateMembership(memberId: string, membershipYears: number[]) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/members/${memberId}/activate-membership`, { membershipYears });
  return res.data;
}

// ─── Campaigns ───────────────────────────────────────────────────────────────

export async function getCampaigns(page = 1, pageSize = 50, status?: string) {
  const res = await adminClient.get<ApiResponse<PagedResult<Campaign>>>("/campaigns", {
    params: { page, pageSize, status },
  });
  return res.data.data!;
}

export async function getCampaign(id: string) {
  const res = await adminClient.get<ApiResponse<Campaign>>(`/campaigns/${id}`);
  return res.data.data!;
}

export interface CreateCampaignBody {
  title: string;
  description: string;
  targetAmount: number;
  amountPerMember: number;
  pensionerAmountPerMember?: number;
  deadline: string;
  yearGroups?: number[];
  bannerImage?: File;
  youtubeVideoUrl?: string;
  allowOnlinePayments?: boolean;
  allowManualPayments?: boolean;
  bankAccountNumber?: string;
  bankAccountName?: string;
  bankName?: string;
  bankBranch?: string;
  mobileMoneyNumber?: string;
  mobileMoneyName?: string;
  mobileMoneyProvider?: string;
  isMembershipCampaign?: boolean;
  membershipYear?: number;
}

export async function createCampaign(body: CreateCampaignBody) {
  const res = await adminClient.post<ApiResponse<Campaign>>("/campaigns", toFormData(body), {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data!;
}

export interface UpdateCampaignBody {
  title?: string;
  description?: string;
  deadline?: string;
  status?: string;
  targetAmount?: number;
  amountPerMember?: number;
  pensionerAmountPerMember?: number;
  yearGroups?: number[];
  bannerImage?: File;
  youtubeVideoUrl?: string;
  allowOnlinePayments?: boolean;
  allowManualPayments?: boolean;
  bankAccountNumber?: string;
  bankAccountName?: string;
  bankName?: string;
  bankBranch?: string;
  mobileMoneyNumber?: string;
  mobileMoneyName?: string;
  mobileMoneyProvider?: string;
  isMembershipCampaign?: boolean;
  membershipYear?: number;
}

export async function updateCampaign(id: string, body: UpdateCampaignBody) {
  const res = await adminClient.put<ApiResponse<Campaign>>(`/campaigns/${id}`, toFormData(body), {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data!;
}

export async function deleteCampaign(id: string) {
  const res = await adminClient.delete<ApiResponse<unknown>>(`/campaigns/${id}`);
  return res.data;
}

export async function archiveCampaign(id: string) {
  const res = await adminClient.put<ApiResponse<Campaign>>(`/campaigns/${id}/archive`);
  return res.data.data!;
}

export async function unarchiveCampaign(id: string) {
  const res = await adminClient.put<ApiResponse<Campaign>>(`/campaigns/${id}/unarchive`);
  return res.data.data!;
}

export async function activateCampaign(id: string) {
  const res = await adminClient.put<ApiResponse<Campaign>>(`/campaigns/${id}/activate`);
  return res.data.data!;
}

// ─── Contributions ───────────────────────────────────────────────────────────

export interface ContributionListParams {
  page?: number;
  pageSize?: number;
  campaignId?: string;
  memberId?: string;
  status?: string;
}

export async function getContributions(params: ContributionListParams & { search?: string } = {}) {
  const res = await adminClient.get<ApiResponse<PagedResult<Contribution>>>("/contributions", {
    params: { page: 1, pageSize: 20, ...params },
  });
  return res.data.data!;
}

export interface RecordManualContributionBody {
  campaignId: string;
  memberNumber: string;
  memberName?: string;
  memberEmail?: string;
  amount: number;
  paymentMethod?: string;
  transactionRef?: string;
  notes?: string;
  paidAt?: string;
  confirmed?: boolean;
}

export async function recordManualContribution(body: RecordManualContributionBody) {
  const res = await adminClient.post<ApiResponse<Contribution>>("/contributions/manual", body);
  return res.data.data!;
}

export async function confirmContribution(id: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/contributions/${id}/confirm`);
  return res.data;
}

export async function rejectContribution(id: string, reason?: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/contributions/${id}/reject`, { reason });
  return res.data;
}

export async function getCampaignPaystackSummary(campaignId: string) {
  const res = await adminClient.get<ApiResponse<PaystackDisbursementSummary>>(`/campaigns/${campaignId}/paystack-summary`);
  return res.data.data!;
}

export async function markCampaignPaystackDisbursed(campaignId: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/campaigns/${campaignId}/paystack-disburse`);
  return res.data;
}

export async function getReportSummary() {
  const res = await adminClient.get<ApiResponse<ReportSummary>>('/reports/summary');
  return res.data.data!;
}

// ─── Jobs ─────────────────────────────────────────────────────────────────────

export interface CreateJobBody {
  title: string;
  company: string;
  location: string;
  type: string;
  description?: string;
  applyUrl?: string;
  deadline?: string;
  yearGroups?: number[];
  bannerImage?: File;
}

export interface UpdateJobBody {
  title: string;
  company: string;
  location: string;
  type: string;
  description?: string;
  applyUrl?: string;
  deadline?: string;
  status: string;
  yearGroups?: number[];
  bannerImage?: File;
}

export async function getJobs(
  page = 1,
  pageSize = 20,
  search?: string,
  status?: string,
  type?: string,
  location?: string,
  postedAfter?: string,
  postedBefore?: string,
) {
  const res = await adminClient.get<ApiResponse<PagedResult<Job>>>("/jobs", {
    params: { page, pageSize, search, status: status || undefined, type, location, postedAfter, postedBefore },
  });
  return res.data.data!;
}

export async function getJob(id: string) {
  const res = await adminClient.get<ApiResponse<Job>>(`/jobs/${id}`);
  return res.data.data!;
}

export async function createJob(body: CreateJobBody) {
  const res = await adminClient.post<ApiResponse<Job>>("/jobs", toFormData(body), {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data!;
}

export async function updateJob(id: string, body: UpdateJobBody) {
  const res = await adminClient.put<ApiResponse<Job>>(`/jobs/${id}`, toFormData(body), {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data!;
}

export async function deleteJob(id: string) {
  const res = await adminClient.delete<ApiResponse<unknown>>(`/jobs/${id}`);
  return res.data;
}

export async function closeJob(id: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/jobs/${id}/close`);
  return res.data;
}

// ─── Events ──────────────────────────────────────────────────────────────────

export interface CreateEventBody {
  title: string;
  description?: string;
  startDate: string;
  endDate?: string;
  venue: string;
  capacity?: number;
  isTicketed: boolean;
  ticketPrice?: number;
  googleLocationUrl?: string;
  yearGroups?: number[];
  bannerImage?: File;
  images?: File[];
  youtubeVideoUrls?: string[];
}

export interface UpdateEventBody {
  title: string;
  description?: string;
  startDate: string;
  endDate?: string;
  venue: string;
  capacity?: number;
  isTicketed: boolean;
  ticketPrice?: number;
  googleLocationUrl?: string;
  status: string;
  yearGroups?: number[];
  bannerImage?: File;
  images?: File[];
  existingImageUrls?: string[];
  youtubeVideoUrls?: string[];
}

export async function getEvents(page = 1, pageSize = 20) {
  const res = await adminClient.get<ApiResponse<PagedResult<AlumniEvent>>>("/events", {
    params: { page, pageSize },
  });
  return res.data.data!;
}

export async function createEvent(body: CreateEventBody) {
  const res = await adminClient.post<ApiResponse<AlumniEvent>>("/events", toFormData(body), {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data!;
}

export async function updateEvent(id: string, body: UpdateEventBody) {
  const res = await adminClient.put<ApiResponse<AlumniEvent>>(`/events/${id}`, toFormData(body), {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data!;
}

export async function deleteEvent(id: string) {
  const res = await adminClient.delete<ApiResponse<unknown>>(`/events/${id}`);
  return res.data;
}

export async function cancelEvent(id: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/events/${id}/cancel`);
  return res.data;
}

export async function getEventRsvps(id: string, page = 1, pageSize = 50, status = "Confirmed") {
  const res = await adminClient.get<ApiResponse<PagedResult<EventRegistration>>>(`/events/${id}/rsvps`, {
    params: { page, pageSize, status },
  });
  return res.data.data!;
}

export async function reopenRsvp(eventId: string, rsvpId: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/events/${eventId}/rsvps/${rsvpId}/reopen`);
  return res.data;
}

// ─── News ─────────────────────────────────────────────────────────────────────

export interface CreateNewsPostBody {
  title: string;
  content: string;
  category: string;
  isPinned: boolean;
  status: string;
  yearGroups?: number[];
  images?: File[];
  youtubeVideoUrls?: string[];
}

export interface UpdateNewsPostBody {
  title: string;
  content: string;
  category: string;
  isPinned: boolean;
  status: string;
  yearGroups?: number[];
  images?: File[];
  existingImageUrls?: string[];
  youtubeVideoUrls?: string[];
}

export async function getNewsPosts(page = 1, pageSize = 20, search?: string, status?: string) {
  const res = await adminClient.get<ApiResponse<PagedResult<NewsPost>>>("/news", {
    params: { page, pageSize, search, status: status || undefined },
  });
  return res.data.data!;
}

export async function getNewsPost(id: string) {
  const res = await adminClient.get<ApiResponse<NewsPost>>(`/news/${id}`);
  return res.data.data!;
}

export async function createNewsPost(body: CreateNewsPostBody) {
  const res = await adminClient.post<ApiResponse<NewsPost>>("/news", toFormData(body), {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data!;
}

export async function publishNewsPost(id: string) {
  const res = await adminClient.put<ApiResponse<NewsPost>>(`/news/${id}/publish`);
  return res.data.data!;
}

export async function deleteNewsPost(id: string) {
  const res = await adminClient.delete<ApiResponse<unknown>>(`/news/${id}`);
  return res.data;
}

export async function updateNewsPost(id: string, body: UpdateNewsPostBody) {
  const res = await adminClient.put<ApiResponse<NewsPost>>(`/news/${id}`, toFormData(body), {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data!;
}

// ─── Forum ────────────────────────────────────────────────────────────────────

export async function getForumCategories(page = 1, pageSize = 50) {
  const res = await adminClient.get<ApiResponse<PagedResult<ForumCategory>>>("/forum/categories", {
    params: { page, pageSize },
  });
  return res.data.data!;
}

export async function createForumCategory(name: string, description?: string) {
  const res = await adminClient.post<ApiResponse<ForumCategory>>("/forum/categories", { name, description });
  return res.data.data!;
}

export async function getForumThreads(page = 1, pageSize = 20, categoryId?: string, search?: string, filter?: string) {
  const res = await adminClient.get<ApiResponse<PagedResult<ForumThread>>>("/forum/threads", {
    params: { page, pageSize, categoryId, search, filter },
  });
  return res.data.data!;
}

export async function pinThread(id: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/forum/threads/${id}/pin`);
  return res.data;
}

export async function closeThread(id: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/forum/threads/${id}/close`);
  return res.data;
}

export async function deleteThread(id: string) {
  const res = await adminClient.delete<ApiResponse<unknown>>(`/forum/threads/${id}`);
  return res.data;
}

// ─── Mentorship ───────────────────────────────────────────────────────────────

export async function getMentorProfiles(page = 1, pageSize = 20, status?: string, search?: string) {
  const res = await adminClient.get<ApiResponse<PagedResult<MentorProfile>>>("/mentorship/profiles", {
    params: { page, pageSize, status, search },
  });
  return res.data.data!;
}

export async function approveMentor(id: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/mentorship/profiles/${id}/approve`);
  return res.data;
}

export async function rejectMentor(id: string) {
  const res = await adminClient.put<ApiResponse<unknown>>(`/mentorship/profiles/${id}/reject`);
  return res.data;
}

export async function getMentorshipRequests(page = 1, pageSize = 20) {
  const res = await adminClient.get<ApiResponse<PagedResult<MentorshipRequest>>>("/mentorship/requests", {
    params: { page, pageSize },
  });
  return res.data.data!;
}

// ─── Resources ────────────────────────────────────────────────────────────────

export interface CreateResourceBody {
  title: string;
  description?: string;
  category: string;
  type: string;
  yearGroups?: number[];
  externalUrl?: string;
  file?: File;
  bannerImage?: File;
}

export interface UpdateResourceBody {
  title: string;
  description?: string;
  category: string;
  type: string;
  yearGroups?: number[];
  externalUrl?: string;
  file?: File;
  bannerImage?: File;
}

export async function getResources(
  page = 1,
  pageSize = 20,
  category?: string,
  search?: string,
  type?: string,
  addedAfter?: string,
  addedBefore?: string,
) {
  const res = await adminClient.get<ApiResponse<PagedResult<Resource>>>("/resources", {
    params: { page, pageSize, category, search, type, addedAfter, addedBefore },
  });
  return res.data.data!;
}

export async function getResource(id: string) {
  const res = await adminClient.get<ApiResponse<Resource>>(`/resources/${id}`);
  return res.data.data!;
}

export async function createResource(body: CreateResourceBody) {
  const res = await adminClient.post<ApiResponse<Resource>>("/resources", toFormData(body), {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data!;
}

export async function deleteResource(id: string) {
  const res = await adminClient.delete<ApiResponse<unknown>>(`/resources/${id}`);
  return res.data;
}

export async function updateResource(id: string, body: UpdateResourceBody) {
  const res = await adminClient.put<ApiResponse<Resource>>(`/resources/${id}`, toFormData(body), {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data!;
}

// ─── File Uploads ─────────────────────────────────────────────────────────────

export async function uploadImage(file: File) {
  const formData = new FormData();
  formData.append("file", file);
  const res = await adminClient.post<ApiResponse<{ fileName: string }>>("/uploads/image", formData, {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data!;
}

export async function uploadFile(file: File) {
  const formData = new FormData();
  formData.append("file", file);
  const res = await adminClient.post<ApiResponse<{ fileName: string }>>("/uploads/file", formData, {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data!;
}

// ── Spotlights ───────────────────────────────────────────────────────────────

export async function getSpotlights(page = 1, pageSize = 10, status?: string): Promise<PagedResult<Spotlight>> {
  const res = await adminClient.get<ApiResponse<PagedResult<Spotlight>>>("/spotlights", {
    params: { page, pageSize, status: status || undefined },
  });
  return res.data.data!;
}

export async function createSpotlight(data: { memberId: string; title: string; story: string; imageUrl?: string }): Promise<Spotlight> {
  const res = await adminClient.post<ApiResponse<Spotlight>>("/spotlights", data);
  return res.data.data!;
}

export async function approveSpotlight(spotlightId: string): Promise<Spotlight> {
  const res = await adminClient.post<ApiResponse<Spotlight>>(`/spotlights/${spotlightId}/approve`);
  return res.data.data!;
}

export async function rejectSpotlight(spotlightId: string, reason?: string): Promise<Spotlight> {
  const res = await adminClient.post<ApiResponse<Spotlight>>(`/spotlights/${spotlightId}/reject`, { reason });
  return res.data.data!;
}

// ── In-app Notifications ────────────────────────────────────────────────────

export async function getNotifications(page = 1, pageSize = 20): Promise<PagedResult<NotificationItem>> {
  const res = await adminClient.get<ApiResponse<PagedResult<NotificationItem>>>("/notifications", { params: { page, pageSize } });
  return res.data.data!;
}

export async function getUnreadNotificationCount(): Promise<number> {
  const res = await adminClient.get<ApiResponse<number>>("/notifications/unread-count");
  return res.data.data ?? 0;
}

export async function markNotificationRead(id: string): Promise<void> {
  await adminClient.put(`/notifications/${id}/read`);
}

export async function markAllNotificationsRead(): Promise<void> {
  await adminClient.put("/notifications/read-all");
}
