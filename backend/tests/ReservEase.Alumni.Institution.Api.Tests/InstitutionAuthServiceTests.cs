using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using Microsoft.AspNetCore.Http;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Mailtrap.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using Xunit;

namespace ReservEase.Alumni.Institution.Api.Tests;

using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

public class InstitutionAuthServiceTests
{
    [Fact]
    public async Task LoginAsync_ReturnsUnauthorized_WhenAdminIsDisabled()
    {
        // Arrange
        var mockRepo = new Mock<IAlumniPgRepository<StaffEntity>>();
        var mockRedis = new Mock<IRedisService<ReservEase.Alumni.Institution.Api.Options.InstitutionRedisConfig>>();
        var options = Microsoft.Extensions.Options.Options.Create(new BearerTokenConfig { InstitutionSigningKey = "secret", Issuer = "test", Audience = "test", AccessTokenLifetime = 1, RefreshTokenLifetime = 1 });
        var logger = new NullLogger<InstitutionAuthService>();

        var disabledAdmin = new StaffEntity
        {
            Id = "test",
            Email = "disabled@test.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password"),
            Role = "Admin",
            IsDisabled = true,
        };

        mockRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<StaffEntity, bool>>>() ))
            .ReturnsAsync(disabledAdmin);

        var mockInstitutionRepo = new Mock<IAlumniPgRepository<InstitutionEntity>>();
        var mockCurrentTenant = new Mock<ICurrentTenantService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockMailtrapOptions = Microsoft.Extensions.Options.Options.Create(new MailtrapConfig());
        var mockEmailService = new Mock<IEmailService>();

        var sut = new InstitutionAuthService(
            mockRepo.Object, mockInstitutionRepo.Object, mockCurrentTenant.Object, mockHttpContextAccessor.Object,
            mockRedis.Object, options, mockMailtrapOptions, mockEmailService.Object, logger);

        // Act
        var response = await sut.LoginAsync(new LoginRequest("disabled@test.com", "password"));

        // Assert
        Assert.Equal(401, response.Code);
        Assert.Equal("Account disabled", response.Message);
    }
}
