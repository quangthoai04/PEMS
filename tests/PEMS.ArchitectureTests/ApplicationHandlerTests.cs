using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace PEMS.ArchitectureTests;

public class ApplicationHandlerTests
{
    private static readonly Assembly ApplicationAssembly = Assembly.Load("PEMS.Application");

    [Fact]
    public void Handlers_ShouldNot_InjectInfrastructureConcreteClass()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("Handler")
            .ShouldNot()
            .HaveDependencyOn("PEMS.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, "Handlers must not inject Infrastructure concrete classes.");
    }

    [Fact]
    public void Handlers_ShouldNot_InjectSpecificInfrastructureConcreteClasses()
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
            .That()
            .HaveNameEndingWith("Handler")
            .ShouldNot()
            .HaveDependencyOnAny(concreteClasses)
            .GetResult();

        Assert.True(result.IsSuccessful, "Handlers must not inject specific Infrastructure concrete classes.");
    }
}
