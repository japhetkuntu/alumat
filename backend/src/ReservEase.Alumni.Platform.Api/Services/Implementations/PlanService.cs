using Microsoft.EntityFrameworkCore;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Platform.Api.Services.Implementations;

public class PlanService(AlumniDbContext db, IAuditLogService auditLog) : IPlanService
{
    public async Task<IApiResponse<List<PlanResponse>>> GetPlansAsync()
    {
        var plans = await db.Plans.OrderBy(p => p.SortOrder).ToListAsync();
        var items = new List<PlanResponse>();
        foreach (var p in plans)
        {
            var subscriberCount = await db.Institutions.CountAsync(i => i.Plan == p.Name);
            items.Add(new PlanResponse(p.Id, p.Name, p.Price, p.BillingInterval, p.MemberLimit, p.StorageLimitGb, p.Modules, p.SupportLevel, p.IsMostUsed, subscriberCount));
        }
        return items.ToOkApiResponse();
    }

    public async Task<IApiResponse<PlanResponse>> CreateAsync(CreatePlanRequest request, string actorId, string actorName)
    {
        if (await db.Plans.AnyAsync(p => p.Name == request.Name))
            return ApiResponseExtensions.ToConflictApiResponse<PlanResponse>("A plan with that name already exists");

        var maxSort = await db.Plans.MaxAsync(p => (int?)p.SortOrder) ?? -1;
        var plan = new Plan
        {
            Name = request.Name,
            Price = request.Price,
            BillingInterval = request.BillingInterval,
            MemberLimit = request.MemberLimit,
            StorageLimitGb = request.StorageLimitGb,
            Modules = request.Modules,
            SupportLevel = request.SupportLevel,
            IsMostUsed = request.IsMostUsed,
            SortOrder = maxSort + 1,
            CreatedBy = actorId,
        };
        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        await auditLog.LogAsync(actorId, actorName, "created plan", plan.Name);

        return new PlanResponse(plan.Id, plan.Name, plan.Price, plan.BillingInterval, plan.MemberLimit, plan.StorageLimitGb, plan.Modules, plan.SupportLevel, plan.IsMostUsed, 0)
            .ToCreatedApiResponse();
    }
}
