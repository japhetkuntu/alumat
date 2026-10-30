using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.Notifications.Sdk;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Member.Api.Services.Implementations;

public class EmailChangeService(
    IAlumniPgRepository<MemberEntity> memberRepo,
    IAlumniPgRepository<InstitutionEntity> institutionRepo,
    IRedisService<MemberRedisConfig> redis,
    IMemberAuthService authService,
    ITemporalClientProvider temporalProvider,
    ICurrentTenantService currentTenant,
    IHttpContextAccessor httpContextAccessor,
    ILogger<EmailChangeService> logger) : IEmailChangeService
{
    // Short on purpose: the code is the only thing standing between an unlocked screen and someone's account.
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan UndoLifetime = TimeSpan.FromDays(2);
    public const int MaxWrongGuesses = 5;
    public const int MaxSendsPerChange = 3;
    public const int MaxStartsPerHour = 5;

    private const string StepCurrent = "Current";
    private const string StepNew = "New";

    private static string PendingKey(string memberId) => $"email:change:{memberId}";
    private static string StartsKey(string memberId) => $"email:change:starts:{memberId}";
    private static string UndoKey(string token) => $"email:change:undo:{token}";

    public async Task<IApiResponse<EmailChangeStatusResponse>> GetStatusAsync(AuthData auth)
    {
        try
        {
            var member = await memberRepo.GetByIdAsync(auth.Id);
            if (member is null) return ApiResponseExtensions.ToNotFoundApiResponse<EmailChangeStatusResponse>("Member not found");
            var pending = await redis.GetAsync<PendingEmailChange>(PendingKey(member.Id));
            return StatusOf(member, pending).ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error reading the email change for member {MemberId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<EmailChangeStatusResponse>("Could not load your email change.");
        }
    }

    public async Task<IApiResponse<EmailChangeStatusResponse>> StartAsync(StartEmailChangeRequest request, AuthData auth)
    {
        try
        {
            var member = await memberRepo.GetByIdAsync(auth.Id);
            if (member is null) return ApiResponseExtensions.ToNotFoundApiResponse<EmailChangeStatusResponse>("Member not found");
            if (member.Status != "Active")
                return ApiResponseExtensions.ToForbiddenApiResponse<EmailChangeStatusResponse>("Only an active account can change its email.");

            var newEmail = Normalize(request.NewEmail);
            if (newEmail is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<EmailChangeStatusResponse>("Enter a valid email address.");
            if (string.Equals(newEmail, member.Email, StringComparison.OrdinalIgnoreCase))
                return ApiResponseExtensions.ToBadRequestApiResponse<EmailChangeStatusResponse>("That is already your email address.");
            if (await IsTakenAsync(newEmail, member.Id))
                return ApiResponseExtensions.ToBadRequestApiResponse<EmailChangeStatusResponse>("That address can't be used. Try a different one.");

            // Counted before the code is sent so that asking over and over cannot be used to flood anyone's inbox.
            var starts = await redis.GetAsync<int?>(StartsKey(member.Id)) ?? 0;
            if (starts >= MaxStartsPerHour)
                return ApiResponseExtensions.ToBadRequestApiResponse<EmailChangeStatusResponse>("Too many attempts. Please try again in an hour.");
            await redis.SetAsync(StartsKey(member.Id), starts + 1, TimeSpan.FromHours(1));

            var pending = new PendingEmailChange { NewEmail = newEmail, Step = StepCurrent, Otp = NewCode() };
            await redis.SetAsync(PendingKey(member.Id), pending, CodeLifetime);
            await SendCodeAsync(member.FirstName, member.Email, pending.Otp, "current");

            logger.LogInformation("Email change started for member {MemberId}", member.Id);
            return StatusOf(member, pending).ToOkApiResponse("We sent a code to your current email address.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error starting an email change for member {MemberId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<EmailChangeStatusResponse>("Could not start the email change.");
        }
    }

    public async Task<IApiResponse<EmailChangeStatusResponse>> ResendAsync(AuthData auth)
    {
        try
        {
            var member = await memberRepo.GetByIdAsync(auth.Id);
            if (member is null) return ApiResponseExtensions.ToNotFoundApiResponse<EmailChangeStatusResponse>("Member not found");
            var pending = await redis.GetAsync<PendingEmailChange>(PendingKey(member.Id));
            if (pending is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<EmailChangeStatusResponse>("This email change has expired. Please start again.");
            if (pending.Sends >= MaxSendsPerChange)
                return ApiResponseExtensions.ToBadRequestApiResponse<EmailChangeStatusResponse>("No more codes can be sent for this change. Please start again.");

            pending.Otp = NewCode();
            pending.Sends++;
            pending.Attempts = 0;
            await redis.SetAsync(PendingKey(member.Id), pending, CodeLifetime);

            var toNew = pending.Step == StepNew;
            await SendCodeAsync(member.FirstName, toNew ? pending.NewEmail : member.Email, pending.Otp, toNew ? "new" : "current");
            return StatusOf(member, pending).ToOkApiResponse("We sent a new code.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error resending an email change code for member {MemberId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<EmailChangeStatusResponse>("Could not send a new code.");
        }
    }

    public async Task<IApiResponse<EmailChangeStatusResponse>> ConfirmCurrentAsync(EmailChangeCodeRequest request, AuthData auth)
    {
        try
        {
            var member = await memberRepo.GetByIdAsync(auth.Id);
            if (member is null) return ApiResponseExtensions.ToNotFoundApiResponse<EmailChangeStatusResponse>("Member not found");
            var pending = await redis.GetAsync<PendingEmailChange>(PendingKey(member.Id));
            if (pending is null || pending.Step != StepCurrent)
                return ApiResponseExtensions.ToBadRequestApiResponse<EmailChangeStatusResponse>("This email change has expired. Please start again.");

            var wrong = await CheckCodeAsync(member.Id, pending, request.Code);
            if (wrong is not null) return ApiResponseExtensions.ToBadRequestApiResponse<EmailChangeStatusResponse>(wrong);

            // The address may have been taken by someone else while the first code was being read.
            if (await IsTakenAsync(pending.NewEmail, member.Id))
            {
                await redis.RemoveAsync(PendingKey(member.Id));
                return ApiResponseExtensions.ToBadRequestApiResponse<EmailChangeStatusResponse>("That address can't be used. Please start again with a different one.");
            }

            pending.Step = StepNew;
            pending.Otp = NewCode();
            pending.Attempts = 0;
            pending.Sends = 1;
            await redis.SetAsync(PendingKey(member.Id), pending, CodeLifetime);
            await SendCodeAsync(member.FirstName, pending.NewEmail, pending.Otp, "new");

            return StatusOf(member, pending).ToOkApiResponse("Now enter the code we sent to your new address.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error confirming the current email for member {MemberId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<EmailChangeStatusResponse>("Could not check that code.");
        }
    }

    public async Task<IApiResponse<EmailChangeResultResponse>> ConfirmNewAsync(EmailChangeCodeRequest request, AuthData auth)
    {
        try
        {
            var member = await memberRepo.GetByIdAsync(auth.Id);
            if (member is null) return ApiResponseExtensions.ToNotFoundApiResponse<EmailChangeResultResponse>("Member not found");
            var pending = await redis.GetAsync<PendingEmailChange>(PendingKey(member.Id));
            if (pending is null || pending.Step != StepNew)
                return ApiResponseExtensions.ToBadRequestApiResponse<EmailChangeResultResponse>("This email change has expired. Please start again.");

            var wrong = await CheckCodeAsync(member.Id, pending, request.Code);
            if (wrong is not null) return ApiResponseExtensions.ToBadRequestApiResponse<EmailChangeResultResponse>(wrong);

            var oldEmail = member.Email;
            var swapped = await SwapAsync(member.Id, oldEmail, pending.NewEmail, auth.Id);
            await redis.RemoveAsync(PendingKey(member.Id));
            if (!swapped)
                return ApiResponseExtensions.ToBadRequestApiResponse<EmailChangeResultResponse>("That address can't be used. Please start again with a different one.");

            // Sign out everywhere else: their refresh token is replaced below by the one for this session.
            var fresh = await memberRepo.GetByIdAsync(member.Id);
            var session = await authService.CreateSessionAsync(fresh!);
            if (session.Data is null)
                return ApiResponseExtensions.ToServerErrorApiResponse<EmailChangeResultResponse>("Your email was changed, but we could not sign you in again. Please sign in with the new address.");

            var undoToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            await redis.SetAsync(UndoKey(undoToken),
                new EmailChangeUndo { MemberId = member.Id, OldEmail = oldEmail, NewEmail = pending.NewEmail }, UndoLifetime);
            await SendChangedNoticeAsync(member.FirstName, oldEmail, pending.NewEmail, undoToken, BaseUrl());

            logger.LogInformation("Member {MemberId} changed their email address", member.Id);
            return new EmailChangeResultResponse(session.Data, pending.NewEmail).ToOkApiResponse("Your email address has been changed.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error finishing an email change for member {MemberId}", auth.Id);
            return ApiResponseExtensions.ToServerErrorApiResponse<EmailChangeResultResponse>("Could not change your email.");
        }
    }

    public async Task<IApiResponse<object>> CancelAsync(AuthData auth)
    {
        await redis.RemoveAsync(PendingKey(auth.Id));
        return new object().ToOkApiResponse("Email change cancelled.");
    }

    public async Task<IApiResponse<object>> UndoAsync(UndoEmailChangeRequest request)
    {
        try
        {
            var token = request.Token?.Trim() ?? string.Empty;
            var undo = token.Length is >= 16 and <= 128 ? await redis.GetAsync<EmailChangeUndo>(UndoKey(token)) : null;
            if (undo is null)
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("This link has expired or was already used.");

            var member = await memberRepo.GetByIdAsync(undo.MemberId);
            if (member is null) return ApiResponseExtensions.ToBadRequestApiResponse<object>("This link has expired or was already used.");

            // Only if nothing has happened since: a later change, by the member, supersedes this one.
            if (!string.Equals(member.Email, undo.NewEmail, StringComparison.OrdinalIgnoreCase))
            {
                await redis.RemoveAsync(UndoKey(token));
                return ApiResponseExtensions.ToBadRequestApiResponse<object>("This email address has been changed again since, so this link can no longer be used.");
            }

            if (!await SwapAsync(member.Id, undo.NewEmail, undo.OldEmail, member.Id))
                return ApiResponseExtensions.ToConflictApiResponse<object>("Your old address is now used by another account, so it can't be restored. Please contact your administrator.");

            await redis.RemoveAsync(UndoKey(token));
            await redis.RemoveAsync(PendingKey(member.Id));
            logger.LogWarning("Email change for member {MemberId} was undone from the notice link", member.Id);
            return new object().ToOkApiResponse("Your previous email address has been restored. Everyone has been signed out. If you did not make the change, reset your password now.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error undoing an email change");
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Could not restore your email.");
        }
    }

    /// <summary>
    /// The one place the address is written. The database refuses a second member with the same address, so even two people
    /// racing for it cannot both win; the loser gets false. Refresh tokens are dropped so other devices cannot renew.
    /// </summary>
    private async Task<bool> SwapAsync(string memberId, string from, string to, string by)
    {
        var now = DateTime.UtcNow;
        try
        {
            var changed = await memberRepo.ExecuteUpdateAsync(m => m.Id == memberId && m.Email == from, s => s
                .SetProperty(m => m.Email, to)
                .SetProperty(m => m.IsEmailVerified, true)
                .SetProperty(m => m.UpdatedAt, now)
                .SetProperty(m => m.UpdatedBy, by));
            if (changed == 0) return false;
        }
        catch (Exception e) when (DbErrors.IsUniqueViolation(e))
        {
            return false;
        }
        await redis.RemoveAsync($"member:refresh:{memberId}");
        return true;
    }

    /// <summary>Null when the code is right; otherwise what to tell the member. Too many wrong guesses ends the change.</summary>
    private async Task<string?> CheckCodeAsync(string memberId, PendingEmailChange pending, string? code)
    {
        var given = (code ?? string.Empty).Trim();
        if (given.Length == pending.Otp.Length
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(pending.Otp)))
            return null;

        pending.Attempts++;
        if (pending.Attempts >= MaxWrongGuesses)
        {
            await redis.RemoveAsync(PendingKey(memberId));
            return "Too many wrong codes. Please start again.";
        }
        await redis.SetAsync(PendingKey(memberId), pending, CodeLifetime);
        return "That code is not right.";
    }

    private async Task<bool> IsTakenAsync(string email, string exceptMemberId)
        => await memberRepo.GetOneAsync(m => m.Email == email && m.Id != exceptMemberId) is not null;

    private static string? Normalize(string? raw)
    {
        var email = raw?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email) || email.Length > 254) return null;
        try
        {
            var parsed = new MailAddress(email);
            return parsed.Address == email && parsed.Host.Contains('.') ? email : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string NewCode() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    private static EmailChangeStatusResponse StatusOf(MemberEntity member, PendingEmailChange? pending)
        => pending is null
            ? new EmailChangeStatusResponse("None", null, Mask(member.Email), null)
            : new EmailChangeStatusResponse(pending.Step, pending.NewEmail, Mask(member.Email), Mask(pending.NewEmail));

    /// <summary>"ama.mensah@gmail.com" becomes "am•••@gmail.com": enough to recognise, not enough to harvest.</summary>
    public static string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return email;
        var local = email[..at];
        var shown = local.Length <= 2 ? local[..1] : local[..2];
        return $"{shown}•••{email[at..]}";
    }

    private string BaseUrl()
    {
        var request = httpContextAccessor.HttpContext?.Request;
        return request is null ? "https://example.com" : $"{request.Scheme}://{request.Host}";
    }

    private async Task<(string? Name, string? Color, string? SecondaryColor, string? Logo)> BrandAsync()
    {
        if (string.IsNullOrEmpty(currentTenant.InstitutionId)) return (null, null, null, null);
        var institution = await institutionRepo.GetByIdAsync(currentTenant.InstitutionId);
        var name = string.IsNullOrWhiteSpace(institution?.PortalName) ? institution?.Name : institution.PortalName;
        return (name, institution?.PrimaryColorHex, institution?.SecondaryColorHex, institution?.LogoUrl);
    }

    private async Task SendCodeAsync(string firstName, string to, string code, string which)
    {
        var brand = await BrandAsync();
        await temporalProvider.EnqueueNotificationAsync(
            NotificationRequest.Email(
                new SendEmailRequest
                {
                    To = [new EmailContact { Email = to, Name = firstName }],
                    TemplateId = "email-change-code",
                    TemplateVariables = new
                    {
                        first_name = firstName,
                        otp_code = code,
                        intro = which == "new"
                            ? "Enter this code to confirm this is your new email address."
                            : "Someone asked to change the email address on your account. If that was you, enter this code to continue.",
                        brand_name = brand.Name, brand_color = brand.Color, brand_secondary_color = brand.SecondaryColor, brand_logo = brand.Logo,
                    },
                },
                $"email change code ({which}) to {to}"),
            logger);
    }

    private async Task SendChangedNoticeAsync(string firstName, string oldEmail, string newEmail, string undoToken, string baseUrl)
    {
        var brand = await BrandAsync();
        await temporalProvider.EnqueueNotificationAsync(
            NotificationRequest.Email(
                new SendEmailRequest
                {
                    To = [new EmailContact { Email = oldEmail, Name = firstName }],
                    TemplateId = "notification",
                    TemplateVariables = new
                    {
                        first_name = firstName,
                        badge_label = "Security",
                        title = "Your email address was changed",
                        body = $"The email on your account was changed to {Mask(newEmail)}. If this was you, there is nothing to do. "
                            + "If it was not, restore your old address now (this link works for 2 days) and then reset your password.",
                        action_url = $"{baseUrl}/undo-email-change?token={undoToken}",
                        action_label = "Restore my old email",
                        brand_name = brand.Name, brand_color = brand.Color, brand_secondary_color = brand.SecondaryColor, brand_logo = brand.Logo,
                    },
                },
                $"email changed notice to {oldEmail}"),
            logger);
    }
}
