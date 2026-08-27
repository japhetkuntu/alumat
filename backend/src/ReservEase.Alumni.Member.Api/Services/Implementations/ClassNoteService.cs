using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Actors;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class ClassNoteService(
    IAlumniPgRepository<ClassNote> classNoteRepo,
    IAlumniPgRepository<ClassNoteLike> likeRepo,
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<Campaign> campaignRepo,
    IAlumniPgRepository<Contribution> contributionRepo,
    IAlumniPgRepository<CommunityMembership> membershipRepo,
    INotificationActor notificationActor,
    ICurrentTenantService currentTenant,
    ILogger<ClassNoteService> logger) : IClassNoteService
{
    private async Task<bool> IsApprovedCommunityMemberAsync(string communityId, string memberId)
    {
        var membership = await membershipRepo.GetOneAsync(m => m.CommunityId == communityId && m.MemberId == memberId);
        return membership is not null && membership.Status == "Approved";
    }

    private async Task<bool> IsMembershipActiveAsync(string memberId, int graduationYear)
    {
        var currentYear = DateTime.UtcNow.Year;
        var requiredCampaigns = await campaignRepo.GetAllAsync(c =>
            c.IsMembershipCampaign && c.MembershipYear.HasValue
            && c.MembershipYear.Value >= graduationYear
            && c.MembershipYear.Value <= currentYear);
        if (requiredCampaigns.Any())
        {
            var requiredIds = requiredCampaigns.Select(c => c.Id).ToHashSet();
            var confirmed = await contributionRepo.GetAllAsync(c => c.MemberId == memberId && c.Status == "Confirmed");
            return confirmed.Count(c => requiredIds.Contains(c.CampaignId)) >= requiredIds.Count;
        }
        var member = await memberRepo.GetByIdAsync(memberId);
        return member?.IsMembershipActive ?? false;
    }

    public async Task<IApiResponse<PgPagedResult<ClassNoteDto>>> GetClassNotesAsync(int page, int pageSize, AuthData member, string? communityId = null)
    {
        try
        {
            var memberEntity = await memberRepo.GetByIdAsync(member.Id);
            if (memberEntity is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<PgPagedResult<ClassNoteDto>>("Member not found");

            PgPagedResult<ClassNote> result;

            if (!string.IsNullOrEmpty(communityId))
            {
                // Community walls are gated by community membership, not by
                // year-group alumni-dues status — a separate, unrelated concept.
                if (!await IsApprovedCommunityMemberAsync(communityId, member.Id))
                    return ApiResponseExtensions.ToForbiddenApiResponse<PgPagedResult<ClassNoteDto>>("You must be an approved member of this community to view its wall");

                result = await classNoteRepo.GetPagedAsync(
                    page, pageSize, "CreatedAt", "desc",
                    n => n.CommunityId == communityId && !n.IsDeleted);
            }
            else
            {
                if (!await IsMembershipActiveAsync(member.Id, memberEntity.GraduationYear))
                    return ApiResponseExtensions.ToBadRequestApiResponse<PgPagedResult<ClassNoteDto>>("Active membership required to view class notes.");

                var yearGroup = memberEntity.GraduationYear;
                result = await classNoteRepo.GetPagedAsync(
                    page, pageSize, "CreatedAt", "desc",
                    n => n.YearGroup == yearGroup && n.CommunityId == null && !n.IsDeleted);
            }

            // Check which notes the current member has liked
            var noteIds = result.Results.Select(n => n.Id).ToList();
            var myLikes = await likeRepo.GetQueryable(l => l.MemberId == member.Id && noteIds.Contains(l.ClassNoteId))
                .Select(l => l.ClassNoteId).ToListAsync();
            var likedSet = myLikes.ToHashSet();

            var dtos = result.Results.Select(n => n.ToDto(likedSet.Contains(n.Id))).ToList();

            return new PgPagedResult<ClassNoteDto>
            {
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                Count = result.Count,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                LowerBoundSize = result.LowerBoundSize,
                UpperBoundSize = result.UpperBoundSize,
                Results = dtos,
            }.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving class notes for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<PgPagedResult<ClassNoteDto>>("Failed to retrieve class notes");
        }
    }

    public async Task<IApiResponse<ClassNoteDto>> CreateClassNoteAsync(CreateClassNoteRequest request, AuthData member)
    {
        try
        {
            var memberEntity = await memberRepo.GetByIdAsync(member.Id);
            if (memberEntity is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<ClassNoteDto>("Member not found");

            if (!string.IsNullOrEmpty(request.CommunityId))
            {
                if (!await IsApprovedCommunityMemberAsync(request.CommunityId, member.Id))
                    return ApiResponseExtensions.ToForbiddenApiResponse<ClassNoteDto>("You must be an approved member of this community to post here");
            }
            else if (!await IsMembershipActiveAsync(member.Id, memberEntity.GraduationYear))
            {
                return ApiResponseExtensions.ToBadRequestApiResponse<ClassNoteDto>("Active membership required to post class notes.");
            }

            var note = new ClassNote
            {
                CommunityId = request.CommunityId,
                AuthorId = member.Id,
                Author = new MemberSnapshot
                {
                    Id = member.Id,
                    FirstName = member.FirstName,
                    LastName = member.LastName,
                    Email = member.Email,
                    ProfilePictureUrl = member.ProfilePictureUrl,
                },
                YearGroup = memberEntity.GraduationYear,
                Content = request.Content,
                ImageUrl = request.ImageUrl,
                CreatedBy = member.Id,
            };

            await classNoteRepo.AddAsync(note);
            // Class-note alerts notify the institution-wide year-group wall — a
            // community wall's own members already see new posts live in the
            // list, so the alert is deliberately skipped for community notes.
            if (string.IsNullOrEmpty(request.CommunityId))
            {
                var authorName = $"{member.FirstName} {member.LastName}";
                notificationActor.Tell(new DispatchClassNoteAlertCommand(currentTenant.InstitutionId!, note, authorName));
            }
            return note.ToDto().ToCreatedApiResponse("Class note posted.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating class note for member {MemberId}", member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<ClassNoteDto>("Failed to create class note");
        }
    }

    public async Task<IApiResponse<object>> ToggleLikeAsync(string classNoteId, AuthData member)
    {
        try
        {
            var note = await classNoteRepo.GetByIdAsync(classNoteId);
            if (note is null || note.IsDeleted)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Class note not found");

            var existingLike = await likeRepo.GetOneAsync(l => l.ClassNoteId == classNoteId && l.MemberId == member.Id);

            if (existingLike is not null)
            {
                await likeRepo.RemoveAsync(existingLike);
                note.LikeCount = Math.Max(0, note.LikeCount - 1);
                await classNoteRepo.UpdateAsync(note);
                return ((object)new { Liked = false, note.LikeCount }).ToOkApiResponse("Like removed.");
            }

            var like = new ClassNoteLike
            {
                ClassNoteId = classNoteId,
                MemberId = member.Id,
                CreatedBy = member.Id,
            };
            await likeRepo.AddAsync(like);
            note.LikeCount += 1;
            await classNoteRepo.UpdateAsync(note);
            return ((object)new { Liked = true, note.LikeCount }).ToOkApiResponse("Like added.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error toggling like on note {NoteId} for member {MemberId}", classNoteId, member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to toggle like");
        }
    }

    public async Task<IApiResponse<object>> DeleteClassNoteAsync(string classNoteId, AuthData member)
    {
        try
        {
            var note = await classNoteRepo.GetByIdAsync(classNoteId);
            if (note is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Class note not found");

            if (note.AuthorId != member.Id)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("You can only delete your own class notes.");

            note.IsDeleted = true;
            note.UpdatedBy = member.Id;
            await classNoteRepo.UpdateAsync(note);
            return ((object)new { Message = "Class note deleted." }).ToOkApiResponse("Deleted.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting class note {NoteId} for member {MemberId}", classNoteId, member.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete class note");
        }
    }
}
