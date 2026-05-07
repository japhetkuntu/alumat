using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umat.Alumni.Admin.Api.Models;
using Umat.Alumni.Admin.Api.Services.Implementations;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Common.Sdk.Options;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;
using Umat.Alumni.PostgresDb.Sdk.Repositories;
using Umat.Alumni.Redis.Sdk.Services;
using Xunit;

namespace Umat.Alumni.Admin.Api.Tests;

using AdminEntity = Umat.Alumni.PostgresDb.Sdk.Entities.Alumni.Admin;

public class AdminAuthServiceTests
{
    [Fact]
    public async Task LoginAsync_ReturnsUnauthorized_WhenAdminIsDisabled()
    {
        // Arrange
        var mockRepo = new Mock<IAlumniPgRepository<AdminEntity>>();
        var mockRedis = new Mock<IRedisService<Umat.Alumni.Admin.Api.Options.AdminRedisConfig>>();
        var options = Microsoft.Extensions.Options.Options.Create(new BearerTokenConfig { AdminSigningKey = "secret", Issuer = "test", Audience = "test", AccessTokenLifetime = 1, RefreshTokenLifetime = 1 });
        var logger = new NullLogger<AdminAuthService>();

        var disabledAdmin = new AdminEntity
        {
            Id = "test",
            Email = "disabled@test.com",
            Password = BCrypt.Net.BCrypt.HashPassword("password"),
            Role = "Admin",
            IsDisabled = true,
        };

        mockRepo.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<AdminEntity, bool>>>() ))
            .ReturnsAsync(disabledAdmin);

        var sut = new AdminAuthService(mockRepo.Object, mockRedis.Object, options, logger);

        // Act
        var response = await sut.LoginAsync(new LoginRequest("disabled@test.com", "password"));

        // Assert
        Assert.Equal(401, response.Code);
        Assert.Equal("Account disabled", response.Message);
    }
}
