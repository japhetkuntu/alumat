using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Marketing;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Member.Api.Controllers;

/// <summary>Public (no-login) endpoints backing a shared marketing link's landing page — see
/// frontend/apps/member/src/app/campaigns/[token]/page.tsx. Not tenant-scoped: MarketingShare is
/// platform-wide, so every call here uses ignoreQueryFilters explicitly rather than relying on
/// whatever tenant this request's Host happens to resolve to (there may be none, on the bare
/// marketing domain this actually gets opened from).</summary>
[AllowAnonymous]
[Route("api/v{version:apiVersion}/public/marketing")]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
public class PublicMarketingController(
    IAlumniPgRepository<MarketingShare> shareRepo,
    IAlumniPgRepository<MarketingVisit> visitRepo,
    ILogger<PublicMarketingController> logger) : DefaultController
{
    // token is either a MarketingShare's full Id (old links) or its short ShortCode (new
    // links, since MarketingRules.GenerateShortCode) — either resolves the same share.
    [HttpGet("{token}")]
    public async Task<IActionResult> Get(string token)
    {
        var share = await shareRepo.GetOneAsync(s => (s.Id == token || s.ShortCode == token) && !s.Disabled, ignoreQueryFilters: true);
        if (share is null) return NotFound();
        var snapshot = MarketingRules.Snapshot(share);
        return Ok(new
        {
            id = share.Id, share.Channel, share.Caption,
            snapshot.CampaignTitle, snapshot.Title, snapshot.Content, snapshot.DestinationPath, snapshot.UseLandingPage,
            assets = snapshot.Assets.Select(a => new { a.Id, a.Url, a.Name, a.ContentType }),
        });
    }

    public record VisitRequest([Required, RegularExpression("^[a-f0-9-]{32,36}$")] string SessionId);

    [HttpPost("{token}/visits")]
    public async Task<IActionResult> Visit(string token, VisitRequest r)
    {
        var share = await shareRepo.GetOneAsync(s => (s.Id == token || s.ShortCode == token) && !s.Disabled, ignoreQueryFilters: true);
        if (share is null) return NotFound();
        // Always dedup/store against the share's real Id, never the token used to reach it —
        // otherwise a visit via the short code and one via the full Id would double-count.
        if (await visitRepo.GetQueryable(ignoreQueryFilters: true).AnyAsync(v => v.ShareId == share.Id && v.SessionId == r.SessionId))
            return NoContent();

        try
        {
            await visitRepo.AddAsync(new MarketingVisit { ShareId = share.Id, SessionId = r.SessionId });
        }
        catch (Exception e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Concurrent duplicate visit (two tabs, or a retry) — the unique index on
            // (ShareId, SessionId) already prevented the double-write; nothing to do.
            logger.LogDebug("Duplicate marketing visit ignored for share {ShareId}", share.Id);
        }
        return NoContent();
    }
}
