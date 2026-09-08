using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Common.Sdk.Services;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Options;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;
using Xunit;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>Google-auth paths on PlatformAuthService — no coverage existed for this service at all before.</summary>
public class PlatformAuthServiceTests
{
    private static PlatformAuthService BuildSut(
        Mock<IAlumniPgRepository<PlatformStaff>> staffRepo,
        Mock<IGoogleTokenVerifier> googleTokenVerifier)
    {
        var redis = new Mock<IRedisService<PlatformRedisConfig>>();
        var tokenOptions = Microsoft.Extensions.Options.Options.Create(new BearerTokenConfig
        {
            PlatformSigningKey = "test-signing-key-at-least-32-chars-long",
            Issuer = "test", Audience = "test", AccessTokenLifetime = 1, RefreshTokenLifetime = 1,
        });
        var mailtrapOptions = Microsoft.Extensions.Options.Options.Create(new MailtrapConfig());
        var notificationActor = new Mock<INotificationActor>();
        var httpContextAccessor = new Mock<IHttpContextAccessor>();

        return new PlatformAuthService(
            staffRepo.Object, redis.Object, tokenOptions, mailtrapOptions,
            notificationActor.Object, httpContextAccessor.Object, googleTokenVerifier.Object,
            new NullLogger<PlatformAuthService>());
    }

    [Fact]
    public async Task GoogleLoginAsync_ReturnsUnauthorized_WhenTokenInvalid()
    {
        var staffRepo = new Mock<IAlumniPgRepository<PlatformStaff>>();
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>())).ReturnsAsync((GoogleIdentity?)null);
        var sut = BuildSut(staffRepo, googleTokenVerifier);

        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest("bad-token"));

        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task GoogleLoginAsync_ReturnsBadRequest_WhenNoMatchingStaff()
    {
        var staffRepo = new Mock<IAlumniPgRepository<PlatformStaff>>();
        staffRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<PlatformStaff, bool>>>())).ReturnsAsync((PlatformStaff?)null);
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("nomatch@test.com", "Ama", "Owusu", null));
        var sut = BuildSut(staffRepo, googleTokenVerifier);

        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest("good-token"));

        Assert.Equal(400, response.Code);
    }

    [Fact]
    public async Task GoogleLoginAsync_ReturnsUnauthorized_WhenStaffDisabled()
    {
        var staff = new PlatformStaff { Id = "s1", Email = "disabled@test.com", Name = "Ama Owusu", Password = "x", IsDisabled = true };
        var staffRepo = new Mock<IAlumniPgRepository<PlatformStaff>>();
        staffRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<PlatformStaff, bool>>>())).ReturnsAsync(staff);
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("disabled@test.com", "Ama", "Owusu", null));
        var sut = BuildSut(staffRepo, googleTokenVerifier);

        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest("good-token"));

        Assert.Equal(401, response.Code);
        Assert.Equal("Account disabled", response.Message);
    }

    [Fact]
    public async Task GoogleLoginAsync_Succeeds_ForEnabledStaff()
    {
        var staff = new PlatformStaff { Id = "s1", Email = "active@test.com", Name = "Ama Owusu", Password = "x", Role = "Support" };
        var staffRepo = new Mock<IAlumniPgRepository<PlatformStaff>>();
        staffRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<PlatformStaff, bool>>>())).ReturnsAsync(staff);
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("active@test.com", "Ama", "Owusu", null));
        var sut = BuildSut(staffRepo, googleTokenVerifier);

        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest("good-token"));

        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.False(string.IsNullOrEmpty(response.Data!.Tokens.AccessToken));
    }
}
