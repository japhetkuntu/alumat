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
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Common.Sdk.Services;
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
            Role = "SuperAdmin",
            IsDisabled = true,
        };

        mockRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<StaffEntity, bool>>>() ))
            .ReturnsAsync(disabledAdmin);

        var mockInstitutionRepo = new Mock<IAlumniPgRepository<InstitutionEntity>>();
        var mockCurrentTenant = new Mock<ICurrentTenantService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockMailtrapOptions = Microsoft.Extensions.Options.Options.Create(new MailtrapConfig());
        var mockNotificationActor = new Mock<INotificationActor>();
        var mockGoogleTokenVerifier = new Mock<IGoogleTokenVerifier>();

        var sut = new InstitutionAuthService(
            mockRepo.Object, mockInstitutionRepo.Object, mockCurrentTenant.Object, mockHttpContextAccessor.Object,
            mockRedis.Object, options, mockMailtrapOptions, mockNotificationActor.Object, mockGoogleTokenVerifier.Object, logger);

        // Act
        var response = await sut.LoginAsync(new LoginRequest("disabled@test.com", "password"));

        // Assert
        Assert.Equal(401, response.Code);
        Assert.Equal("Account disabled", response.Message);
    }

    private static InstitutionAuthService BuildSutForGoogle(
        Mock<IAlumniPgRepository<StaffEntity>> adminRepo, Mock<IGoogleTokenVerifier> googleTokenVerifier)
    {
        var redis = new Mock<IRedisService<ReservEase.Alumni.Institution.Api.Options.InstitutionRedisConfig>>();
        var options = Microsoft.Extensions.Options.Options.Create(new BearerTokenConfig
        {
            InstitutionSigningKey = "test-signing-key-at-least-32-chars-long",
            Issuer = "test", Audience = "test", AccessTokenLifetime = 1, RefreshTokenLifetime = 1,
        });
        var institutionRepo = new Mock<IAlumniPgRepository<InstitutionEntity>>();
        var currentTenant = new Mock<ICurrentTenantService>();
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        var mailtrapOptions = Microsoft.Extensions.Options.Options.Create(new MailtrapConfig());
        var notificationActor = new Mock<INotificationActor>();

        return new InstitutionAuthService(
            adminRepo.Object, institutionRepo.Object, currentTenant.Object, httpContextAccessor.Object,
            redis.Object, options, mailtrapOptions, notificationActor.Object, googleTokenVerifier.Object,
            new NullLogger<InstitutionAuthService>());
    }

    [Fact]
    public async Task GoogleLoginAsync_ReturnsUnauthorized_WhenTokenInvalid()
    {
        var adminRepo = new Mock<IAlumniPgRepository<StaffEntity>>();
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>())).ReturnsAsync((GoogleIdentity?)null);
        var sut = BuildSutForGoogle(adminRepo, googleTokenVerifier);

        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest("bad-token"));

        Assert.Equal(401, response.Code);
        adminRepo.Verify(r => r.GetOneAsync(It.IsAny<Expression<Func<StaffEntity, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task GoogleLoginAsync_ReturnsBadRequest_WhenNoMatchingStaff()
    {
        var adminRepo = new Mock<IAlumniPgRepository<StaffEntity>>();
        adminRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<StaffEntity, bool>>>())).ReturnsAsync((StaffEntity?)null);
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("nomatch@test.com", "Kwame", "Mensah", null));
        var sut = BuildSutForGoogle(adminRepo, googleTokenVerifier);

        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest("good-token"));

        Assert.Equal(400, response.Code);
    }

    [Fact]
    public async Task GoogleLoginAsync_ReturnsUnauthorized_WhenAdminDisabled_ViaGoogle()
    {
        var admin = new StaffEntity { Id = "a1", Email = "disabled@test.com", Password = "x", Role = "SuperAdmin", IsDisabled = true };
        var adminRepo = new Mock<IAlumniPgRepository<StaffEntity>>();
        adminRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<StaffEntity, bool>>>())).ReturnsAsync(admin);
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("disabled@test.com", "Kwame", "Mensah", null));
        var sut = BuildSutForGoogle(adminRepo, googleTokenVerifier);

        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest("good-token"));

        Assert.Equal(401, response.Code);
        Assert.Equal("Account disabled", response.Message);
    }

    [Fact]
    public async Task GoogleLoginAsync_Succeeds_ForEnabledStaff()
    {
        var admin = new StaffEntity { Id = "a1", Email = "active@test.com", FirstName = "Kwame", LastName = "Mensah", Password = "x", Role = "SuperAdmin", IsDisabled = false };
        var adminRepo = new Mock<IAlumniPgRepository<StaffEntity>>();
        adminRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<StaffEntity, bool>>>())).ReturnsAsync(admin);
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("active@test.com", "Kwame", "Mensah", null));
        var sut = BuildSutForGoogle(adminRepo, googleTokenVerifier);

        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest("good-token"));

        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        // Token strings no longer travel in the response body — they're set as
        // httpOnly cookies instead (see AuthCookieExtensions). ExpiresIn is what's left.
        Assert.True(response.Data!.Tokens.ExpiresIn > 0);
    }
}
