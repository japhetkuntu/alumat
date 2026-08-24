using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

public class BatchService(
    IAlumniPgRepository<Batch> batchRepo,
    ILogger<BatchService> logger) : IBatchService
{
    public async Task<IApiResponse<List<BatchListItem>>> GetBatchesAsync()
    {
        try
        {
            var batches = await batchRepo.GetAllAsync(_ => true);
            var items = batches
                .OrderByDescending(b => b.Year)
                .Select(b => new BatchListItem(b.Id, b.Name, b.Year, b.IsActive))
                .ToList();
            return items.ToOkApiResponse();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error retrieving batches");
            return ApiResponseExtensions.ToServerErrorApiResponse<List<BatchListItem>>("Failed to retrieve batches");
        }
    }

    public async Task<IApiResponse<BatchListItem>> CreateBatchAsync(CreateBatchRequest request, string createdBy)
    {
        try
        {
            var existing = await batchRepo.GetOneAsync(b => b.Year == request.Year);
            if (existing is not null)
                return ApiResponseExtensions.ToConflictApiResponse<BatchListItem>($"A batch for {request.Year} already exists");

            var batch = new Batch
            {
                Name = request.Name.Trim(),
                Year = request.Year,
                CreatedBy = createdBy,
            };
            await batchRepo.AddAsync(batch);

            logger.LogInformation("Batch {BatchId} ({Year}) created by {CreatorId}", batch.Id, batch.Year, createdBy);
            return new BatchListItem(batch.Id, batch.Name, batch.Year, batch.IsActive).ToCreatedApiResponse("Batch created");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating batch for year {Year}", request.Year);
            return ApiResponseExtensions.ToServerErrorApiResponse<BatchListItem>("Failed to create batch");
        }
    }

    public async Task<IApiResponse<BatchListItem>> UpdateBatchAsync(string id, UpdateBatchRequest request, string updatedBy)
    {
        try
        {
            var batch = await batchRepo.GetByIdAsync(id);
            if (batch is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<BatchListItem>("Batch not found");

            var duplicate = await batchRepo.GetOneAsync(b => b.Year == request.Year && b.Id != id);
            if (duplicate is not null)
                return ApiResponseExtensions.ToConflictApiResponse<BatchListItem>($"A batch for {request.Year} already exists");

            batch.Name = request.Name.Trim();
            batch.Year = request.Year;
            batch.IsActive = request.IsActive;
            batch.UpdatedAt = DateTime.UtcNow;
            batch.UpdatedBy = updatedBy;
            await batchRepo.UpdateAsync(batch);

            return new BatchListItem(batch.Id, batch.Name, batch.Year, batch.IsActive).ToOkApiResponse("Batch updated");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating batch {BatchId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<BatchListItem>("Failed to update batch");
        }
    }

    public async Task<IApiResponse<object>> DeleteBatchAsync(string id)
    {
        try
        {
            var batch = await batchRepo.GetByIdAsync(id);
            if (batch is null)
                return ApiResponseExtensions.ToNotFoundApiResponse<object>("Batch not found");

            await batchRepo.RemoveAsync(batch);
            return new object().ToOkApiResponse("Batch deleted");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error deleting batch {BatchId}", id);
            return ApiResponseExtensions.ToServerErrorApiResponse<object>("Failed to delete batch");
        }
    }
}
