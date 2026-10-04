using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class MemberAccountDeletionService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Notification> notificationRepo,
    IAlumniPgRepository<NotificationPreference> preferenceRepo,
    IAlumniPgRepository<PushSubscription> pushRepo,
    IAlumniPgRepository<MentorProfile> mentorRepo,
    IAlumniPgRepository<MentorshipRequest> mentorshipRequestRepo,
    IAlumniPgRepository<BusinessListing> listingRepo,
    IAlumniPgRepository<Spotlight> spotlightRepo,
    IAlumniPgRepository<CommunityMembership> communityMembershipRepo,
    IAlumniPgRepository<EventRsvp> rsvpRepo,
    IAlumniPgRepository<MemberBadge> badgeRepo,
    IAlumniPgRepository<RecurringContribution> recurringRepo,
    IAlumniPgRepository<Pledge> pledgeRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<PaymentTransaction> transactionRepo,
    IAlumniPgRepository<StoreOrder> storeOrderRepo,
    IAlumniPgRepository<ServiceRequest> serviceRequestRepo,
    IAlumniPgRepository<ForumThread> forumThreadRepo,
    IAlumniPgRepository<ForumPost> forumPostRepo,
    IAlumniPgRepository<ClassNote> classNoteRepo,
    IAlumniPgRepository<ClassNoteLike> classNoteLikeRepo,
    IAlumniPgRepository<Referral> referralRepo,
    ILogger<MemberAccountDeletionService> logger) : IMemberAccountDeletionService
{
    public const string ConfirmationWord = "DELETE";

    public async Task<IApiResponse<object>> DeleteMyAccountAsync(AuthData auth, string confirmation)
    {
        try
        {
            if (!string.Equals(confirmation?.Trim(), ConfirmationWord, StringComparison.Ordinal))
                return ApiResponseExtensions.ToBadRequestApiResponse<object>($"Type {ConfirmationWord} to confirm.");

            var member = await memberRepo.GetByIdAsync(auth.Id);
            if (member is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Account not found.");
            if (member.Status == "Deleted")
                return new object().ToOkApiResponse("Account already deleted.");

            var id = member.Id;

            // ── Personal content and preferences: removed outright ─────────────────────────────
            foreach (var n in await notificationRepo.GetAllAsync(n => n.RecipientId == id && n.RecipientType == "Member")) await notificationRepo.RemoveAsync(n);
            foreach (var p in await preferenceRepo.GetAllAsync(p => p.MemberId == id)) await preferenceRepo.RemoveAsync(p);
            foreach (var s in await pushRepo.GetAllAsync(s => s.OwnerId == id && s.OwnerType == PushSubscriptionOwnerTypes.Member)) await pushRepo.RemoveAsync(s);
            foreach (var b in await badgeRepo.GetAllAsync(b => b.MemberId == id)) await badgeRepo.RemoveAsync(b);
            foreach (var m in await communityMembershipRepo.GetAllAsync(m => m.MemberId == id)) await communityMembershipRepo.RemoveAsync(m);
            foreach (var r in await rsvpRepo.GetAllAsync(r => r.MemberId == id)) await rsvpRepo.RemoveAsync(r);
            foreach (var p in await pledgeRepo.GetAllAsync(p => p.MemberId == id)) await pledgeRepo.RemoveAsync(p);
            foreach (var l in await listingRepo.GetAllAsync(l => l.MemberId == id)) await listingRepo.RemoveAsync(l);
            foreach (var s in await spotlightRepo.GetAllAsync(s => s.MemberId == id)) await spotlightRepo.RemoveAsync(s);

            // Mentoring: the profile, requests made to it, and requests this member made as a mentee.
            var mentorProfiles = (await mentorRepo.GetAllAsync(p => p.MemberId == id)).ToList();
            var profileIds = mentorProfiles.Select(p => p.Id).ToList();
            foreach (var req in await mentorshipRequestRepo.GetAllAsync(r => r.MenteeId == id || profileIds.Contains(r.MentorProfileId)))
                await mentorshipRequestRepo.RemoveAsync(req);
            foreach (var p in mentorProfiles) await mentorRepo.RemoveAsync(p);

            // A monthly gift must stop with the account.
            var recurring = (await recurringRepo.GetAllAsync(r => r.MemberId == id)).ToList();
            foreach (var r in recurring) { r.Status = "Cancelled"; r.Member = Former(r.Member); }
            if (recurring.Count > 0) await recurringRepo.UpdateRangeAsync(recurring);

            // ── Money and order records: kept for the institution's books, identity removed ─────
            var contributions = (await contributionRepo.GetAllAsync(c => c.MemberId == id)).ToList();
            foreach (var c in contributions) c.Member = Former(c.Member);
            if (contributions.Count > 0) await contributionRepo.UpdateRangeAsync(contributions);

            var transactions = (await transactionRepo.GetAllAsync(t => t.MemberId == id)).ToList();
            foreach (var t in transactions) t.Member = Former(t.Member);
            if (transactions.Count > 0) await transactionRepo.UpdateRangeAsync(transactions);

            var orders = (await storeOrderRepo.GetAllAsync(o => o.MemberId == id)).ToList();
            foreach (var o in orders) o.Member = Former(o.Member);
            if (orders.Count > 0) await storeOrderRepo.UpdateRangeAsync(orders);

            var requests = (await serviceRequestRepo.GetAllAsync(r => r.MemberId == id)).ToList();
            foreach (var r in requests) r.Member = Former(r.Member);
            if (requests.Count > 0) await serviceRequestRepo.UpdateRangeAsync(requests);

            // ── Things the member wrote or took part in: kept for everyone else, identity removed ──
            // Forum threads and posts and class notes stay (other people's replies depend on them) but show "Former member".
            var threads = (await forumThreadRepo.GetAllAsync(t => t.AuthorId == id)).ToList();
            foreach (var t in threads) t.Author = Former(t.Author);
            if (threads.Count > 0) await forumThreadRepo.UpdateRangeAsync(threads);

            var posts = (await forumPostRepo.GetAllAsync(p => p.AuthorId == id)).ToList();
            foreach (var p in posts) p.Author = Former(p.Author);
            if (posts.Count > 0) await forumPostRepo.UpdateRangeAsync(posts);

            var notes = (await classNoteRepo.GetAllAsync(n => n.AuthorId == id)).ToList();
            foreach (var n in notes) n.Author = Former(n.Author);
            if (notes.Count > 0) await classNoteRepo.UpdateRangeAsync(notes);

            // Likes this member gave are personal activity: removed, and the notes' counts brought back down.
            var likes = (await classNoteLikeRepo.GetAllAsync(l => l.MemberId == id)).ToList();
            if (likes.Count > 0)
            {
                var likedNoteIds = likes.Select(l => l.ClassNoteId).Distinct().ToList();
                var likedNotes = (await classNoteRepo.GetAllAsync(n => likedNoteIds.Contains(n.Id))).ToList();
                foreach (var note in likedNotes)
                    note.LikeCount = Math.Max(0, note.LikeCount - likes.Count(l => l.ClassNoteId == note.Id));
                if (likedNotes.Count > 0) await classNoteRepo.UpdateRangeAsync(likedNotes);
                foreach (var like in likes) await classNoteLikeRepo.RemoveAsync(like);
            }

            // Referrals: as the referrer the member's name goes (the invited person's own address belongs to them and stays);
            // as the referred person the member's email and name go.
            var referrals = (await referralRepo.GetAllAsync(r => r.ReferrerId == id || r.ReferredMemberId == id)).ToList();
            foreach (var r in referrals)
            {
                if (r.ReferrerId == id) r.Referrer = Former(r.Referrer);
                if (r.ReferredMemberId == id)
                {
                    r.ReferredEmail = $"deleted-{id}@removed.invalid";
                    r.ReferredMember = Former(r.ReferredMember);
                }
            }
            if (referrals.Count > 0) await referralRepo.UpdateRangeAsync(referrals);

            // ── The member row itself: personal fields cleared, sign-in made impossible ─────────
            member.FirstName = "Former";
            member.LastName = "member";
            member.Email = $"deleted-{id}@removed.invalid";
            member.Phone = null;
            member.Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"));
            member.StudentId = null;
            member.DateOfBirth = null;
            member.Program = null;
            member.Company = null;
            member.JobTitle = null;
            member.Location = null;
            member.ShowOnAlumniMap = false;
            member.MapLatitude = null;
            member.MapLongitude = null;
            member.LinkedInUrl = null;
            member.Bio = null;
            member.ProfilePictureUrl = null;
            member.EmailVerificationToken = null;
            member.BanReason = null;
            member.YearOfEntry = null;
            member.House = null;
            member.StudentStatus = null;
            member.PrefectStatus = null;
            member.ClubsAndSocieties = null;
            member.LeadershipRoles = null;
            member.Achievements = null;
            member.Skills = null;
            member.Interests = null;
            member.ReferralCode = null;
            member.IsMembershipActive = false;
            member.Status = "Deleted";
            member.UpdatedBy = id;
            await memberRepo.UpdateAsync(member);

            logger.LogInformation("Member {MemberId} deleted their account", id);
            return new object().ToOkApiResponse("Your account has been deleted.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting account for member {MemberId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("We couldn't delete your account. Please try again or contact your institution.");
        }
    }

    /// <summary>Keeps the member's id and number (so a receipt still ties to a record) but drops name, email and photo.</summary>
    private static MemberSnapshot? Former(MemberSnapshot? snapshot) => snapshot is null ? null : new MemberSnapshot
    {
        Id = snapshot.Id,
        FirstName = "Former",
        LastName = "member",
        Email = string.Empty,
        ProfilePictureUrl = null,
        MemberNumber = snapshot.MemberNumber,
    };
}
