using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Storage.Sdk.Services;
using Xunit;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class JobServiceTests
{
    [Fact]
    public async Task UpdateJobAsync_ReturnsNotFound_WhenAdminNotInYearGroup()
    {
        // Arrange
        var mockRepo = new Mock<IAlumniPgRepository<Job>>();
        var mockStorage = new Mock<IStorageService>();
        var logger = new NullLogger<JobService>();

        var job = new Job
        {
            Id = "job-1",
            YearGroups = new List<int> { 2026 },
            CreatedBy = "other-admin",
        };

        mockRepo.Setup(r => r.GetByIdAsync(job.Id)).ReturnsAsync(job);

        var service = new JobService(mockRepo.Object, mockStorage.Object, Mock.Of<ReservEase.Alumni.Institution.Api.Services.Interfaces.INotificationActor>(), logger);
        var admin = new AuthData { Id = "admin-1", Role = "Admin", GraduationYear = 2025 };

        // Act
        var response = await service.UpdateJobAsync(new UpdateJobRequest
        {
            JobId = job.Id,
            Title = "Test",
            Company = "TestCo",
            Location = "Testville",
            Type = "Full-time",
            Status = "Active",
        }, admin);

        // Assert
        Assert.Equal(404, response.Code);
        Assert.Equal("Job not found", response.Message);
    }

    [Fact]
    public async Task UpdateJobAsync_ReturnsNotFound_WhenSuperAdminNotCreatorAndHasYearGroups()
    {
        // Arrange
        var mockRepo = new Mock<IAlumniPgRepository<Job>>();
        var mockStorage = new Mock<IStorageService>();
        var logger = new NullLogger<JobService>();

        var job = new Job
        {
            Id = "job-2",
            YearGroups = new List<int> { 2026 },
            CreatedBy = "other-admin",
        };

        mockRepo.Setup(r => r.GetByIdAsync(job.Id)).ReturnsAsync(job);

        var service = new JobService(mockRepo.Object, mockStorage.Object, Mock.Of<ReservEase.Alumni.Institution.Api.Services.Interfaces.INotificationActor>(), logger);
        var admin = new AuthData { Id = "superadmin", Role = "SuperAdmin", GraduationYear = 2025 };

        // Act
        var response = await service.UpdateJobAsync(new UpdateJobRequest
        {
            JobId = job.Id,
            Title = "Test",
            Company = "TestCo",
            Location = "Testville",
            Type = "Full-time",
            Status = "Active",
        }, admin);

        // Assert
        Assert.Equal(404, response.Code);
        Assert.Equal("Job not found", response.Message);
    }
}
