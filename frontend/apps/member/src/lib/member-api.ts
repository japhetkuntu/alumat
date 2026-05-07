import { memberClient, publicMemberClient } from "@/lib/api-client";
import type { PagedResult, Campaign, Contribution, AlumniEvent, EventRegistration, Job, NewsPost, ForumCategory, ForumThread, ForumPost, MentorProfile, MentorshipRequest, Resource, Member, YearGroupLeaderboardEntry, MemberBadge, Spotlight, Referral, ReferralInfo, ClassNote, NotificationPreference } from "@/types";

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

// ── Departments ─────────────────────────────────────────────────────────────

export interface Department {
  id: string;
  name: string;
}

export async function getDepartments(): Promise<Department[]> {
  const res = await memberClient.get("/departments");
  return res.data.data ?? [];
}

// ── Auth / Profile ──────────────────────────────────────────────────────────

export interface MemberProfileResponse {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  memberNumber?: string;
  graduationYear: number;
  departmentId: string;
  company?: string;
  jobTitle?: string;
  location?: string;
  linkedInUrl?: string;
  bio?: string;
  profilePictureUrl?: string;
  status: string;
  employmentStatus?: string;
  isMembershipActive?: boolean;
  membershipExpiry?: string;
  membershipYearsPaid?: number;
  lastMembershipPaidAt?: string;
}

export interface MembershipStatusResponse {
  isMembershipActive: boolean;
  membershipExpiry?: string;
  membershipYearsPaid: number;
  lastMembershipPaidAt?: string;
  isCurrentYearPaid: boolean;
  hasArrears: boolean;
  arrearsCount: number;
  arrearsYears: number[];
}

export async function getMyProfile(): Promise<MemberProfileResponse> {
  const res = await memberClient.get("/auth/me");
  return res.data.data!;
}

export interface UpdateProfileBody {
  company?: string;
  jobTitle?: string;
  location?: string;
  linkedInUrl?: string;
  bio?: string;
  phone?: string;
  employmentStatus?: string;
  profilePicture?: File;
}

export async function updateMyProfile(body: UpdateProfileBody) {
  const res = await memberClient.put("/auth/me", toFormData(body), {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data;
}

export async function changePassword(currentPassword: string, newPassword: string) {
  const res = await memberClient.put("/auth/changepassword", { currentPassword, newPassword });
  return res.data;
}

// ── Campaigns ───────────────────────────────────────────────────────────────

export async function getMyCampaigns(page = 1, pageSize = 20): Promise<PagedResult<Campaign>> {
  const res = await memberClient.get("/campaigns", { params: { page, pageSize } });
  return res.data.data!;
}

export async function getCampaignById(campaignId: string): Promise<Campaign> {
  const res = await memberClient.get(`/campaigns/${campaignId}`);
  return res.data.data!;
}

/** Public — no auth required. Returns the active membership campaign for the current year, or null if none. */
export async function getCurrentMembershipCampaign(): Promise<Campaign | null> {
  const res = await publicMemberClient.get("/campaigns/membership/current");
  return res.data.data ?? null;
}

export async function getMyMembershipStatus(): Promise<MembershipStatusResponse> {
  const res = await memberClient.get("/contributions/membership/status");
  return res.data.data!;
}

export async function getMyCurrentYearUnpaidMembershipCampaigns(): Promise<Campaign[]> {
  const res = await memberClient.get("/contributions/membership/current-unpaid");
  return res.data.data ?? [];
}

export async function renewMembership(campaignId: string, years = 1, paymentMethod: "online" | "manual" = "online") {
  const res = await memberClient.post("/contributions/membership/renew", { campaignId, years, paymentMethod });
  return res.data.data;
}

// ── Contributions ────────────────────────────────────────────────────────────

export async function getMyContributions(params?: { page?: number; pageSize?: number; campaignId?: string }): Promise<PagedResult<Contribution>> {
  const res = await memberClient.get("/contributions", { params });
  return res.data.data!;
}

export interface InitiatePaystackBody { campaignId: string; amount: number; }
export interface InitiatePaystackGuestBody { campaignId: string; amount: number; email: string; callbackUrl?: string; }

export async function initiatePaystackPayment(body: InitiatePaystackBody) {
  const res = await memberClient.post("/contributions/paystack/initiate", body);
  return res.data.data!;
}

export async function initiatePaystackPaymentGuest(body: InitiatePaystackGuestBody) {
  const res = await memberClient.post("/contributions/paystack/initiate/guest", body);
  return res.data.data!;
}

export async function verifyPaystackPayment(reference: string) {
  const res = await memberClient.get(`/contributions/paystack/verify/${reference}`);
  return res.data.data!;
}

export interface ContributionStatusResponse {
  reference: string;
  status: string;
  amount?: number;
  paymentMethod?: string;
  message?: string;
}

export async function getPaystackPaymentStatus(reference: string) {
  const res = await memberClient.get(`/contributions/paystack/status/${reference}`);
  return res.data.data as ContributionStatusResponse;
}

export interface ActivationStatusResponse {
  status: string;
  email?: string;
  memberNumber?: string;
  message?: string;
}

/** Public read-only status check for the membership activation callback page. No auth required, no side effects. */
export async function getActivationStatus(reference: string): Promise<ActivationStatusResponse> {
  const res = await publicMemberClient.get(`/contributions/paystack/activation-status/${reference}`);
  return res.data.data as ActivationStatusResponse;
}

export interface UploadProofBody { campaignId: string; transactionRef: string; notes?: string; }

export async function uploadContributionProof(body: UploadProofBody) {
  const res = await memberClient.post("/contributions/proof", body);
  return res.data;
}

// ── Events ───────────────────────────────────────────────────────────────────

export async function getEvents(page = 1, pageSize = 20, status?: string): Promise<PagedResult<AlumniEvent>> {
  const res = await memberClient.get("/events", { params: { page, pageSize, status } });
  return res.data.data!;
}

export async function getEventById(eventId: string): Promise<AlumniEvent> {
  const res = await memberClient.get(`/events/${eventId}`);
  return res.data.data!;
}

export async function rsvpEvent(eventId: string) {
  const res = await memberClient.post("/events/rsvp", { eventId });
  return res.data;
}

export async function cancelRsvp(eventId: string) {
  const res = await memberClient.delete(`/events/${eventId}/rsvp`);
  return res.data;
}

export async function getMyRsvps(status: "Confirmed" | "Cancelled" | "All" = "Confirmed"): Promise<EventRegistration[]> {
  const res = await memberClient.get("/events/my-rsvps", { params: { status } });
  return res.data.data!;
}

// ── Jobs ─────────────────────────────────────────────────────────────────────

export async function getJobs(
  page = 1,
  pageSize = 20,
  type?: string,
  search?: string,
  location?: string,
  postedAfter?: string,
  postedBefore?: string,
): Promise<PagedResult<Job>> {
  const res = await memberClient.get("/jobs", {
    params: {
      page,
      pageSize,
      type: type || undefined,
      search: search || undefined,
      location: location || undefined,
      postedAfter: postedAfter || undefined,
      postedBefore: postedBefore || undefined,
    },
  });
  return res.data.data!;
}

export async function getJobById(jobId: string): Promise<Job> {
  const res = await memberClient.get(`/jobs/${jobId}`);
  return res.data.data!;
}

// ── News ─────────────────────────────────────────────────────────────────────

export async function getNewsPosts(page = 1, pageSize = 20, category?: string, search?: string): Promise<PagedResult<NewsPost>> {
  const res = await memberClient.get("/news", { params: { page, pageSize, category: category || undefined, search: search || undefined } });
  return res.data.data!;
}

export async function getNewsPost(postId: string): Promise<NewsPost> {
  const res = await memberClient.get(`/news/${postId}`);
  return res.data.data!;
}

// ── Forum ─────────────────────────────────────────────────────────────────────

export async function getForumCategories(): Promise<PagedResult<ForumCategory>> {
  const res = await memberClient.get("/forum/categories");
  return res.data.data!;
}

export async function getForumThreads(
  page = 1,
  pageSize = 20,
  categoryId?: string,
  search?: string,
  filter?: string,
): Promise<PagedResult<ForumThread>> {
  const res = await memberClient.get("/forum/threads", {
    params: { page, pageSize, categoryId: categoryId || undefined, search: search || undefined, filter: filter || undefined },
  });
  return res.data.data!;
}

export async function getThreadPosts(threadId: string, page = 1, pageSize = 30): Promise<PagedResult<ForumPost>> {
  const res = await memberClient.get(`/forum/threads/${threadId}/posts`, { params: { page, pageSize } });
  return res.data.data!;
}

export interface CreateThreadBody { categoryId: string; title: string; content: string; }

export async function createThread(body: CreateThreadBody) {
  const res = await memberClient.post("/forum/threads", body);
  return res.data.data!;
}

export async function replyToThread(threadId: string, content: string) {
  const res = await memberClient.post(`/forum/threads/${threadId}/reply`, { content });
  return res.data;
}

// ── Mentorship ───────────────────────────────────────────────────────────────

export async function getMentors(page = 1, pageSize = 20, search?: string): Promise<PagedResult<MentorProfile>> {
  const res = await memberClient.get("/mentorship/mentors", { params: { page, pageSize, search } });
  return res.data.data!;
}

export interface RegisterAsMentorBody { area: string; bio?: string; maxMentees?: number; }

export async function registerAsMentor(body: RegisterAsMentorBody) {
  const res = await memberClient.post("/mentorship/mentor-profile", body);
  return res.data;
}

export interface RequestMentorshipBody { mentorProfileId: string; area: string; message?: string; }

export async function requestMentorship(body: RequestMentorshipBody) {
  const res = await memberClient.post("/mentorship/requests", body);
  return res.data;
}

export async function getMyMentorshipRequests(page = 1, pageSize = 20): Promise<PagedResult<MentorshipRequest>> {
  const res = await memberClient.get("/mentorship/requests/mine", { params: { page, pageSize } });
  return res.data.data!;
}

export async function getMyMentorProfile(): Promise<MentorProfile> {
  const res = await memberClient.get("/mentorship/mentor-profile/mine");
  return res.data.data!;
}

export async function getIncomingMentorshipRequests(page = 1, pageSize = 20): Promise<PagedResult<MentorshipRequest>> {
  const res = await memberClient.get("/mentorship/requests/incoming", { params: { page, pageSize } });
  return res.data.data!;
}

export async function acceptMentorshipRequest(requestId: string) {
  const res = await memberClient.put(`/mentorship/requests/${requestId}/accept`);
  return res.data;
}

export async function rejectMentorshipRequest(requestId: string) {
  const res = await memberClient.put(`/mentorship/requests/${requestId}/reject`);
  return res.data;
}

// ── File Uploads ─────────────────────────────────────────────────────────────

export async function uploadImage(file: File) {
  const formData = new FormData();
  formData.append("file", file);
  const res = await memberClient.post("/uploads/image", formData, {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return res.data.data! as { fileName: string };
}

// ── Directory ────────────────────────────────────────────────────────────────

export async function searchDirectory(params?: { page?: number; pageSize?: number; search?: string; departmentId?: string; graduationYear?: number }): Promise<PagedResult<Member>> {
  const res = await memberClient.get("/directory", { params });
  return res.data.data!;
}

// ── Resources ────────────────────────────────────────────────────────────────

export async function getResources(
  page = 1,
  pageSize = 20,
  category?: string,
  search?: string,
  type?: string,
  addedAfter?: string,
  addedBefore?: string,
): Promise<PagedResult<Resource>> {
  const res = await memberClient.get("/resources", {
    params: {
      page,
      pageSize,
      category: category || undefined,
      search: search || undefined,
      type: type || undefined,
      addedAfter: addedAfter || undefined,
      addedBefore: addedBefore || undefined,
    },
  });
  return res.data.data!;
}

export async function getResource(id: string): Promise<Resource> {
  const res = await memberClient.get(`/resources/${id}`);
  return res.data.data!;
}

export async function trackResourceDownload(id: string): Promise<void> {
  await memberClient.post(`/resources/${id}/download`);
}

// ── Leaderboard ──────────────────────────────────────────────────────────────

export async function getLeaderboard(): Promise<YearGroupLeaderboardEntry[]> {
  const res = await memberClient.get("/leaderboard");
  return res.data.data!;
}

// ── Badges ───────────────────────────────────────────────────────────────────

export async function getMyBadges(): Promise<MemberBadge[]> {
  const res = await memberClient.get("/badges");
  return res.data.data!;
}

export async function evaluateBadges(): Promise<MemberBadge[]> {
  const res = await memberClient.post("/badges/evaluate");
  return res.data.data!;
}

// ── Spotlights ───────────────────────────────────────────────────────────────

export async function getSpotlights(page = 1, pageSize = 10): Promise<PagedResult<Spotlight>> {
  const res = await memberClient.get("/spotlights", { params: { page, pageSize } });
  return res.data.data!;
}

export async function getSpotlight(id: string): Promise<Spotlight> {
  const res = await memberClient.get(`/spotlights/${id}`);
  return res.data.data!;
}

export async function submitSpotlight(data: { title: string; story: string; imageUrl?: string }): Promise<Spotlight> {
  const res = await memberClient.post("/spotlights", data);
  return res.data.data!;
}

export async function getMySpotlights(): Promise<Spotlight[]> {
  const res = await memberClient.get("/spotlights/mine");
  return res.data.data!;
}

// ── Referrals ────────────────────────────────────────────────────────────────

export async function getReferralInfo(): Promise<ReferralInfo> {
  const res = await memberClient.get("/referrals");
  return res.data.data!;
}

export async function sendReferralInvite(email: string): Promise<void> {
  await memberClient.post("/referrals/invite", { email });
}

export async function getMyReferrals(): Promise<Referral[]> {
  const res = await memberClient.get("/referrals/list");
  return res.data.data!;
}

// ── Class Notes ──────────────────────────────────────────────────────────────

export async function getClassNotes(page = 1, pageSize = 20): Promise<PagedResult<ClassNote>> {
  const res = await memberClient.get("/classnotes", { params: { page, pageSize } });
  return res.data.data!;
}

export async function createClassNote(data: { content: string; imageUrl?: string }): Promise<ClassNote> {
  const res = await memberClient.post("/classnotes", data);
  return res.data.data!;
}

export async function toggleClassNoteLike(classNoteId: string): Promise<{ liked: boolean; likeCount: number }> {
  const res = await memberClient.post(`/classnotes/${classNoteId}/like`);
  return res.data.data!;
}

export async function deleteClassNote(classNoteId: string): Promise<void> {
  await memberClient.delete(`/classnotes/${classNoteId}`);
}

// ── Notification Preferences ─────────────────────────────────────────────────

export async function getNotificationPreferences(): Promise<NotificationPreference> {
  const res = await memberClient.get("/notificationpreferences");
  return res.data.data!;
}

export async function updateNotificationPreferences(data: Omit<NotificationPreference, "id">): Promise<NotificationPreference> {
  const res = await memberClient.put("/notificationpreferences", data);
  return res.data.data!;
}
