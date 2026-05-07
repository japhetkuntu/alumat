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

export interface BaseFilter {
  page?: number;
  pageSize?: number;
  sortColumn?: string;
  sortDir?: string;
  search?: string;
}

// ─── Auth ─────────────────────────────────────────────────────────────────────

export type UserRole = "SuperAdmin" | "Admin" | "Member";
export type MemberStatus = "Pending" | "Active" | "Suspended" | "Banned" | "Blocked";

export interface AuthData {
  id: string;
  email: string;
  name: string;
  role: UserRole;
  graduationYear?: number;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  phone?: string;
  graduationYear: number;
  departmentId: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

// ─── Department / Faculty ─────────────────────────────────────────────────────

export interface Faculty {
  id: string;
  name: string;
  createdAt: string;
}

export interface Department {
  id: string;
  name: string;
  facultyId: string;
  faculty?: Faculty;
  createdAt: string;
}

// ─── Member ───────────────────────────────────────────────────────────────────

export interface Member {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  graduationYear: number;
  departmentId: string;
  department?: Department;
  profilePictureUrl?: string;
  status: MemberStatus;
  role: UserRole;
  createdAt: string;
  // profile fields (populated in directory / profile responses)
  company?: string;
  jobTitle?: string;
  location?: string;
  linkedInUrl?: string;
  bio?: string;
  departmentName?: string;
  // admin-facing fields
  memberNumber?: string;
  isEmailVerified?: boolean;
  rejectionCount?: number;
  banReason?: string;
  // membership fields
  isMembershipActive?: boolean;
  membershipExpiry?: string;
  membershipYearsPaid?: number;
  lastMembershipPaidAt?: string;
  studentId?: string;
}

export type MemberFilter = BaseFilter & {
  status?: MemberStatus;
  departmentId?: string;
  graduationYear?: number;
};

export interface AdminUser {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: UserRole;
  graduationYear?: number;
  isDisabled?: boolean;
  createdAt: string;
}

export interface CreateAdminRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  role: UserRole;
  graduationYear?: number;
}

export interface UpdateAdminRequest {
  firstName: string;
  lastName: string;
  role: UserRole;
  graduationYear?: number;
  isDisabled?: boolean;
}

// ─── Campaign ─────────────────────────────────────────────────────────────────

export type CampaignStatus = "Draft" | "Active" | "Closed" | "Completed" | "Archived";

export interface Campaign {
  id: string;
  title: string;
  description?: string;
  targetAmount: number;
  amountPerMember: number;
  pensionerAmountPerMember?: number;
  deadline: string;
  status: CampaignStatus;
  collectedAmount: number;
  paidCount: number;
  yearGroups?: number[];
  isMembershipCampaign?: boolean;
  totalEligibleMembers?: number;
  isPaystackDisbursed: boolean;
  paystackDisbursedAt?: string;
  paystackDisbursedBy?: string;
  createdAt: string;
  bannerImageUrl?: string;
  youtubeVideoUrl?: string;
  allowOnlinePayments: boolean;
  allowManualPayments: boolean;
  membershipYear?: number;
  bankAccount?: {
    accountNumber: string;
    accountName: string;
    bankName: string;
    branch: string;
  };
  mobileMoneyAccount?: {
    mobileMoneyNumber: string;
    name: string;
    provider: string;
  };
}

export interface CreateCampaignRequest {
  title: string;
  description: string;
  targetAmount: number;
  amountPerMember: number;
  deadline: string;
  allowOnlinePayments?: boolean;
  allowManualPayments?: boolean;
  bankAccountNumber?: string;
  bankAccountName?: string;
  bankName?: string;
  bankBranch?: string;
  mobileMoneyNumber?: string;
  mobileMoneyName?: string;
  mobileMoneyProvider?: string;
}

export type CampaignFilter = BaseFilter & {
  status?: CampaignStatus;
};

export interface PaystackDisbursementSummary {
  totalPaidToPaystack: number;
  totalDisbursed: number;
  totalOutstanding: number;
  confirmedCount: number;
  disbursedCount: number;
}

export interface ReportSummary {
  totalMembers: number;
  totalContributions: number;
  totalCollected: number;
  totalCampaigns: number;
  activeCampaigns: number;
  closedCampaigns: number;
  totalEvents: number;
  totalJobs: number;
}

// ─── Contribution ─────────────────────────────────────────────────────────────

export type ContributionStatus = "Pending" | "Confirmed" | "Rejected";
export type ContributionMethod = "Manual" | "Paystack" | "MobileMoney" | "BankTransfer";

export interface Contribution {
  id: string;
  campaignId: string;
  campaignTitle?: string;
  memberId: string;
  memberName?: string;
  memberEmail?: string;
  memberProfilePictureUrl?: string;
  memberNumber?: string;
  amount: number;
  paymentMethod: ContributionMethod;
  status: ContributionStatus;
  transactionRef?: string;
  notes?: string;
  confirmedAt?: string;
  createdAt: string;
}

export interface InitiateContributionRequest {
  campaignId: string;
  amount: number;
  callbackUrl: string;
}

export interface InitiateContributionResponse {
  authorizationUrl: string;
  reference: string;
  contributionId: string;
}

export interface ManualPaymentRequest {
  campaignId: string;
  memberId: string;
  amount: number;
  paidAt: string;
  notes?: string;
}

export type ContributionFilter = BaseFilter & {
  campaignId?: string;
  memberId?: string;
  status?: ContributionStatus;
  method?: ContributionMethod;
};

// ─── Job ──────────────────────────────────────────────────────────────────────

export type JobType = "Full-time" | "Part-time" | "Contract" | "Internship";
export type JobStatus = "Active" | "Closed" | "Draft";

export interface Job {
  id: string;
  postedBy: string;
  title: string;
  company: string;
  location: string;
  type: JobType;
  description?: string;
  applyUrl?: string;
  deadline?: string;
  status: JobStatus;
  yearGroups?: number[];
  createdAt: string;
  bannerImageUrl?: string;
}

export type JobFilter = BaseFilter & {
  type?: JobType;
  status?: JobStatus;
  location?: string;
};

// ─── Event ────────────────────────────────────────────────────────────────────

export type EventStatus = "Upcoming" | "Ongoing" | "Completed" | "Cancelled";

export interface AlumniEvent {
  id: string;
  title: string;
  description?: string;
  startDate: string;
  endDate?: string;
  venue: string;
  capacity?: number;
  isTicketed: boolean;
  ticketPrice?: number;
  status: EventStatus;
  yearGroups?: number[];
  rsvpCount: number;
  createdAt: string;
  bannerImageUrl?: string;
  imageUrls?: string[];
  youtubeVideoUrls?: string[];
  googleLocationUrl?: string;
}

export interface EventRegistration {
  id: string;
  eventId: string;
  eventTitle?: string;
  memberId: string;
  memberName?: string;
  memberEmail?: string;
  memberProfilePictureUrl?: string;
  status: "Confirmed" | "Cancelled";
  createdAt: string;
  updatedAt?: string;
}

// ─── News ─────────────────────────────────────────────────────────────────────

export type NewsStatus = "Draft" | "Published" | "Archived";

export interface NewsPost {
  id: string;
  authorId: string;
  authorName?: string;
  title: string;
  content: string;
  category: string;
  isPinned: boolean;
  status: NewsStatus;
  yearGroups?: number[];
  publishedAt?: string;
  createdAt: string;
  imageUrls?: string[];
  youtubeVideoUrls?: string[];
}

// ─── Forum ────────────────────────────────────────────────────────────────────

export interface ForumCategory {
  id: string;
  name: string;
  description?: string;
  sortOrder?: number;
  createdAt: string;
}

export interface ForumThread {
  id: string;
  categoryId: string;
  categoryName?: string;
  authorId: string;
  authorName?: string;
  authorProfilePictureUrl?: string;
  title: string;
  isPinned: boolean;
  isClosed: boolean;
  replyCount: number;
  createdAt: string;
}

export interface ForumPost {
  id: string;
  threadId: string;
  authorId: string;
  authorName?: string;
  authorProfilePictureUrl?: string;
  content: string;
  createdAt: string;
}

// ─── Mentorship ───────────────────────────────────────────────────────────────

export type MentorProfileStatus = "Pending" | "Approved" | "Rejected";

export interface MentorProfile {
  id: string;
  memberId: string;
  memberName?: string;
  memberProfilePictureUrl?: string;
  area: string;
  bio?: string;
  yearGroups?: number[];
  maxMentees: number;
  currentMenteeCount: number;
  status: MentorProfileStatus;
  createdAt: string;
}

export type MentorshipStatus = "Pending" | "Accepted" | "Rejected" | "Completed";

export interface MentorshipRequest {
  id: string;
  mentorProfileId: string;
  mentorProfileName?: string;
  menteeId: string;
  menteeName?: string;
  menteeProfilePictureUrl?: string;
  area: string;
  message?: string;
  status: MentorshipStatus;
  createdAt: string;
}

// ─── Resource ─────────────────────────────────────────────────────────────────

export interface Resource {
  id: string;
  title: string;
  description?: string;
  category: string;
  type: "File" | "Link";
  fileUrl?: string;
  externalUrl?: string;
  yearGroups?: number[];
  uploadedBy?: string;
  downloadCount?: number;
  createdAt: string;
  bannerImageUrl?: string;
}

// ─── Dashboard ────────────────────────────────────────────────────────────────

export interface AdminDashboardStats {
  totalMembers: number;
  pendingApprovals: number;
  activeMembers: number;
  activeCampaigns: number;
  totalContributions: number;
  totalAmountCollected: number;
  upcomingEvents: number;
  openJobs: number;
}

export interface MemberDashboardStats {
  totalContributions: number;
  amountContributed: number;
  activeCampaigns: number;
  upcomingEvents: number;
}

// ─── Spotlight ────────────────────────────────────────────────────────────────

export interface Spotlight {
  id: string;
  memberId: string;
  memberName?: string;
  memberProfilePictureUrl?: string;
  memberGraduationYear?: number;
  title: string;
  story: string;
  imageUrl?: string;
  status: "Pending" | "Approved" | "Rejected";
  featuredMonth?: string;
  createdAt: string;
}
