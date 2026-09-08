using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Common.Sdk.Services;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Options;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Storage.Sdk.Services;
using Xunit;

namespace ReservEase.Alumni.Institution.Api.Tests;

using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
using Referral = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Referral;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

/// <summary>
/// Google-auth paths on MemberAuthService — the only ones this service had no
/// coverage for at all. Password login/register are exercised indirectly by
/// the same success/failure shapes, but not duplicated here.
/// </summary>
public class MemberAuthServiceTests
{
    private static MemberAuthService BuildSut(
        Mock<IAlumniPgRepository<MemberEntity>> memberRepo,
        Mock<IGoogleTokenVerifier> googleTokenVerifier,
        Mock<IRedisService<MemberRedisConfig>>? redis = null)
    {
        var referralRepo = new Mock<IAlumniPgRepository<Referral>>();
        var institutionRepo = new Mock<IAlumniPgRepository<InstitutionEntity>>();
        var currentTenant = new Mock<ICurrentTenantService>();
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        redis ??= new Mock<IRedisService<MemberRedisConfig>>();
        var tokenOptions = Microsoft.Extensions.Options.Options.Create(new BearerTokenConfig
        {
            MemberSigningKey = "test-signing-key-at-least-32-chars-long",
            Issuer = "test", Audience = "test", AccessTokenLifetime = 1, RefreshTokenLifetime = 1,
        });
        var mailtrapOptions = Microsoft.Extensions.Options.Options.Create(new MailtrapConfig());
        var notificationActor = new Mock<INotificationActor>();
        var storageService = new Mock<IStorageService>();

        return new MemberAuthService(
            memberRepo.Object, referralRepo.Object, institutionRepo.Object, currentTenant.Object,
            httpContextAccessor.Object, redis.Object, tokenOptions, mailtrapOptions,
            notificationActor.Object, storageService.Object, googleTokenVerifier.Object,
            new NullLogger<MemberAuthService>());
    }

    [Fact]
    public async Task GoogleLoginAsync_ReturnsUnauthorized_WhenTokenInvalid()
    {
        var memberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>())).ReturnsAsync((GoogleIdentity?)null);
        var sut = BuildSut(memberRepo, googleTokenVerifier);

        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest("bad-token"));

        Assert.Equal(401, response.Code);
        memberRepo.Verify(r => r.GetOneAsync(It.IsAny<Expression<Func<MemberEntity, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task GoogleLoginAsync_ReturnsBadRequest_WhenNoMatchingMember()
    {
        var memberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();
        memberRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<MemberEntity, bool>>>())).ReturnsAsync((MemberEntity?)null);
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("nomatch@test.com", "Kwame", "Mensah", null));
        var sut = BuildSut(memberRepo, googleTokenVerifier);

        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest("good-token"));

        Assert.Equal(400, response.Code);
        Assert.Contains("register", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Banned")]
    [InlineData("Blocked")]
    [InlineData("Suspended")]
    public async Task GoogleLoginAsync_ReturnsBadRequest_ForNonActiveStatuses(string status)
    {
        var member = new MemberEntity { Id = "m1", Email = "match@test.com", Status = status, Password = "x" };
        var memberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();
        memberRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<MemberEntity, bool>>>())).ReturnsAsync(member);
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("match@test.com", "Kwame", "Mensah", null));
        var sut = BuildSut(memberRepo, googleTokenVerifier);

        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest("good-token"));

        Assert.Equal(400, response.Code);
    }

    [Fact]
    public async Task GoogleLoginAsync_Succeeds_ForActiveMember()
    {
        var member = new MemberEntity
        {
            Id = "m1", Email = "active@test.com", FirstName = "Kwame", LastName = "Mensah",
            Status = "Active", Password = "x", GraduationYear = 2020,
        };
        var memberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();
        memberRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<MemberEntity, bool>>>())).ReturnsAsync(member);
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("active@test.com", "Kwame", "Mensah", null));
        var redis = new Mock<IRedisService<MemberRedisConfig>>();
        var sut = BuildSut(memberRepo, googleTokenVerifier, redis);

        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest("good-token"));

        Assert.Equal(200, response.Code);
        Assert.NotNull(response.Data);
        Assert.False(string.IsNullOrEmpty(response.Data!.Tokens.AccessToken));
        redis.Verify(r => r.SetAsync(
            It.Is<string>(k => k == "member:refresh:m1"),
            It.IsAny<string>(),
            It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task GoogleRegisterAsync_ReturnsConflict_WhenActiveAccountAlreadyExists()
    {
        var existing = new MemberEntity { Id = "m1", Email = "taken@test.com", Status = "Active", Password = "x" };
        var memberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();
        memberRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<MemberEntity, bool>>>())).ReturnsAsync(existing);
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("taken@test.com", "Ama", "Owusu", null));
        var sut = BuildSut(memberRepo, googleTokenVerifier);

        var response = await sut.GoogleRegisterAsync(new GoogleRegisterRequest("good-token", "0244000000", "STU1", 2021, null));

        Assert.Equal(409, response.Code);
        memberRepo.Verify(r => r.AddAsync(It.IsAny<MemberEntity>()), Times.Never);
    }

    [Fact]
    public async Task GoogleRegisterAsync_CreatesPendingMember_WhenNoExistingAccount()
    {
        var memberRepo = new Mock<IAlumniPgRepository<MemberEntity>>();
        memberRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<MemberEntity, bool>>>())).ReturnsAsync((MemberEntity?)null);
        MemberEntity? created = null;
        memberRepo.Setup(r => r.AddAsync(It.IsAny<MemberEntity>()))
            .Callback<MemberEntity>(m => created = m)
            .ReturnsAsync(1);
        var googleTokenVerifier = new Mock<IGoogleTokenVerifier>();
        googleTokenVerifier.Setup(v => v.VerifyAsync(It.IsAny<string>()))
            .ReturnsAsync(new GoogleIdentity("new@test.com", "Ama", "Owusu", null));
        var sut = BuildSut(memberRepo, googleTokenVerifier);

        var response = await sut.GoogleRegisterAsync(new GoogleRegisterRequest("good-token", "0244000000", "STU1", 2021, null));

        Assert.Equal(201, response.Code);
        Assert.NotNull(created);
        Assert.Equal("new@test.com", created!.Email);
        Assert.Equal("Pending", created.Status);
        Assert.True(created.IsEmailVerified);
        Assert.Equal("google", created.CreatedBy);
        Assert.False(string.IsNullOrEmpty(created.Password)); // never-typed placeholder hash, but must not be blank
    }
}
