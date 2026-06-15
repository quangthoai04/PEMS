using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace PEMS.ArchitectureTests;

public class NamespaceAndConcreteClassTests
{
    private static readonly Assembly ApplicationAssembly = Assembly.Load("PEMS.Application");

    [Fact]
    public void Application_ShouldNot_UseInfrastructureNamespace()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("PEMS.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, "PEMS.Application must not use PEMS.Infrastructure namespace.");
    }

    [Fact]
    public void Application_ShouldNot_UseConcreteClasses()
    {
        var concreteClasses = new[]
        {
            "ApplicationDbContext",
            "UserRepository",
            "DelegationRepository",
            "JwtTokenService",
            "PasswordHasher",
            "RefreshTokenStore",
            "EmailService",
            "RedisRateLimitStore",
            "OcrService",
            "FaceRecognitionService"
        };

        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(concreteClasses)
            .GetResult();

        Assert.True(result.IsSuccessful, "PEMS.Application must not use concrete classes like ApplicationDbContext, UserRepository, etc.");
    }
}
